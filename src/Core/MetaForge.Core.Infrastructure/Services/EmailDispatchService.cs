using System.Text.Json;

using MetaForge.Infrastructure.Dynamic;

namespace MetaForge.Infrastructure.Services;

/// <summary>
/// Enqueues outbound emails into the durable outbox and signals the background worker.
/// </summary>
public class EmailDispatchService : IEmailDispatchService
{
    private readonly MetaForgeDbContext _db;
    private readonly IEntityTypeResolver _typeResolver;
    private readonly IEmailTemplateRenderer _renderer;
    private readonly IEmailQueue _queue;

    public EmailDispatchService(
        MetaForgeDbContext db,
        IEntityTypeResolver typeResolver,
        IEmailTemplateRenderer renderer,
        IEmailQueue queue)
    {
        _db = db;
        _typeResolver = typeResolver;
        _renderer = renderer;
        _queue = queue;
    }

    public async Task<int> EnqueueFromTemplateAsync(EmailSendRequest request, CancellationToken cancellationToken = default)
    {
        var template = await _db.EmailTemplates
            .Include(t => t.EmailChannel)
            .Include(t => t.RetryPolicy)
            .FirstOrDefaultAsync(t => t.Code == request.TemplateCode && t.IsActive, cancellationToken)
            ?? throw new NotFoundException($"Email template '{request.TemplateCode}' was not found.");

        var record = await LoadRecordAsync(request.EntityName, request.RecordId, cancellationToken);
        var tokens = BuildTokens(record, request.AdditionalTokens);

        var rendered = _renderer.Render(template, tokens);

        if (!string.IsNullOrWhiteSpace(request.ToAddress))
            rendered.ToAddress = request.ToAddress.Trim();
        if (!string.IsNullOrWhiteSpace(request.Cc))
            rendered.Cc = request.Cc.Trim();
        if (!string.IsNullOrWhiteSpace(request.Bcc))
            rendered.Bcc = request.Bcc.Trim();

        var channel = await ResolveChannelAsync(template.EmailChannelId, cancellationToken);
        var policy = await ResolvePolicyAsync(template.RetryPolicyId, cancellationToken);

        var message = CreateMessage(rendered, channel, policy, template.Id, request.EntityName, request.RecordId, tokens);
        _db.EmailMessages.Add(message);
        await _db.SaveChangesAsync(cancellationToken);

        await _queue.EnqueueAsync(message.Id, cancellationToken);
        return message.Id;
    }

    public async Task<int> EnqueueRawAsync(RawEmailRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ToAddress))
            throw new BusinessException("Recipient address is required.");
        if (string.IsNullOrWhiteSpace(request.Subject))
            throw new BusinessException("Subject is required.");

        var channel = await ResolveChannelAsync(request.EmailChannelId, cancellationToken);
        var policy = await ResolvePolicyAsync(request.RetryPolicyId, cancellationToken);

        var rendered = new RenderedEmail
        {
            ToAddress = request.ToAddress.Trim(),
            Cc = request.Cc?.Trim(),
            Bcc = request.Bcc?.Trim(),
            Subject = request.Subject,
            BodyHtml = request.BodyHtml,
            BodyText = request.BodyText
        };

        var message = CreateMessage(rendered, channel, policy, null, null, null, null);
        _db.EmailMessages.Add(message);
        await _db.SaveChangesAsync(cancellationToken);

        await _queue.EnqueueAsync(message.Id, cancellationToken);
        return message.Id;
    }

    private static Dictionary<string, object?> BuildTokens(
        Dictionary<string, object?> record,
        Dictionary<string, object?>? additional)
    {
        var tokens = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in record)
            tokens[key] = value;

        tokens["Now"] = DateTime.Now;
        tokens["AppName"] = "MetaForge";

        if (additional != null)
        {
            foreach (var (key, value) in additional)
                tokens[key] = value;
        }

        return tokens;
    }

    private async Task<EmailChannel> ResolveChannelAsync(int? channelId, CancellationToken cancellationToken)
    {
        EmailChannel? channel = null;

        if (channelId.HasValue)
            channel = await _db.EmailChannels.FirstOrDefaultAsync(c => c.Id == channelId && c.IsActive, cancellationToken);

        channel ??= await _db.EmailChannels.FirstOrDefaultAsync(c => c.IsDefault && c.IsActive, cancellationToken)
            ?? await _db.EmailChannels.FirstOrDefaultAsync(c => c.IsActive, cancellationToken);

        return channel ?? throw new BusinessException("No active email channel is configured.");
    }

    private async Task<EmailRetryPolicy> ResolvePolicyAsync(int? policyId, CancellationToken cancellationToken)
    {
        EmailRetryPolicy? policy = null;

        if (policyId.HasValue)
            policy = await _db.EmailRetryPolicies.FirstOrDefaultAsync(p => p.Id == policyId && p.IsActive, cancellationToken);

        policy ??= await _db.EmailRetryPolicies.FirstOrDefaultAsync(p => p.IsDefault && p.IsActive, cancellationToken)
            ?? await _db.EmailRetryPolicies.FirstOrDefaultAsync(p => p.IsActive, cancellationToken);

        return policy ?? throw new BusinessException("No active email retry policy is configured.");
    }

    private static EmailMessage CreateMessage(
        RenderedEmail rendered,
        EmailChannel channel,
        EmailRetryPolicy policy,
        int? templateId,
        string? sourceEntity,
        int? sourceRecordId,
        Dictionary<string, object?>? tokens)
    {
        return new EmailMessage
        {
            EmailTemplateId = templateId,
            EmailChannelId = channel.Id,
            RetryPolicyId = policy.Id,
            ToAddress = rendered.ToAddress,
            Cc = rendered.Cc,
            Bcc = rendered.Bcc,
            Subject = rendered.Subject,
            BodyHtml = rendered.BodyHtml,
            BodyText = rendered.BodyText,
            Status = EmailStatus.Queued,
            AttemptCount = 0,
            MaxAttempts = policy.MaxAttempts,
            CreatedUtc = DateTime.UtcNow,
            SourceEntity = sourceEntity,
            SourceRecordId = sourceRecordId?.ToString(),
            ContextJson = tokens == null ? null : JsonSerializer.Serialize(tokens)
        };
    }

    private async Task<Dictionary<string, object?>> LoadRecordAsync(
        string entityName,
        int recordId,
        CancellationToken cancellationToken)
    {
        var entityType = _typeResolver.Resolve(entityName);
        var entity = await _db.FindAsync(entityType, [recordId], cancellationToken)
            ?? throw new NotFoundException($"{entityName} with id {recordId} was not found.");

        return DynamicEntityMapper.ToDictionary(entity);
    }
}
