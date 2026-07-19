namespace MetaForge.Application.Interfaces;

/// <summary>
/// Master-detail screen engine.
/// </summary>
public interface IMasterDetailService
{
    Task<MasterDetailScreenDto> LoadScreenAsync(string formCode, object? masterId = null, CancellationToken cancellationToken = default);

    Task<Dictionary<string, object?>> LoadMasterAsync(string formCode, object masterId, CancellationToken cancellationToken = default);

    Task<List<Dictionary<string, object?>>> LoadDetailsAsync(string formCode, object masterId, CancellationToken cancellationToken = default);

    Task<object> SaveMasterDetailAsync(
        string formCode,
        Dictionary<string, object?> masterData,
        List<Dictionary<string, object?>>? detailData,
        IReadOnlyList<object>? deletedDetailIds = null,
        IReadOnlyList<DetailSectionSaveDto>? detailSections = null,
        CancellationToken cancellationToken = default);

    Task DeleteDetailAsync(string formCode, object detailId, CancellationToken cancellationToken = default);
}
