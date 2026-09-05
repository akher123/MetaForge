namespace MetaForge.Application.Interfaces;

/// <summary>
/// Invalidates cached navigation data after menu or permission changes.
/// </summary>
public interface INavigationCacheInvalidator
{
    void InvalidateSidebarMenus();
}
