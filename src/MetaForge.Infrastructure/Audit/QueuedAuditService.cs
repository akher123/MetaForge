using MetaForge.Application.DTOs;
using MetaForge.Application.Interfaces;

namespace MetaForge.Infrastructure.Audit;

/// <summary>
/// Application-facing audit service that enqueues entries for background persistence.
/// </summary>
public sealed class QueuedAuditService : IAuditService
{
    private readonly IAuditQueue _queue;
    private readonly IAuditUserProvider _userProvider;

    public QueuedAuditService(IAuditQueue queue, IAuditUserProvider userProvider)
    {
        _queue = queue;
        _userProvider = userProvider;
    }

    public Task LogAsync(
        string entityName,
        string recordId,
        string action,
        string? oldValue,
        string? newValue,
        CancellationToken cancellationToken = default)
    {
        var entry = new AuditLogEntry(
            entityName,
            recordId,
            action,
            oldValue,
            newValue,
            _userProvider.GetCurrentUserName(),
            DateTime.UtcNow);

        return _queue.EnqueueAsync(entry, cancellationToken).AsTask();
    }
}
