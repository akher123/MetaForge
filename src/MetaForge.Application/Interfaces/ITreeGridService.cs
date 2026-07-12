using MetaForge.Application.Common;
using MetaForge.Application.DTOs;

namespace MetaForge.Application.Interfaces;

/// <summary>
/// Multi-table tree grid engine for TreeViewMultiTable screens.
/// </summary>
public interface ITreeGridService
{
    Task<TreeScreenDto?> LoadScreenAsync(string formCode, CancellationToken cancellationToken = default);

    Task<PagedResult<TreeNodeDto>> GetLevelDataAsync(TreeLevelQueryRequest request, CancellationToken cancellationToken = default);
}
