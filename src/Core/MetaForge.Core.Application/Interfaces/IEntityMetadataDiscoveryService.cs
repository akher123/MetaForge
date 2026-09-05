namespace MetaForge.Application.Interfaces;

/// <summary>
/// EF Core model metadata discovery.
/// </summary>
public interface IEntityMetadataDiscoveryService
{
    IReadOnlyList<EntityMetadataDto> DiscoverAll();

    EntityMetadataDto? Discover(string entityName);

    Task GenerateFormConfigurationAsync(string entityName, CancellationToken cancellationToken = default);
}
