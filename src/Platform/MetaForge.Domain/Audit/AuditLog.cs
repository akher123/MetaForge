namespace MetaForge.Domain.Audit;

/// <summary>
/// Audit trail entry for entity changes.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }

    public string EntityName { get; set; } = string.Empty;

    public string RecordId { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string? UserName { get; set; }

    public DateTime Timestamp { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }
}
