namespace MetaForge.Application.Interfaces;

/// <summary>
/// Caches loaded <see cref="ForgeForm"/> graphs (fields, relations, grid columns) to avoid repeated database queries.
/// </summary>
public interface IFormMetadataCache
{
    Task<ForgeForm?> GetByCodeAsync(string formCode, CancellationToken cancellationToken = default);

    Task<ForgeForm?> GetByEntityNameAsync(string entityName, CancellationToken cancellationToken = default);

    void Invalidate(string formCode, string? entityName = null);
}
