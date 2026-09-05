namespace MetaForge.Application.Interfaces;

/// <summary>
/// Dynamic grid configuration and data retrieval.
/// </summary>
public interface IGridService
{
    Task<GridDefinition?> GetGridDefinitionAsync(string formCode, CancellationToken cancellationToken = default);

    Task<byte[]> ExportExcelAsync(string formCode, GridQueryRequest request, CancellationToken cancellationToken = default);

    Task<byte[]> ExportCsvAsync(string formCode, GridQueryRequest request, CancellationToken cancellationToken = default);
}
