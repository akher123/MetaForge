using MetaForge.Application.Common;
using MetaForge.Infrastructure.Dynamic;
using MetaForge.Shared.Constants;

namespace MetaForge.Web.Controllers.Api;

[Authorize]
[ApiController]
[Route("api/metaforge/form-catalog")]
public class FormCatalogApiController : ControllerBase
{
    private readonly INavigationService _navigationService;
    private readonly IEntityMetadataDiscoveryService _discoveryService;
    private readonly IFormAuthorizationService _authorizationService;

    public FormCatalogApiController(
        INavigationService navigationService,
        IEntityMetadataDiscoveryService discoveryService,
        IFormAuthorizationService authorizationService)
    {
        _navigationService = navigationService;
        _discoveryService = discoveryService;
        _authorizationService = authorizationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMenu(CancellationToken cancellationToken) =>
        Ok(await _navigationService.GetMenuAsync(cancellationToken));

    [HttpGet("{formCode}/permissions")]
    public async Task<IActionResult> GetFormPermissions(string formCode, CancellationToken cancellationToken) =>
        Ok(await _authorizationService.GetFormPermissionsAsync(User, formCode, cancellationToken));

    [HttpGet("discover")]
    public IActionResult Discover() => Ok(_discoveryService.DiscoverAll());

    [HttpPost("discover/{entityName}")]
    public async Task<IActionResult> GenerateForm(string entityName, CancellationToken cancellationToken)
    {
        await _discoveryService.GenerateFormConfigurationAsync(entityName, cancellationToken);
        return Ok(new { message = $"Form generated for {entityName}" });
    }
}

[Authorize]
[ApiController]
[Route("api/metaforge/forms")]
public class FormDefinitionsApiController : ControllerBase
{
    private readonly IFormMetadataService _formMetadataService;

    public FormDefinitionsApiController(IFormMetadataService formMetadataService) => _formMetadataService = formMetadataService;

    [HttpGet("{formCode}")]
    [RequireFormPermission(PermissionAction.View)]
    public async Task<IActionResult> GetForm(string formCode, CancellationToken cancellationToken)
    {
        var form = await _formMetadataService.GetFormDefinitionAsync(formCode, cancellationToken);
        if (form == null) return NotFound();

        Response.Headers.CacheControl = "private, max-age=300";
        return Ok(form);
    }
}

[Authorize]
[ApiController]
[Route("api/metaforge/grid")]
public class GridApiController : ControllerBase
{
    private readonly IGridService _gridService;
    private readonly IGenericCrudService _crudService;
    private readonly IFormAuthorizationService _authorizationService;

    public GridApiController(
        IGridService gridService,
        IGenericCrudService crudService,
        IFormAuthorizationService authorizationService)
    {
        _gridService = gridService;
        _crudService = crudService;
        _authorizationService = authorizationService;
    }

    [HttpGet("{formCode}")]
    [RequireFormPermission(PermissionAction.View)]
    public async Task<IActionResult> GetDefinition(string formCode, CancellationToken cancellationToken)
    {
        var grid = await _gridService.GetGridDefinitionAsync(formCode, cancellationToken);
        if (grid == null) return NotFound();

        Response.Headers.CacheControl = "private, max-age=300";
        return Ok(grid);
    }

    [HttpPost("data")]
    public async Task<IActionResult> GetData([FromBody] GridQueryRequest? request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Entity))
            return BadRequest(new { error = "Entity name is required." });

        var formCode = await _authorizationService.ResolveFormCodeByEntityAsync(request.Entity, cancellationToken);
        if (string.IsNullOrWhiteSpace(formCode))
            return BadRequest(new { error = $"No form configured for entity '{request.Entity}'." });

        var denied = await PermissionGuard.EnsureFormPermissionAsync(HttpContext, formCode, PermissionAction.View, cancellationToken);
        if (denied != null) return denied;

        return Ok(await _crudService.GetAllAsync(request, cancellationToken));
    }

    [HttpGet("{formCode}/export/excel")]
    [RequireFormPermission(PermissionAction.Export)]
    public async Task<IActionResult> ExportExcel(string formCode, [FromQuery] GridQueryRequest request, CancellationToken cancellationToken)
    {
        var bytes = await _gridService.ExportExcelAsync(formCode, request, cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{formCode}.xlsx");
    }

    [HttpGet("{formCode}/export/csv")]
    [RequireFormPermission(PermissionAction.Export)]
    public async Task<IActionResult> ExportCsv(string formCode, [FromQuery] GridQueryRequest request, CancellationToken cancellationToken)
    {
        var bytes = await _gridService.ExportCsvAsync(formCode, request, cancellationToken);
        return File(bytes, "text/csv", $"{formCode}.csv");
    }
}

[Authorize]
[ApiController]
[Route("api/metaforge/crud")]
public class CrudApiController : ControllerBase
{
    private readonly IGenericCrudService _crudService;

    public CrudApiController(IGenericCrudService crudService) => _crudService = crudService;

    [HttpGet("{entity}/{id}")]
    public async Task<IActionResult> GetById(string entity, int id, CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsureEntityPermissionAsync(HttpContext, entity, PermissionAction.View, cancellationToken);
        if (denied != null) return denied;

        return Ok(await _crudService.GetByIdAsync(entity, id, cancellationToken));
    }

