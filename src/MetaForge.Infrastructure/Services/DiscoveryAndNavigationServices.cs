using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace MetaForge.Infrastructure.Services;

/// <summary>
/// EF Core model metadata discovery and auto-configuration.
/// </summary>
public class EntityMetadataDiscoveryService : IEntityMetadataDiscoveryService
{
    private readonly MetaForgeDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;

    public EntityMetadataDiscoveryService(MetaForgeDbContext dbContext, IUnitOfWork unitOfWork)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
    }

    public IReadOnlyList<EntityMetadataDto> DiscoverAll() =>
        _dbContext.Model.GetEntityTypes()
            .Where(t => FeatureDiscoveryConstants.IsFeatureEntityNamespace(t.ClrType.Namespace))
            .Select(t => MapEntity(t))
            .ToList();

    public EntityMetadataDto? Discover(string entityName) =>
        _dbContext.Model.GetEntityTypes()
            .FirstOrDefault(t => t.ClrType.Name == entityName) is { } entityType
            ? MapEntity(entityType)
            : null;

    public async Task GenerateFormConfigurationAsync(string entityName, CancellationToken cancellationToken = default)
    {
        var metadata = Discover(entityName)
            ?? throw new Shared.Exceptions.NotFoundException($"Entity '{entityName}' not found.");

        var existing = await _unitOfWork.Forms.GetByEntityNameAsync(entityName, cancellationToken);
        if (existing != null) return;

        var module = new ForgeForm
        {
            Code = entityName.ToLowerInvariant(),
            Name = entityName,
            EntityName = entityName,
            TableName = metadata.TableName,
            GroupName = "Master Data",
            FormType = FormType.Master,
            IsActive = true,
            DisplayOrder = 0,
            Fields = metadata.Properties
                .Where(p => !p.IsKey && p.Name != "Id")
                .Select((p, i) => new ForgeField
                {
                    PropertyName = p.Name,
                    Label = p.Name,
                    ControlType = InferControlType(p.ClrType, p.Name),
                    IsRequired = !p.IsNullable && !p.IsForeignKey,
                    IsVisible = true,
                    DisplayOrder = i,
                    LookupEntity = p.IsForeignKey ? p.Name.Replace("Id", "") : null
                }).ToList(),
            GridColumns = metadata.Properties
                .Where(p => !p.IsForeignKey || p.Name.EndsWith("Id"))
                .Take(6)
                .Select((p, i) => new ForgeGridColumn
                {
                    PropertyName = p.Name,
                    Label = p.Name,
                    DisplayOrder = i,
                    IsSortable = true,
                    IsSearchable = p.ClrType == "System.String"
                }).ToList(),
            Relations = metadata.Relations.Select(r => new ForgeRelation
            {
                RelationType = r.RelationType,
                ParentEntity = r.ParentEntity,
                ChildEntity = r.ChildEntity,
                ForeignKey = r.ForeignKey,
                NavigationProperty = r.NavigationProperty
            }).ToList()
        };

        await _unitOfWork.Forms.AddAsync(module, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static EntityMetadataDto MapEntity(Microsoft.EntityFrameworkCore.Metadata.IEntityType entityType)
    {
        var clrType = entityType.ClrType;
        return new EntityMetadataDto
        {
            EntityName = clrType.Name,
            TableName = entityType.GetTableName() ?? clrType.Name,
            PrimaryKey = entityType.FindPrimaryKey()?.Properties.FirstOrDefault()?.Name,
            Properties = clrType.GetProperties()
                .Where(p => !p.GetGetMethod()?.IsVirtual == true || p.PropertyType.Namespace?.StartsWith("System") == true)
                .Select(p =>
                {
                    var efProp = entityType.FindProperty(p.Name);
                    return new EntityPropertyMetadataDto
                    {
                        Name = p.Name,
                        ClrType = p.PropertyType.FullName ?? p.PropertyType.Name,
                        IsKey = efProp?.IsPrimaryKey() ?? false,
                        IsForeignKey = efProp?.IsForeignKey() ?? false,
                        MaxLength = efProp?.GetMaxLength(),
                        IsNullable = efProp?.IsNullable ?? true
                    };
                }).ToList(),
            Relations = entityType.GetForeignKeys().Select(fk =>
            {
                var isMany = fk.DeclaringEntityType.ClrType != clrType;
                return new EntityRelationMetadataDto
                {
                    RelationType = isMany ? RelationType.OneToMany : RelationType.ManyToOne,
                    ParentEntity = fk.PrincipalEntityType.ClrType.Name,
                    ChildEntity = fk.DeclaringEntityType.ClrType.Name,
                    ForeignKey = fk.Properties.First().Name,
                    NavigationProperty = fk.DependentToPrincipal?.Name
                };
            }).ToList()
        };
    }

    private static string InferControlType(string clrType, string propertyName)
    {
        if (propertyName.EndsWith("Ids", StringComparison.Ordinal) && propertyName.Length > 3)
            return ControlType.MultiSelect;
        if (propertyName.EndsWith("Id") && propertyName != "Id")
            return ControlType.Autocomplete;
        if (clrType.Contains("Boolean")) return ControlType.Checkbox;
        if (clrType.Contains("DateTime")) return ControlType.DateTime;
        if (clrType.Contains("DateOnly") || propertyName.Contains("Date")) return ControlType.Date;
        if (clrType.Contains("Int") || clrType.Contains("Decimal") || clrType.Contains("Double"))
            return ControlType.Number;
        if (propertyName.Contains("Description") || propertyName.Contains("Notes"))
            return ControlType.TextArea;
        return ControlType.TextBox;
    }
}

/// <summary>
/// Dynamic navigation menu from active modules.
/// </summary>
public class NavigationService : INavigationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IFormAuthorizationService _authorizationService;

    public NavigationService(
        IUnitOfWork unitOfWork,
        IHttpContextAccessor httpContextAccessor,
        IFormAuthorizationService authorizationService)
    {
        _unitOfWork = unitOfWork;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    public async Task<IReadOnlyList<MenuGroupDto>> GetMenuAsync(CancellationToken cancellationToken = default)
    {
        var modules = await _unitOfWork.Forms.GetActiveFormsAsync(cancellationToken);
        var user = _httpContextAccessor.HttpContext?.User;

        var visibleModules = new List<ForgeForm>();
        foreach (var module in modules)
        {
            if (user?.Identity?.IsAuthenticated != true)
                continue;

            if (await _authorizationService.HasFormPermissionAsync(user, module.Code, PermissionAction.View, cancellationToken))
                visibleModules.Add(module);
        }

        return visibleModules
            .GroupBy(m => m.GroupName ?? "General")
            .Select(g => new MenuGroupDto
            {
                GroupName = g.Key,
                Items = g.Select(m => new MenuItemDto
                {
                    Code = m.Code,
                    Name = m.Name,
                    EntityName = m.EntityName,
                    Url = $"/Modules/{m.Code}"
                }).ToList()
            })
            .Where(g => g.Items.Count > 0)
            .ToList();
    }

    public async Task<AppMenuDto> GetAppMenuAsync(CancellationToken cancellationToken = default)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var menu = new AppMenuDto();

        if (user?.Identity?.IsAuthenticated != true)
            return menu;

        menu.SystemItems.Add(new SystemMenuItemDto { Name = "Dashboard", Url = "/Home/Dashboard" });

        if (await _authorizationService.HasPermissionCodeAsync(user, Shared.Constants.ConfigPermissions.View, cancellationToken))
            menu.SystemItems.Add(new SystemMenuItemDto { Name = "Form Builder", Url = "/FormBuilder" });

        if (await _authorizationService.HasPermissionCodeAsync(user, Shared.Constants.ReportConfigPermissions.View, cancellationToken))
            menu.SystemItems.Add(new SystemMenuItemDto { Name = "Report Builder", Url = "/ReportBuilder" });

        if (await CanViewSecurityAsync(user, cancellationToken))
            menu.SystemItems.Add(new SystemMenuItemDto { Name = "Security", Url = "/Security" });

        menu.FormGroups = (await GetMenuAsync(cancellationToken)).ToList();
        return menu;
    }

    public async Task<IReadOnlyList<MenuTreeNodeDto>> GetSidebarMenuAsync(CancellationToken cancellationToken = default)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            return [];

        var flat = await _unitOfWork.Menus.GetActiveTreeAsync(cancellationToken);
        if (flat.Count == 0)
            return await BuildFallbackSidebarAsync(user, cancellationToken);

        var roots = flat
            .Where(m => m.ParentId == null)
            .OrderBy(m => m.DisplayOrder)
            .ThenBy(m => m.Name);

        var tree = new List<MenuTreeNodeDto>();
        foreach (var root in roots)
        {
            var node = await BuildTreeNodeAsync(root, flat, user, cancellationToken);
            if (node != null)
                tree.Add(node);
        }

        return tree;
    }

    private async Task<MenuTreeNodeDto?> BuildTreeNodeAsync(
        ForgeMenu menu,
        IReadOnlyList<ForgeMenu> flat,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (menu.ItemType == MenuItemType.Folder)
        {
            var children = flat
                .Where(m => m.ParentId == menu.Id)
                .OrderBy(m => m.DisplayOrder)
                .ThenBy(m => m.Name);

            var childNodes = new List<MenuTreeNodeDto>();
            foreach (var child in children)
            {
                var node = await BuildTreeNodeAsync(child, flat, user, cancellationToken);
                if (node != null)
                    childNodes.Add(node);
            }

            if (childNodes.Count == 0)
                return null;

            return new MenuTreeNodeDto
            {
                Id = menu.Id,
                Name = menu.Name,
                Icon = menu.Icon ?? "fa-folder",
                ItemType = MenuItemType.Folder,
                Children = childNodes
            };
        }

        if (!await CanViewMenuItemAsync(menu, user, cancellationToken))
            return null;

        return new MenuTreeNodeDto
        {
            Id = menu.Id,
            Name = menu.Name,
            Icon = menu.Icon ?? (menu.ItemType == MenuItemType.Form ? "fa-table" : "fa-link"),
            ItemType = menu.ItemType,
            Url = MenuUrlResolver.Resolve(menu),
            Children = []
        };
    }

    private async Task<bool> CanViewMenuItemAsync(ForgeMenu menu, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (menu.ItemType == MenuItemType.Form)
        {
            if (menu.Form == null)
                return false;
            return await _authorizationService.HasFormPermissionAsync(user, menu.Form.Code, PermissionAction.View, cancellationToken);
        }

        if (menu.ItemType != MenuItemType.Url || string.IsNullOrWhiteSpace(menu.Url))
            return false;

        var url = menu.Url.Trim();
        if (url.Contains("Dashboard", StringComparison.OrdinalIgnoreCase))
            return true;
        if (url.Contains("FormBuilder", StringComparison.OrdinalIgnoreCase) || url.Contains("ModuleConfig", StringComparison.OrdinalIgnoreCase))
            return await _authorizationService.HasPermissionCodeAsync(user, Shared.Constants.ConfigPermissions.View, cancellationToken);
        if (url.Contains("ReportBuilder", StringComparison.OrdinalIgnoreCase))
            return await _authorizationService.HasPermissionCodeAsync(user, Shared.Constants.ReportConfigPermissions.View, cancellationToken);
        if (url.Contains("/Reports/", StringComparison.OrdinalIgnoreCase))
        {
            var code = url.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            return !string.IsNullOrWhiteSpace(code)
                && await _authorizationService.HasPermissionCodeAsync(user, Shared.Constants.ReportPermissions.Run(code), cancellationToken);
        }
        if (url.Contains("/Menu", StringComparison.OrdinalIgnoreCase))
            return await _authorizationService.HasPermissionCodeAsync(user, Shared.Constants.ConfigPermissions.Manage, cancellationToken);
        if (url.Contains("Security", StringComparison.OrdinalIgnoreCase))
            return await CanViewSecurityAsync(user, cancellationToken);

        return true;
    }

    private async Task<IReadOnlyList<MenuTreeNodeDto>> BuildFallbackSidebarAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var tree = new List<MenuTreeNodeDto>
        {
            new()
            {
                Id = 0,
                Name = "Dashboard",
                Icon = "fa-gauge-high",
                ItemType = MenuItemType.Url,
                Url = "/Home/Dashboard"
            }
        };

        var moduleGroups = await GetMenuAsync(cancellationToken);
        foreach (var group in moduleGroups)
        {
            var folder = new MenuTreeNodeDto
            {
                Id = group.GroupName.GetHashCode(),
                Name = group.GroupName,
                Icon = group.GroupName.Contains("Transaction", StringComparison.OrdinalIgnoreCase) ? "fa-file-invoice" : "fa-database",
                ItemType = MenuItemType.Folder,
                Children = group.Items.Select(i => new MenuTreeNodeDto
                {
                    Id = i.Code.GetHashCode(),
                    Name = i.Name,
                    Icon = "fa-table",
                    ItemType = MenuItemType.Form,
                    Url = i.Url
                }).ToList()
            };
            if (folder.Children.Count > 0)
                tree.Add(folder);
        }

        return tree;
    }

    public async Task<IReadOnlyList<NavigationBreadcrumbDto>> GetBreadcrumbsAsync(
        string requestPath,
        string? currentPage = null,
        CancellationToken cancellationToken = default)
    {
        var targetPath = NormalizePath(requestPath);
        var sidebar = await GetSidebarMenuAsync(cancellationToken);
        var trail = new List<NavigationBreadcrumbDto>();

        if (TryFindMenuPath(sidebar, targetPath, trail, currentPage, out var matched))
            return EnsureDashboardRoot(matched);

        return EnsureDashboardRoot(await BuildFallbackPathAsync(targetPath, currentPage, cancellationToken));
    }

    private static bool TryFindMenuPath(
        IEnumerable<MenuTreeNodeDto> nodes,
        string targetPath,
        List<NavigationBreadcrumbDto> trail,
        string? currentPage,
        out IReadOnlyList<NavigationBreadcrumbDto> result)
    {
        foreach (var node in nodes)
        {
            if (node.ItemType == MenuItemType.Folder)
            {
                trail.Add(new NavigationBreadcrumbDto { Text = node.Name });
                if (TryFindMenuPath(node.Children, targetPath, trail, currentPage, out result))
                    return true;

                trail.RemoveAt(trail.Count - 1);
                continue;
            }

            var nodeUrl = NormalizePath(node.Url);
            if (string.IsNullOrEmpty(nodeUrl))
                continue;

            if (string.Equals(nodeUrl, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                trail.Add(new NavigationBreadcrumbDto
                {
                    Text = node.Name,
                    IsCurrent = string.IsNullOrWhiteSpace(currentPage)
                });
                result = FinalizeTrail(trail, currentPage, node.Url);
                return true;
            }

            if (targetPath.StartsWith(nodeUrl + "/", StringComparison.OrdinalIgnoreCase))
            {
                trail.Add(new NavigationBreadcrumbDto { Text = node.Name, Url = node.Url });
                var remainder = targetPath[(nodeUrl.Length + 1)..];
                AppendPathSegments(trail, node.Url!, remainder, currentPage);
                result = FinalizeTrail(trail, currentPage, targetPath);
                return true;
            }
        }

        result = [];
        return false;
    }

    private static void AppendPathSegments(
        List<NavigationBreadcrumbDto> trail,
        string baseUrl,
        string remainder,
        string? currentPage)
    {
        var segments = remainder.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var cumulative = NormalizePath(baseUrl);

        for (var i = 0; i < segments.Length; i++)
        {
            cumulative += "/" + segments[i];
            var isLastSegment = i == segments.Length - 1;
            var text = FormatPathSegment(segments[i]);

            if (isLastSegment && string.IsNullOrWhiteSpace(currentPage))
            {
                trail.Add(new NavigationBreadcrumbDto { Text = text, IsCurrent = true });
                return;
            }

            trail.Add(new NavigationBreadcrumbDto { Text = text, Url = cumulative });
        }
    }

    private static IReadOnlyList<NavigationBreadcrumbDto> FinalizeTrail(
        List<NavigationBreadcrumbDto> trail,
        string? currentPage,
        string? linkForPreviousCurrent = null)
    {
        if (trail.Count == 0)
            return trail;

        if (!string.IsNullOrWhiteSpace(currentPage))
        {
            var last = trail[^1];
            if (last.IsCurrent)
            {
                trail[^1] = new NavigationBreadcrumbDto
                {
                    Text = last.Text,
                    Url = linkForPreviousCurrent ?? last.Url
                };
            }

            trail.Add(new NavigationBreadcrumbDto { Text = currentPage, IsCurrent = true });
        }
        else if (!trail[^1].IsCurrent && string.IsNullOrWhiteSpace(trail[^1].Url))
        {
            trail[^1] = new NavigationBreadcrumbDto { Text = trail[^1].Text, IsCurrent = true };
        }
        else if (!trail[^1].IsCurrent)
        {
            var last = trail[^1];
            trail[^1] = new NavigationBreadcrumbDto { Text = last.Text, Url = last.Url, IsCurrent = true };
        }

        return trail;
    }

    private static IReadOnlyList<NavigationBreadcrumbDto> EnsureDashboardRoot(IReadOnlyList<NavigationBreadcrumbDto> trail)
    {
        if (trail.Count == 0)
            return trail;

        if (string.Equals(trail[0].Text, "Dashboard", StringComparison.OrdinalIgnoreCase))
            return trail;

        var list = trail.ToList();
        list.Insert(0, new NavigationBreadcrumbDto { Text = "Dashboard", Url = "/Home/Dashboard" });
        return list;
    }

    private async Task<IReadOnlyList<NavigationBreadcrumbDto>> BuildFallbackPathAsync(
        string targetPath,
        string? currentPage,
        CancellationToken cancellationToken)
    {
        var trail = new List<NavigationBreadcrumbDto>();

        if (targetPath.StartsWith("/modules/", StringComparison.OrdinalIgnoreCase))
        {
            var segments = targetPath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2)
            {
                var code = segments[1];
                var form = await _unitOfWork.Forms.GetByCodeAsync(code, cancellationToken);
                if (!string.IsNullOrWhiteSpace(form?.GroupName))
                    trail.Add(new NavigationBreadcrumbDto { Text = form.GroupName });

                var moduleUrl = $"/Modules/{code}";
                var moduleName = form?.Name ?? FormatPathSegment(code);
                if (segments.Length == 2)
                {
                    trail.Add(new NavigationBreadcrumbDto { Text = moduleName, IsCurrent = string.IsNullOrWhiteSpace(currentPage) });
                    return FinalizeTrail(trail, currentPage, moduleUrl);
                }

                trail.Add(new NavigationBreadcrumbDto { Text = moduleName, Url = moduleUrl });
                AppendPathSegments(trail, moduleUrl, string.Join('/', segments.Skip(2)), currentPage);
                return FinalizeTrail(trail, currentPage, targetPath);
            }
        }

        if (targetPath.StartsWith("/security", StringComparison.OrdinalIgnoreCase))
            return FinalizeTrail(BuildSecurityFallback(trail, targetPath), currentPage, targetPath);

        if (string.Equals(targetPath, "/formbuilder", StringComparison.OrdinalIgnoreCase)
            || targetPath.StartsWith("/formbuilder/", StringComparison.OrdinalIgnoreCase))
        {
            trail.Add(new NavigationBreadcrumbDto { Text = "Form Builder", IsCurrent = string.IsNullOrWhiteSpace(currentPage) });
            return FinalizeTrail(trail, currentPage, "/FormBuilder");
        }

        if (string.Equals(targetPath, "/reportbuilder", StringComparison.OrdinalIgnoreCase)
            || targetPath.StartsWith("/reportbuilder/", StringComparison.OrdinalIgnoreCase))
        {
            trail.Add(new NavigationBreadcrumbDto { Text = "Report Builder", IsCurrent = string.IsNullOrWhiteSpace(currentPage) });
            return FinalizeTrail(trail, currentPage, "/ReportBuilder");
        }

        if (targetPath.StartsWith("/reports/", StringComparison.OrdinalIgnoreCase))
        {
            var segments = targetPath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2)
            {
                trail.Add(new NavigationBreadcrumbDto { Text = "Reports", Url = "/ReportBuilder" });
                var reportName = currentPage ?? FormatPathSegment(segments[1]);
                trail.Add(new NavigationBreadcrumbDto { Text = reportName, IsCurrent = true });
                return FinalizeTrail(trail, currentPage, targetPath);
            }
        }

        if (string.Equals(targetPath, "/menu", StringComparison.OrdinalIgnoreCase)
            || targetPath.StartsWith("/menu/", StringComparison.OrdinalIgnoreCase))
        {
            trail.Add(new NavigationBreadcrumbDto { Text = "Menu Management", IsCurrent = string.IsNullOrWhiteSpace(currentPage) });
            return FinalizeTrail(trail, currentPage, "/Menu");
        }

        return FinalizeTrail(trail, currentPage, targetPath);
    }

    private static List<NavigationBreadcrumbDto> BuildSecurityFallback(List<NavigationBreadcrumbDto> trail, string targetPath)
    {
        if (string.Equals(targetPath, "/security", StringComparison.OrdinalIgnoreCase))
        {
            trail.Add(new NavigationBreadcrumbDto { Text = "Security", IsCurrent = true });
            return trail;
        }

        trail.Add(new NavigationBreadcrumbDto { Text = "Security", Url = "/Security" });
        AppendPathSegments(trail, "/Security", targetPath["/security/".Length..], null);
        return trail;
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "/";

        var normalized = path.Split('?', '#')[0].Trim().TrimEnd('/');
        return string.IsNullOrEmpty(normalized) ? "/" : normalized.ToLowerInvariant();
    }

    private static string FormatPathSegment(string segment) => segment.ToLowerInvariant() switch
    {
        "users" => "Users",
        "roles" => "Roles",
        "permissions" => "Permissions",
        "create" => "Create",
        "edit" => "Edit",
        "form" => "Form",
        "masterdetail" => "Entry",
        "customer" => "Customer",
        "product" => "Product",
        "supplier" => "Supplier",
        "country" => "Country",
        "salesorder" => "Sales Order",
        _ => char.ToUpperInvariant(segment[0]) + segment[1..]
    };

    private async Task<bool> CanViewSecurityAsync(ClaimsPrincipal user, CancellationToken cancellationToken) =>
        await _authorizationService.HasPermissionCodeAsync(user, Shared.Constants.SecurityPermissions.ViewUsers, cancellationToken)
        || await _authorizationService.HasPermissionCodeAsync(user, Shared.Constants.SecurityPermissions.ViewRoles, cancellationToken)
        || await _authorizationService.HasPermissionCodeAsync(user, Shared.Constants.SecurityPermissions.ViewPermissions, cancellationToken);
}
