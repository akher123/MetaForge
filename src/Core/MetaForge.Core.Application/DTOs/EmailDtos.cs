namespace MetaForge.Application.DTOs;

public class EmailChannelListItemDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
}

public class EmailChannelDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string? FromDisplayName { get; set; }
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public bool SmtpUseSsl { get; set; } = true;
    public string? SmtpUsername { get; set; }
    public string? CredentialSecretName { get; set; }
    public int MaxDegreeOfParallelism { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }
}

public class EmailRetryPolicyListItemDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int MaxAttempts { get; set; }
    public string BackoffStrategy { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
}

public class EmailRetryPolicyDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int MaxAttempts { get; set; } = 5;
    public string BackoffStrategy { get; set; } = string.Empty;
    public int BaseDelaySeconds { get; set; } = 60;
    public int MaxDelaySeconds { get; set; } = 3600;
    public double BackoffMultiplier { get; set; } = 2.0;
    public bool UseJitter { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }
}

public class EmailTemplateListItemDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ChannelName { get; set; }
    public int BindingCount { get; set; }
    public bool IsActive { get; set; }
}

public class EmailTemplateBindingDto
{
    public int Id { get; set; }
    public int FormId { get; set; }
    public string? FormCode { get; set; }
    public string? FormName { get; set; }
    public string TriggerEvent { get; set; } = string.Empty;
    public string? ActionCode { get; set; }
    public string? RecipientField { get; set; }
    public string? ConditionExpression { get; set; }
    public bool IsActive { get; set; } = true;
}

public class EmailTemplateDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    public string? BodyText { get; set; }
    public string? DefaultToExpression { get; set; }
    public string? DefaultCc { get; set; }
    public string? DefaultBcc { get; set; }
    public int? EmailChannelId { get; set; }
    public int? RetryPolicyId { get; set; }
    public string Culture { get; set; } = "en";
    public bool IsActive { get; set; } = true;
    public List<EmailTemplateBindingDto> Bindings { get; set; } = [];
}

public class EmailSendRequest
{
    public string TemplateCode { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public int RecordId { get; set; }
    public string? ToAddress { get; set; }
    public string? Cc { get; set; }
    public string? Bcc { get; set; }
    public Dictionary<string, object?>? AdditionalTokens { get; set; }
}

public class RawEmailRequest
{
    public string ToAddress { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    public string? BodyText { get; set; }
    public string? Cc { get; set; }
    public string? Bcc { get; set; }
    public int? EmailChannelId { get; set; }
    public int? RetryPolicyId { get; set; }
}

public class RenderedEmail
{
    public string ToAddress { get; set; } = string.Empty;
    public string? Cc { get; set; }
    public string? Bcc { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    public string? BodyText { get; set; }
}

public class EmailMessageListItemDto
{
    public int Id { get; set; }
    public string? TemplateCode { get; set; }
    public string ToAddress { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? SentUtc { get; set; }
    public DateTime? NextAttemptUtc { get; set; }
    public string? LastError { get; set; }
    public string? SourceEntity { get; set; }
    public string? SourceRecordId { get; set; }
}

public class EmailLogQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? Status { get; set; }
    public string? Search { get; set; }
}

public class FormOptionDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
}
