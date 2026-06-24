namespace MetaForge.Application.Interfaces;

/// <summary>
/// Loads and syncs MultiSelect junction-table associations for dynamic forms.
/// </summary>
public interface IMappingAssociationService
{
    Task EnrichAsync(string entityName, Dictionary<string, object?> data, object masterId, CancellationToken cancellationToken = default);

    void ExtractMappingFields(Domain.Metadata.ForgeForm form, Dictionary<string, object?> data, out Dictionary<string, object?> mappingData);

    Task SyncAsync(string entityName, object masterId, Dictionary<string, object?> mappingData, CancellationToken cancellationToken = default);

    Task DeleteMappingsAsync(string entityName, object masterId, CancellationToken cancellationToken = default);
}