    [HttpPost("{entity}")]
    public async Task<IActionResult> Create(string entity, [FromBody] Dictionary<string, object?> data, CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsureEntityPermissionAsync(HttpContext, entity, PermissionAction.Create, cancellationToken);
        if (denied != null) return denied;

        var result = await _crudService.CreateAsync(entity, data, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{entity}/{id}")]
    public async Task<IActionResult> Update(string entity, int id, [FromBody] Dictionary<string, object?> data, CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsureEntityPermissionAsync(HttpContext, entity, PermissionAction.Edit, cancellationToken);
        if (denied != null) return denied;

        await _crudService.UpdateAsync(entity, id, data, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{entity}/{id}")]
    public async Task<IActionResult> Delete(string entity, int id, CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsureEntityPermissionAsync(HttpContext, entity, PermissionAction.Delete, cancellationToken);
        if (denied != null) return denied;

        await _crudService.DeleteAsync(entity, id, cancellationToken);
        return NoContent();
    }
}

[Authorize]
[ApiController]
[Route("api/metaforge/masterdetail")]
public class MasterDetailApiController : ControllerBase
{
    private readonly IMasterDetailService _masterDetailService;

    public MasterDetailApiController(IMasterDetailService masterDetailService) => _masterDetailService = masterDetailService;

    [HttpGet("{formCode}/{id?}")]
    [RequireFormPermission(PermissionAction.View)]
    public async Task<IActionResult> LoadScreen(string formCode, int? id, CancellationToken cancellationToken) =>
        Ok(await _masterDetailService.LoadScreenAsync(formCode, id, cancellationToken));

    [HttpPost("{formCode}")]
    public async Task<IActionResult> Save(string formCode, [FromBody] MasterDetailSaveRequest request, CancellationToken cancellationToken)
    {
        var action = request.Master?.ContainsKey("Id") == true &&
                     request.Master["Id"] != null &&
                     DynamicEntityMapper.ToInt32(request.Master["Id"]) > 0
            ? PermissionAction.Edit
            : PermissionAction.Create;

        var denied = await PermissionGuard.EnsureFormPermissionAsync(HttpContext, formCode, action, cancellationToken);
        if (denied != null) return denied;

        if (request.Master == null)
            return BadRequest(new { error = "Master data is required." });

        var id = await _masterDetailService.SaveMasterDetailAsync(
            formCode,
            request.Master,
            request.Details,
            request.DeletedDetailIds,
            request.DetailSections,
            cancellationToken);
        return Ok(new { id });
    }

    [HttpDelete("{formCode}/detail/{detailId}")]
    [RequireFormPermission(PermissionAction.Delete)]
    public async Task<IActionResult> DeleteDetail(string formCode, int detailId, CancellationToken cancellationToken)
    {
        await _masterDetailService.DeleteDetailAsync(formCode, detailId, cancellationToken);
        return NoContent();
    }
}

[Authorize]
[ApiController]
[Route("api/metaforge/lookups")]
public class LookupsApiController : ControllerBase
{
    private readonly ILookupService _lookupService;

    public LookupsApiController(ILookupService lookupService) => _lookupService = lookupService;

    [HttpGet("{entityName}")]
    public async Task<IActionResult> Get(
        string entityName,
        [FromQuery] string? filterField,
        [FromQuery] string? filterValue,
        [FromQuery] string? filter,
        CancellationToken cancellationToken)
    {
        var denied = await PermissionGuard.EnsureLookupAccessAsync(HttpContext, entityName, cancellationToken);
        if (denied != null) return denied;

        if (!string.IsNullOrWhiteSpace(filter) && string.IsNullOrWhiteSpace(filterField))
        {
            var parts = filter.Split('=', 2);
            if (parts.Length == 2)
            {
                filterField = parts[0].Trim();
                filterValue = parts[1].Trim().Trim('\'');
            }
        }

        Response.Headers.CacheControl = "no-store, no-cache";
        Response.Headers.Pragma = "no-cache";

        return Ok(await _lookupService.GetLookupItemsAsync(entityName, filterField, filterValue, cancellationToken));
    }

    [HttpGet("{entityName}/search")]
    public async Task<IActionResult> Search(
        string entityName,
        [FromQuery] string? search,
        [FromQuery] int skip = 0,
        [FromQuery] int take = AppConstants.DefaultLookupPageSize,
        [FromQuery] string? filterField = null,
        [FromQuery] string? filterValue = null,
        CancellationToken cancellationToken = default)
    {
        var denied = await PermissionGuard.EnsureLookupAccessAsync(HttpContext, entityName, cancellationToken);
        if (denied != null) return denied;

        Response.Headers.CacheControl = "no-store, no-cache";
        Response.Headers.Pragma = "no-cache";

        return Ok(await _lookupService.SearchLookupItemsAsync(
            entityName,
            search,
            skip,
            take,
            filterField,
            filterValue,
            cancellationToken));
    }

    [HttpGet("{entityName}/item/{value}")]
    public async Task<IActionResult> GetItem(
        string entityName,
        string value,
        CancellationToken cancellationToken = default)
    {
        var denied = await PermissionGuard.EnsureLookupAccessAsync(HttpContext, entityName, cancellationToken);
        if (denied != null) return denied;

        Response.Headers.CacheControl = "no-store, no-cache";
        Response.Headers.Pragma = "no-cache";

        var item = await _lookupService.GetLookupItemByValueAsync(entityName, value, cancellationToken);
        return item == null ? NotFound() : Ok(item);
    }
}

public class MasterDetailSaveRequest
{
    public Dictionary<string, object?> Master { get; set; } = [];

    public List<Dictionary<string, object?>>? Details { get; set; }

    public List<int>? DeletedDetailIds { get; set; }

    public List<DetailSectionSaveDto>? DetailSections { get; set; }
}
