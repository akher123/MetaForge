namespace MetaForge.Application.Interfaces;

using MetaForge.Shared.Constants;

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

    Task<LookupSearchResultDto> SearchLookupItemsAsync(
        string entityName,
        string? search = null,
        int skip = 0,
        int take = AppConstants.DefaultLookupPageSize,
        string? filterField = null,
        string? filterValue = null,
        CancellationToken cancellationToken = default);

    Task<LookupItemDto?> GetLookupItemByValueAsync(
        string entityName,
        string value,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, string>> ResolveLookupTextsAsync(
        string entityName,
        IEnumerable<string> values,
        CancellationToken cancellationToken = default);

    Task InvalidateCacheAsync(string entityName, CancellationToken cancellationToken = default);
}
