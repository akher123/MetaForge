using MetaForge.Domain.Notifications;

namespace MetaForge.Application.Interfaces;

public interface IEmailConfigurationService
{
    Task<IReadOnlyList<EmailChannelListItemDto>> GetChannelsAsync(CancellationToken cancellationToken = default);
    Task<EmailChannelDto?> GetChannelAsync(int id, CancellationToken cancellationToken = default);
    Task<int> SaveChannelAsync(EmailChannelDto dto, CancellationToken cancellationToken = default);
    Task DeleteChannelAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmailRetryPolicyListItemDto>> GetRetryPoliciesAsync(CancellationToken cancellationToken = default);
    Task<EmailRetryPolicyDto?> GetRetryPolicyAsync(int id, CancellationToken cancellationToken = default);
    Task<int> SaveRetryPolicyAsync(EmailRetryPolicyDto dto, CancellationToken cancellationToken = default);
    Task DeleteRetryPolicyAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmailTemplateListItemDto>> GetTemplatesAsync(CancellationToken cancellationToken = default);
    Task<EmailTemplateDto?> GetTemplateAsync(int id, CancellationToken cancellationToken = default);
    Task<int> SaveTemplateAsync(EmailTemplateDto dto, CancellationToken cancellationToken = default);
    Task DeleteTemplateAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetAvailableTokensAsync(int formId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FormOptionDto>> GetFormOptionsAsync(CancellationToken cancellationToken = default);
}

public interface IEmailDispatchService
{
    Task<int> EnqueueFromTemplateAsync(EmailSendRequest request, CancellationToken cancellationToken = default);
    Task<int> EnqueueRawAsync(RawEmailRequest request, CancellationToken cancellationToken = default);
}

public interface IEmailMessageService
{
    Task<PagedResult<EmailMessageListItemDto>> GetMessagesAsync(EmailLogQuery query, CancellationToken cancellationToken = default);
    Task CancelAsync(int id, CancellationToken cancellationToken = default);
    Task<int> ResendAsync(int id, CancellationToken cancellationToken = default);
}

public interface IEmailTemplateRenderer
{
    RenderedEmail Render(EmailTemplate template, IReadOnlyDictionary<string, object?> tokens);
    string RenderToken(string template, IReadOnlyDictionary<string, object?> tokens);
}

public interface IRetryPolicyEvaluator
{
    TimeSpan? GetNextDelay(EmailRetryPolicy policy, int attemptCount);
}

public interface IEmailQueue
{
    ValueTask EnqueueAsync(int emailMessageId, CancellationToken cancellationToken = default);
    IAsyncEnumerable<int> DequeueAllAsync(CancellationToken cancellationToken);
}

public interface IEmailChannelSender
{
    Task SendAsync(EmailMessage message, EmailChannel channel, CancellationToken cancellationToken = default);
}

public interface IEmailTriggerService
{
    Task TriggerAsync(string entityName, int recordId, string triggerEvent, string? actionCode = null, CancellationToken cancellationToken = default);
}
