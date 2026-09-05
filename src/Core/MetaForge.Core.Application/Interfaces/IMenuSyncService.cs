namespace MetaForge.Application.Interfaces;

/// <summary>
/// Keeps navigation menus in sync with form configuration.
/// </summary>
public interface IMenuSyncService
{
    Task SyncFormMenuAsync(ForgeForm form, CancellationToken cancellationToken = default);

    Task DeactivateFormMenuAsync(int formId, CancellationToken cancellationToken = default);

    Task EnsureDefaultMenusAsync(CancellationToken cancellationToken = default);

    Task EnsureSystemAdminMenusAsync(CancellationToken cancellationToken = default);

    Task EnsureAccountMenusAsync(CancellationToken cancellationToken = default);
}
