using MetaForge.Application.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MetaForge.Infrastructure.Email;

/// <summary>
/// Background worker that processes the email outbox via channel queue with DB-backed retry.
/// </summary>
public sealed class EmailBackgroundService : BackgroundService
{
    private readonly IEmailQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EmailOptions _options;
    private readonly ILogger<EmailBackgroundService> _logger;

    public EmailBackgroundService(
        IEmailQueue queue,
        IServiceScopeFactory scopeFactory,
        IOptions<EmailOptions> options,
        ILogger<EmailBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequeuePendingFromDatabaseAsync(stoppingToken);

        _ = Task.Run(() => RetrySweepLoopAsync(stoppingToken), stoppingToken);

        await foreach (var messageId in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                await ProcessMessageAsync(messageId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing email message {MessageId}", messageId);
            }
        }
    }

    private async Task RetrySweepLoopAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.RetrySweepIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
                await RequeueDueRetriesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email retry sweep failed");
            }
        }
    }

    private async Task RequeuePendingFromDatabaseAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MetaForgeDbContext>();

        var pendingIds = await db.EmailMessages
            .AsNoTracking()
            .Where(m => m.Status == EmailStatus.Queued
                || (m.Status == EmailStatus.Retrying && (m.NextAttemptUtc == null || m.NextAttemptUtc <= DateTime.UtcNow)))
            .OrderBy(m => m.CreatedUtc)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        foreach (var id in pendingIds)
            await _queue.EnqueueAsync(id, cancellationToken);

        if (pendingIds.Count > 0)
            _logger.LogInformation("Re-queued {Count} pending email messages on startup", pendingIds.Count);
    }

    private async Task RequeueDueRetriesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MetaForgeDbContext>();

        var dueIds = await db.EmailMessages
            .AsNoTracking()
            .Where(m => m.Status == EmailStatus.Retrying
                && m.NextAttemptUtc != null
                && m.NextAttemptUtc <= DateTime.UtcNow)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        foreach (var id in dueIds)
            await _queue.EnqueueAsync(id, cancellationToken);
    }

    private async Task ProcessMessageAsync(int messageId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MetaForgeDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailChannelSender>();
        var retryEvaluator = scope.ServiceProvider.GetRequiredService<IRetryPolicyEvaluator>();

        var message = await db.EmailMessages
            .Include(m => m.EmailChannel)
            .Include(m => m.RetryPolicy)
            .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

        if (message == null)
            return;

        if (message.Status is EmailStatus.Sent or EmailStatus.Cancelled or EmailStatus.Failed)
            return;

        if (message.Status == EmailStatus.Retrying
            && message.NextAttemptUtc.HasValue
            && message.NextAttemptUtc > DateTime.UtcNow)
            return;

        message.Status = EmailStatus.Sending;
        message.AttemptCount++;
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await sender.SendAsync(message, message.EmailChannel, cancellationToken);
            message.Status = EmailStatus.Sent;
            message.SentUtc = DateTime.UtcNow;
            message.NextAttemptUtc = null;
            message.LastError = null;
        }
        catch (Exception ex)
        {
            message.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            var delay = retryEvaluator.GetNextDelay(message.RetryPolicy, message.AttemptCount);

            if (delay == null)
            {
                message.Status = EmailStatus.Failed;
                message.NextAttemptUtc = null;
                _logger.LogWarning("Email {MessageId} failed permanently after {Attempts} attempts", messageId, message.AttemptCount);
            }
            else
            {
                message.Status = EmailStatus.Retrying;
                message.NextAttemptUtc = DateTime.UtcNow.Add(delay.Value);
                _logger.LogWarning(ex, "Email {MessageId} failed attempt {Attempt}, retry at {NextAttempt}",
                    messageId, message.AttemptCount, message.NextAttemptUtc);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
