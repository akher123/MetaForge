using MetaForge.Application.DTOs;

namespace MetaForge.Application.Interfaces;

/// <summary>
/// Persists audit log entries to durable storage.
/// </summary>
public interface IAuditLogStore
{
    Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
}
