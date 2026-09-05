namespace MetaForge.Application.Interfaces;

/// <summary>
/// Dynamic navigation menu from admin modules.
/// </summary>
public interface INavigationService
{
    Task<IReadOnlyList<MenuGroupDto>> GetMenuAsync(CancellationToken cancellationToken = default);

    Task<AppMenuDto> GetAppMenuAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MenuTreeNodeDto>> GetSidebarMenuAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NavigationBreadcrumbDto>> GetBreadcrumbsAsync(
        string requestPath,
        string? currentPage = null,
        CancellationToken cancellationToken = default);
}
