namespace MetaForge.Application.Interfaces;

/// <summary>
/// Generic CRUD operations for any configured entity.
/// </summary>
public interface IGenericCrudService
{
    Task<PagedResult<Dictionary<string, object?>>> GetAllAsync(GridQueryRequest request, CancellationToken cancellationToken = default);

    Task<Dictionary<string, object?>> GetByIdAsync(string entityName, object id, CancellationToken cancellationToken = default);

    Task<object> CreateAsync(string entityName, Dictionary<string, object?> data, CancellationToken cancellationToken = default);

    Task UpdateAsync(string entityName, object id, Dictionary<string, object?> data, CancellationToken cancellationToken = default);

    Task DeleteAsync(string entityName, object id, CancellationToken cancellationToken = default);
}
