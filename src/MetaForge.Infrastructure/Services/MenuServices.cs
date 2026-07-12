namespace MetaForge.Infrastructure.Services;

/// <summary>
/// Resolves navigation URLs from menu entries.
/// </summary>
public static class MenuUrlResolver
{
    public static string Resolve(ForgeMenu menu)
    {
        if (menu.ItemType == MenuItemType.Url)
            return string.IsNullOrWhiteSpace(menu.Url) ? "#" : menu.Url.Trim();

        if (menu.ItemType == MenuItemType.Form && menu.Form != null)
        {
            var code = menu.Form.Code;
            return menu.Action switch
            {
                MenuLinkAction.Create => $"/Modules/{code}/Form",
                _ => $"/Modules/{code}"
            };
        }

        return "#";
    }

    public static string ResolveActionForForm(ForgeForm module) => MenuLinkAction.Index;
}

/// <summary>
/// CRUD for hierarchical navigation menus.
/// </summary>
public class MenuManagementService : IMenuManagementService
{
    private readonly IUnitOfWork _unitOfWork;

    public MenuManagementService(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<IReadOnlyList<MenuListItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var menus = await _unitOfWork.Menus.GetAllAsync(cancellationToken);
        return FlattenTree(BuildTree(menus), 0).ToList();
    }

    public async Task<MenuEntryDto?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var menu = await _unitOfWork.Menus.GetByIdAsync(id, cancellationToken);
        return menu == null ? null : MapToEntry(menu);
    }

    public async Task<IReadOnlyList<MenuParentOptionDto>> GetFolderOptionsAsync(int? excludeId = null, CancellationToken cancellationToken = default)
    {
        var menus = await _unitOfWork.Menus.GetAllAsync(cancellationToken);
        var folders = menus.Where(m => m.ItemType == MenuItemType.Folder).ToList();
        if (excludeId.HasValue)
            folders = folders.Where(f => f.Id != excludeId.Value && !IsDescendant(folders, excludeId.Value, f.Id)).ToList();

        return FlattenTree(BuildTree(folders), 0)
            .Select(m => new MenuParentOptionDto { Id = m.Id, Name = m.Name, Depth = m.Depth })
            .ToList();
    }

    public async Task<IReadOnlyList<MenuFormOptionDto>> GetFormOptionsAsync(CancellationToken cancellationToken = default)
    {
        var modules = await _unitOfWork.Forms.GetActiveFormsAsync(cancellationToken);
        return modules
            .Select(m => new MenuFormOptionDto { Id = m.Id, Code = m.Code, Name = m.Name })
            .ToList();
    }

    public async Task<int> SaveAsync(MenuEntryDto entry, CancellationToken cancellationToken = default)
    {
        Validate(entry);

        if (entry.ParentId.HasValue && entry.ParentId == entry.Id)
            throw new BusinessException("A menu item cannot be its own parent.");

        ForgeMenu menu;
        if (entry.Id > 0)
        {
            menu = await _unitOfWork.Menus.GetByIdTrackedAsync(entry.Id, cancellationToken)
                ?? throw new NotFoundException($"Menu {entry.Id} was not found.");
        }
        else
        {
            menu = new ForgeMenu();
            await _unitOfWork.Menus.AddAsync(menu, cancellationToken);
        }

        if (entry.ParentId.HasValue)
        {
            var parent = await _unitOfWork.Menus.GetByIdAsync(entry.ParentId.Value, cancellationToken)
                ?? throw new BusinessException("Parent menu was not found.");
            if (parent.ItemType != MenuItemType.Folder)
                throw new BusinessException("Parent must be a folder.");
            if (entry.Id > 0 && IsDescendant(await _unitOfWork.Menus.GetAllAsync(cancellationToken), entry.Id, entry.ParentId.Value))
                throw new BusinessException("Cannot assign a descendant as parent.");
        }

        menu.ParentId = entry.ParentId;
        menu.Name = entry.Name.Trim();
        menu.Icon = string.IsNullOrWhiteSpace(entry.Icon) ? null : entry.Icon.Trim();
        menu.ItemType = entry.ItemType;
        menu.FormId = entry.ItemType == MenuItemType.Form ? entry.FormId : null;
        menu.Action = entry.ItemType == MenuItemType.Form ? entry.Action ?? MenuLinkAction.Index : null;
        menu.Url = entry.ItemType == MenuItemType.Url ? entry.Url?.Trim() : null;
        menu.DisplayOrder = entry.DisplayOrder;
        menu.IsActive = entry.IsActive;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return menu.Id;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var menu = await _unitOfWork.Menus.GetByIdTrackedAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Menu {id} was not found.");

        if (await _unitOfWork.Menus.HasChildrenAsync(id, cancellationToken))
            throw new BusinessException("Remove or reassign child menu items before deleting this entry.");

        _unitOfWork.Menus.Remove(menu);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static void Validate(MenuEntryDto entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Name))
            throw new BusinessException("Menu name is required.");

        if (!MenuItemType.All.Contains(entry.ItemType))
            throw new BusinessException($"Invalid menu item type '{entry.ItemType}'.");

        if (entry.ItemType == MenuItemType.Form)
        {
            if (!entry.FormId.HasValue)
                throw new BusinessException("Module is required for module menu items.");
            if (string.IsNullOrWhiteSpace(entry.Action))
                entry.Action = MenuLinkAction.Index;
            if (entry.Action == MenuLinkAction.MasterDetail)
                entry.Action = MenuLinkAction.Index;
            if (!MenuLinkAction.All.Contains(entry.Action))
                throw new BusinessException($"Invalid menu action '{entry.Action}'.");
        }

        if (entry.ItemType == MenuItemType.Url && string.IsNullOrWhiteSpace(entry.Url))
            throw new BusinessException("URL is required for URL menu items.");
    }

    private static MenuEntryDto MapToEntry(ForgeMenu menu) => new()
    {
        Id = menu.Id,
        ParentId = menu.ParentId,
        Name = menu.Name,
        Icon = menu.Icon,
        ItemType = menu.ItemType,
        FormId = menu.FormId,
        Action = menu.Action,
        Url = menu.Url,
        DisplayOrder = menu.DisplayOrder,
        IsActive = menu.IsActive
    };

    private static List<ForgeMenu> BuildTree(IReadOnlyList<ForgeMenu> flat)
    {
        var lookup = flat.ToDictionary(m => m.Id);
        foreach (var menu in flat)
            menu.Children = flat.Where(m => m.ParentId == menu.Id).OrderBy(m => m.DisplayOrder).ThenBy(m => m.Name).ToList();
        return flat.Where(m => m.ParentId == null || !lookup.ContainsKey(m.ParentId.Value))
            .OrderBy(m => m.DisplayOrder).ThenBy(m => m.Name).ToList();
    }

    private static IEnumerable<MenuListItemDto> FlattenTree(IEnumerable<ForgeMenu> nodes, int depth)
    {
        foreach (var node in nodes)
        {
            yield return new MenuListItemDto
            {
                Id = node.Id,
                ParentId = node.ParentId,
                Name = node.Name,
                ItemType = node.ItemType,
                FormName = node.Form?.Name,
                Url = node.ItemType == MenuItemType.Url ? node.Url : MenuUrlResolver.Resolve(node),
                DisplayOrder = node.DisplayOrder,
                IsActive = node.IsActive,
                Depth = depth
            };

            foreach (var child in FlattenTree(node.Children, depth + 1))
                yield return child;
        }
    }

    private static bool IsDescendant(IReadOnlyList<ForgeMenu> flat, int ancestorId, int nodeId)
    {
        var current = flat.FirstOrDefault(m => m.Id == nodeId);
        while (current?.ParentId != null)
        {
            if (current.ParentId == ancestorId)
                return true;
            current = flat.FirstOrDefault(m => m.Id == current.ParentId);
        }

        return false;
    }
}

