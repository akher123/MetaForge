using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MetaForge.Infrastructure.Audit;

/// <summary>
/// Background worker that persists audit log entries from the in-process channel queue.
/// </summary>
public sealed class AuditBackgroundService : BackgroundService
{
    private readonly IAuditQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditBackgroundService> _logger;

    public AuditBackgroundService(
        IAuditQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<AuditBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var entry in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<IAuditLogStore>();
                await store.WriteAsync(entry, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to persist audit log for {Entity} {RecordId} {Action}",
                    entry.EntityName,
                    entry.RecordId,
                    entry.Action);
            }
        }
    }
}
