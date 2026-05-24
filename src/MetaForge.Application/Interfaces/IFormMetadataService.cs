namespace MetaForge.Application.Interfaces;

/// <summary>
/// Builds form definitions from admin metadata.
/// </summary>
public interface IFormMetadataService
{
    Task<FormDefinition?> GetFormDefinitionAsync(string formCode, CancellationToken cancellationToken = default);

    Task<FormDefinition?> GetFormDefinitionByEntityAsync(string entityName, CancellationToken cancellationToken = default);

    Task InvalidateCacheAsync(string formCode, string? entityName = null, CancellationToken cancellationToken = default);
}
