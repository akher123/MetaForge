namespace MetaForge.Application.Interfaces;

/// <summary>
/// Executes configured dynamic reports at runtime.
/// </summary>
public interface IReportService
{
    Task<ReportDefinitionDto?> GetDefinitionAsync(string reportCode, CancellationToken cancellationToken = default);

    Task<ReportResultDto> ExecuteAsync(
        string reportCode,
        ReportQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportExcelAsync(string reportCode, ReportQueryRequest request, CancellationToken cancellationToken = default);

    Task<byte[]> ExportPdfAsync(string reportCode, ReportQueryRequest request, CancellationToken cancellationToken = default);
}
