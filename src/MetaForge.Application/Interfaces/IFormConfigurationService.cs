namespace MetaForge.Application.Interfaces;

/// <summary>
/// Admin form metadata configuration (create/edit master data &amp; transaction forms).
/// </summary>
public interface IFormConfigurationService
{
    Task<IReadOnlyList<FormConfigListItemDto>> GetAllFormsAsync(CancellationToken cancellationToken = default);

    Task<FormConfigDto?> GetFormAsync(int id, CancellationToken cancellationToken = default);

    Task<FormConfigDto?> GetFormByEntityAsync(string entityName, CancellationToken cancellationToken = default);

    Task<FormBuilderScreenDto> GetScreenAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DiscoveredEntityOptionDto>> GetDiscoveredEntitiesAsync(CancellationToken cancellationToken = default);

    Task<FormConfigDto> BuildDraftAsync(string entityName, string groupName, CancellationToken cancellationToken = default);

    Task<int> SaveFormAsync(FormConfigDto config, CancellationToken cancellationToken = default);

    Task<int> SaveScreenAsync(FormBuilderSaveDto screen, CancellationToken cancellationToken = default);

    Task DeleteFormAsync(int id, CancellationToken cancellationToken = default);
}
