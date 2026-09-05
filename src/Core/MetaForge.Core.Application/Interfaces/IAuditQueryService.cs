namespace MetaForge.Application.Interfaces;

/// <summary>
/// Read-only audit log query service for compliance review.
/// </summary>
public interface IAuditQueryService
{
    Task<PagedResult<AuditLogListItemDto>> GetPagedAsync(AuditLogQuery query, CancellationToken cancellationToken = default);

    Task<AuditLogDetailDto?> GetDetailAsync(long id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditEntityOptionDto>> GetEntityOptionsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetActionOptionsAsync(CancellationToken cancellationToken = default);
}
