namespace MetaForge.Application.Interfaces;

/// <summary>
/// CRUD operations for hierarchical navigation menus.
/// </summary>
public interface IMenuManagementService
{
    Task<IReadOnlyList<MenuListItemDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<MenuEntryDto?> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MenuParentOptionDto>> GetFolderOptionsAsync(int? excludeId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MenuFormOptionDto>> GetFormOptionsAsync(CancellationToken cancellationToken = default);

    Task<int> SaveAsync(MenuEntryDto entry, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
