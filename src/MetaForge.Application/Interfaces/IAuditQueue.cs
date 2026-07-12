using MetaForge.Application.DTOs;

namespace MetaForge.Application.Interfaces;

/// <summary>
/// In-process channel queue for deferred audit log writes.
/// </summary>
public interface IAuditQueue
{
    ValueTask EnqueueAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);

    IAsyncEnumerable<AuditLogEntry> DequeueAllAsync(CancellationToken cancellationToken);
}
