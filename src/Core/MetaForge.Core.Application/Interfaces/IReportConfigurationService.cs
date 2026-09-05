namespace MetaForge.Application.Interfaces;

/// <summary>
/// Admin report metadata configuration (Report Builder).
/// </summary>
public interface IReportConfigurationService
{
    Task<IReadOnlyList<ReportConfigListItemDto>> GetAllReportsAsync(CancellationToken cancellationToken = default);

    Task<ReportConfigDto?> GetReportAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DiscoveredEntityOptionDto>> GetDiscoveredEntitiesAsync(CancellationToken cancellationToken = default);

    Task<ReportConfigDto> BuildDraftAsync(string entityName, string groupName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReportPropertyOptionDto>> GetEntityPropertyPathsAsync(string entityName, CancellationToken cancellationToken = default);

    Task<int> SaveReportAsync(ReportConfigDto config, CancellationToken cancellationToken = default);

    Task DeleteReportAsync(int id, CancellationToken cancellationToken = default);
}
