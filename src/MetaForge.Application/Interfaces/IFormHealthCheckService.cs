using MetaForge.Application.DTOs;

namespace MetaForge.Application.Interfaces;

/// <summary>
/// Scans configured forms and related metadata for drift, gaps, and misconfiguration.
/// </summary>
public interface IFormHealthCheckService
{
    Task<FormHealthReportDto> GetReportAsync(CancellationToken cancellationToken = default);

    Task<FormHealthItemDto?> GetFormHealthAsync(int formId, CancellationToken cancellationToken = default);
}
