namespace MetaForge.Application.Interfaces;

/// <summary>
/// Dynamic lookup dropdown engine.
/// </summary>
public interface ILookupService
{
    Task<IReadOnlyList<LookupItemDto>> GetLookupItemsAsync(
        string entityName,
        string? filterField = null,
        string? filterValue = null,
        CancellationToken cancellationToken = default);

    Task InvalidateCacheAsync(string entityName, CancellationToken cancellationToken = default);
}
