namespace MetaForge.Infrastructure.Services;

/// <summary>
/// Query and manage outbound email message status.
/// </summary>
public class EmailMessageService : IEmailMessageService
{
    private readonly MetaForgeDbContext _db;
    private readonly IEmailQueue _queue;

    public EmailMessageService(MetaForgeDbContext db, IEmailQueue queue)
    {
        _db = db;
        _queue = queue;
    }

    public async Task<PagedResult<EmailMessageListItemDto>> GetMessagesAsync(
        EmailLogQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var q = _db.EmailMessages
            .Include(m => m.EmailTemplate)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status))
            q = q.Where(m => m.Status == query.Status);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            q = q.Where(m =>
                m.ToAddress.Contains(search)
                || m.Subject.Contains(search)
                || (m.LastError != null && m.LastError.Contains(search)));
        }

        var total = await q.CountAsync(cancellationToken);

        var items = await q
            .OrderByDescending(m => m.CreatedUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new EmailMessageListItemDto
            {
                Id = m.Id,
                TemplateCode = m.EmailTemplate != null ? m.EmailTemplate.Code : null,
                ToAddress = m.ToAddress,
                Subject = m.Subject,
                Status = m.Status,
                AttemptCount = m.AttemptCount,
                MaxAttempts = m.MaxAttempts,
                CreatedUtc = m.CreatedUtc,
                SentUtc = m.SentUtc,
                NextAttemptUtc = m.NextAttemptUtc,
                LastError = m.LastError,
                SourceEntity = m.SourceEntity,
                SourceRecordId = m.SourceRecordId
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<EmailMessageListItemDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task CancelAsync(int id, CancellationToken cancellationToken = default)
    {
        var message = await _db.EmailMessages.FindAsync([id], cancellationToken)
            ?? throw new NotFoundException($"Email message {id} was not found.");

        if (message.Status is EmailStatus.Sent or EmailStatus.Cancelled)
            throw new BusinessException($"Cannot cancel an email with status '{message.Status}'.");

        message.Status = EmailStatus.Cancelled;
        message.NextAttemptUtc = null;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> ResendAsync(int id, CancellationToken cancellationToken = default)
    {
        var original = await _db.EmailMessages.FindAsync([id], cancellationToken)
            ?? throw new NotFoundException($"Email message {id} was not found.");

        var message = new EmailMessage
        {
            EmailTemplateId = original.EmailTemplateId,
            EmailChannelId = original.EmailChannelId,
            RetryPolicyId = original.RetryPolicyId,
            ToAddress = original.ToAddress,
            Cc = original.Cc,
            Bcc = original.Bcc,
            Subject = original.Subject,
            BodyHtml = original.BodyHtml,
            BodyText = original.BodyText,
            Status = EmailStatus.Queued,
            AttemptCount = 0,
            MaxAttempts = original.MaxAttempts,
            CreatedUtc = DateTime.UtcNow,
            SourceEntity = original.SourceEntity,
            SourceRecordId = original.SourceRecordId,
            ContextJson = original.ContextJson
        };

        _db.EmailMessages.Add(message);
        await _db.SaveChangesAsync(cancellationToken);
        await _queue.EnqueueAsync(message.Id, cancellationToken);
        return message.Id;
    }
}
