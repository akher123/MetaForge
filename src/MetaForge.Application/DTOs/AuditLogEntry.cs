namespace MetaForge.Application.DTOs;

/// <summary>
/// In-memory audit payload enqueued for background persistence.
/// </summary>
public sealed record AuditLogEntry(
    string EntityName,
    string RecordId,
    string Action,
    string? OldValue,
    string? NewValue,
    string UserName,
    DateTime TimestampUtc);