/// <summary>
/// Syncs module configuration with navigation menu entries.
/// </summary>
public class MenuSyncService : IMenuSyncService
{
    private readonly IUnitOfWork _unitOfWork;

    public MenuSyncService(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task SyncFormMenuAsync(ForgeForm module, CancellationToken cancellationToken = default)
    {
        var resolvedModule = module.Relations.Count > 0
            ? module
            : await _unitOfWork.Forms.GetByIdAsync(module.Id, cancellationToken) ?? module;

        if (await IsDetailOnlyFormAsync(resolvedModule, cancellationToken))
        {
            await DeactivateFormMenuAsync(resolvedModule.Id, cancellationToken);
            return;
        }

        var folder = await EnsureGroupFolderAsync(resolvedModule.GroupName ?? "General", cancellationToken);
        var existing = await _unitOfWork.Menus.GetByFormIdTrackedAsync(resolvedModule.Id, cancellationToken);
        var action = MenuUrlResolver.ResolveActionForForm(resolvedModule);

        if (existing == null)
        {
            existing = new ForgeMenu
            {
                ParentId = folder.Id,
                Name = resolvedModule.Name,
                ItemType = MenuItemType.Form,
                FormId = resolvedModule.Id,
                Action = action,
                DisplayOrder = resolvedModule.DisplayOrder,
                IsActive = resolvedModule.IsActive
            };
            await _unitOfWork.Menus.AddAsync(existing, cancellationToken);
        }
        else
        {
            // Preserve parent folder from Menu Management; only assign on first sync.
            existing.Name = resolvedModule.Name;
            existing.ItemType = MenuItemType.Form;
            existing.FormId = resolvedModule.Id;
            existing.Action = action;
            existing.DisplayOrder = resolvedModule.DisplayOrder;
            existing.IsActive = resolvedModule.IsActive;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateFormMenuAsync(int moduleId, CancellationToken cancellationToken = default)
    {
        var menu = await _unitOfWork.Menus.GetByFormIdTrackedAsync(moduleId, cancellationToken);
        if (menu == null) return;

        menu.IsActive = false;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task EnsureDefaultMenusAsync(CancellationToken cancellationToken = default)
    {
        if ((await _unitOfWork.Menus.GetAllAsync(cancellationToken)).Count > 0)
            return;

        await EnsureUrlMenuAsync(null, "Dashboard", "/Home/Dashboard", "fa-gauge-high", 0, cancellationToken);

        var modules = await _unitOfWork.Forms.GetActiveFormsAsync(cancellationToken);
        foreach (var module in modules)
            await SyncFormMenuAsync(module, cancellationToken);

        var systemFolder = await EnsureGroupFolderAsync("System", cancellationToken);
        await EnsureUrlMenuAsync(systemFolder.Id, "Form Builder", "/FormBuilder", "fa-wand-magic-sparkles", 0, cancellationToken);
        await EnsureUrlMenuAsync(systemFolder.Id, "Report Builder", "/ReportBuilder", "fa-chart-column", 1, cancellationToken);
        await EnsureUrlMenuAsync(systemFolder.Id, "Email Admin", "/EmailAdmin", "fa-envelope", 2, cancellationToken);
        await EnsureUrlMenuAsync(systemFolder.Id, "Menu Management", "/Menu", "fa-sitemap", 3, cancellationToken);
        await EnsureUrlMenuAsync(systemFolder.Id, "Security", "/Security", "fa-shield-halved", 4, cancellationToken);
    }

    public async Task EnsureSystemAdminMenusAsync(CancellationToken cancellationToken = default)
    {
        var systemFolder = await EnsureGroupFolderAsync("System", cancellationToken);
        await EnsureUrlMenuAsync(systemFolder.Id, "Form Builder", "/FormBuilder", "fa-wand-magic-sparkles", 0, cancellationToken);
        await EnsureUrlMenuAsync(systemFolder.Id, "Report Builder", "/ReportBuilder", "fa-chart-column", 1, cancellationToken);
        await EnsureUrlMenuAsync(systemFolder.Id, "Email Admin", "/EmailAdmin", "fa-envelope", 2, cancellationToken);
        await EnsureUrlMenuAsync(systemFolder.Id, "Menu Management", "/Menu", "fa-sitemap", 3, cancellationToken);
        await EnsureUrlMenuAsync(systemFolder.Id, "Security", "/Security", "fa-shield-halved", 4, cancellationToken);
    }

    private async Task<bool> IsDetailOnlyFormAsync(ForgeForm form, CancellationToken cancellationToken)
    {
        if (form.FormType == FormType.Detail)
            return true;

        var forms = await _unitOfWork.Forms.GetAllAsync(cancellationToken);
        return forms
            .SelectMany(m => m.Relations)
            .Any(r => r.RelationType.Equals(RelationType.OneToMany, StringComparison.OrdinalIgnoreCase)
                && r.ChildEntity.Equals(form.EntityName, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<ForgeMenu> EnsureGroupFolderAsync(string groupName, CancellationToken cancellationToken)
    {
        var normalized = string.IsNullOrWhiteSpace(groupName) ? "General" : groupName.Trim();
        var existing = await _unitOfWork.Menus.FindFolderByNameAsync(normalized, null, cancellationToken);
        if (existing != null)
            return existing;

        var folder = new ForgeMenu
        {
            Name = normalized,
            ItemType = MenuItemType.Folder,
            Icon = normalized.Contains("Transaction", StringComparison.OrdinalIgnoreCase) ? "fa-file-invoice" : "fa-database",
            DisplayOrder = normalized.Contains("Transaction", StringComparison.OrdinalIgnoreCase) ? 2 : 1,
            IsActive = true
        };
        await _unitOfWork.Menus.AddAsync(folder, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return folder;
    }

    private async Task EnsureUrlMenuAsync(int? parentId, string name, string url, string icon, int order, CancellationToken cancellationToken)
    {
        var menus = await _unitOfWork.Menus.GetAllAsync(cancellationToken);
        if (menus.Any(m => m.ItemType == MenuItemType.Url && m.Url == url))
            return;

        await _unitOfWork.Menus.AddAsync(new ForgeMenu
        {
            ParentId = parentId,
            Name = name,
            ItemType = MenuItemType.Url,
            Url = url,
            Icon = icon,
            DisplayOrder = order,
            IsActive = true
        }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
