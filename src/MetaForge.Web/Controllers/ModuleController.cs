namespace MetaForge.Web.Controllers;

/// <summary>
/// Dynamic admin MVC controller for metadata-driven CRUD screens.
/// </summary>
[Authorize]
public class ModuleController : Controller
{
    private readonly IFormMetadataService _formMetadataService;
    private readonly IGridService _gridService;
    private readonly ITreeGridService _treeGridService;
    private readonly IFormAuthorizationService _authorizationService;

    public ModuleController(
        IFormMetadataService formMetadataService,
        IGridService gridService,
        ITreeGridService treeGridService,
        IFormAuthorizationService authorizationService)
    {
        _formMetadataService = formMetadataService;
        _gridService = gridService;
        _treeGridService = treeGridService;
        _authorizationService = authorizationService;
    }

    [HttpGet("/Modules/{formCode}")]
    public async Task<IActionResult> Index(string formCode, int? edit, string? @new, CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsureFormPermissionAsync(HttpContext, formCode, PermissionAction.View, cancellationToken);
        if (denied != null) return denied;

        var form = await _formMetadataService.GetFormDefinitionAsync(formCode, cancellationToken);
        if (form == null) return NotFound();

        var permissions = await _authorizationService.GetFormPermissionsAsync(User, formCode, cancellationToken);
        var isTreeMultiTable = string.Equals(form.FormType, FormType.TreeViewMultiTable.ToString(), StringComparison.OrdinalIgnoreCase);

        if (isTreeMultiTable)
        {
            var treeScreen = await _treeGridService.LoadScreenAsync(formCode, cancellationToken);
            if (treeScreen == null) return NotFound();

            var levelPermissions = new Dictionary<string, FormPermissionsDto>(StringComparer.OrdinalIgnoreCase);
            foreach (var level in treeScreen.Levels)
            {
                if (levelPermissions.ContainsKey(level.EntityName))
                    continue;

                var entityFormCode = await _authorizationService.ResolveFormCodeByEntityAsync(level.EntityName, cancellationToken);
                if (string.IsNullOrWhiteSpace(entityFormCode))
                    continue;

                levelPermissions[level.EntityName] =
                    await _authorizationService.GetFormPermissionsAsync(User, entityFormCode, cancellationToken);
            }

            ViewBag.FormCode = formCode;
            ViewBag.Form = form;
            ViewBag.TreeScreen = treeScreen;
            ViewBag.Permissions = permissions;
            ViewBag.LevelPermissions = levelPermissions;
            return View("TreeIndex");
        }

        var grid = await _gridService.GetGridDefinitionAsync(formCode, cancellationToken);
        var isTabular = string.Equals(form.FormType, FormType.MasterDetailTabular.ToString(), StringComparison.OrdinalIgnoreCase);
        var isTabbed = string.Equals(form.FormType, FormType.Tabbed.ToString(), StringComparison.OrdinalIgnoreCase);
        var hasMasterDetail = isTabular
            || string.Equals(form.FormType, FormType.MasterDetail.ToString(), StringComparison.OrdinalIgnoreCase)
            || form.Relations.Any(r => r.RelationType == RelationType.OneToMany);

        if (grid != null)
            GridDefinitionFilter.ApplyPermissions(grid, permissions);

        ViewBag.FormCode = formCode;
        ViewBag.IsTabularMasterDetail = isTabular;
        ViewBag.IsTabbedForm = isTabbed;
        ViewBag.HasMasterDetail = hasMasterDetail;
        ViewBag.Form = form;
        ViewBag.Grid = grid;
        ViewBag.Permissions = permissions;
        ViewBag.OpenMasterDetailId = edit;
        ViewBag.OpenMasterDetailNew = string.Equals(@new, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(@new, "true", StringComparison.OrdinalIgnoreCase);
        return View("Index");
    }

    [HttpGet("/Modules/{formCode}/Form/{id?}")]
    public async Task<IActionResult> Form(string formCode, int? id, CancellationToken cancellationToken)
    {
        var action = id.HasValue ? PermissionAction.Edit : PermissionAction.Create;
        var denied = await PermissionGuard.EnsureFormPermissionAsync(HttpContext, formCode, action, cancellationToken);
        if (denied != null) return denied;

        var form = await _formMetadataService.GetFormDefinitionAsync(formCode, cancellationToken);
        if (form == null) return NotFound();

        var permissions = await _authorizationService.GetFormPermissionsAsync(User, formCode, cancellationToken);

        ViewBag.FormCode = formCode;
        ViewBag.Form = form;
        ViewBag.RecordId = id;
        ViewBag.Permissions = permissions;
        return View();
    }

    [HttpGet("/Modules/{formCode}/MasterDetail/{id?}")]
    public IActionResult MasterDetail(string formCode, int? id) =>
        id.HasValue
            ? Redirect($"/Modules/{formCode}?edit={id.Value}")
            : Redirect($"/Modules/{formCode}?new=1");
}
