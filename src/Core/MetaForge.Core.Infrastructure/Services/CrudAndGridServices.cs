using ClosedXML.Excel;
using MetaForge.Application.Configuration;
using MetaForge.Infrastructure.Dynamic;
using MetaForge.Modules.Abstractions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace MetaForge.Infrastructure.Services;

/// <summary>
/// Generic CRUD engine operating on any configured entity.
/// </summary>
public class GenericCrudService : IGenericCrudService
{
    private readonly IModuleDbContextResolver _contextResolver;
    private readonly IEntityTypeResolver _typeResolver;
    private readonly IFormMetadataCache _formCache;
    private readonly ILookupService _lookupService;
    private readonly IDynamicValidationService _validationService;
    private readonly IAuditService _auditService;
    private readonly IMappingAssociationService _mappingAssociationService;
    private readonly IEmailTriggerService _emailTriggerService;

    public GenericCrudService(
        IModuleDbContextResolver contextResolver,
        IEntityTypeResolver typeResolver,
        IFormMetadataCache formCache,
        ILookupService lookupService,
        IDynamicValidationService validationService,
        IAuditService auditService,
        IMappingAssociationService mappingAssociationService,
        IEmailTriggerService emailTriggerService)
    {
        _contextResolver = contextResolver;
        _typeResolver = typeResolver;
        _formCache = formCache;
        _lookupService = lookupService;
        _validationService = validationService;
        _auditService = auditService;
        _mappingAssociationService = mappingAssociationService;
        _emailTriggerService = emailTriggerService;
    }

    public async Task<PagedResult<Dictionary<string, object?>>> GetAllAsync(GridQueryRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Entity))
            throw new BusinessException("Entity name is required.");

        if (request.Page < 1) request.Page = 1;
        if (request.PageSize < 1) request.PageSize = 25;

        var entityType = _typeResolver.Resolve(request.Entity);
        var method = typeof(GenericCrudService)
            .GetMethod(nameof(GetAllTypedAsync), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(entityType);

        return await (Task<PagedResult<Dictionary<string, object?>>>)method.Invoke(this, [request, cancellationToken])!;
    }

    private async Task<PagedResult<Dictionary<string, object?>>> GetAllTypedAsync<T>(GridQueryRequest request, CancellationToken cancellationToken) where T : class
    {
        var form = await _formCache.GetByEntityNameAsync(request.Entity, cancellationToken);
        var propertyColumns = form?.GridColumns.Where(c => c.IsVisible).Select(c => c.PropertyName).ToList()
            ?? typeof(T).GetProperties().Select(p => p.Name).ToList();

        // Always include primary key — required for Edit/Delete actions in the grid
        var keyProperty = typeof(T).GetProperty("Id")?.Name ?? "Id";
        if (!propertyColumns.Any(c => string.Equals(c, keyProperty, StringComparison.OrdinalIgnoreCase)))
            propertyColumns = [keyProperty, ..propertyColumns];

        var gridColumns = form != null
            ? GridService.MapGrid(form).Columns
            : propertyColumns.Select(c => new GridColumnDefinition { PropertyName = c }).ToList();

        var searchable = form?.GridColumns.Where(c => c.IsSearchable).Select(c => c.PropertyName).ToList() ?? propertyColumns;

        IQueryable<T> query = GetContext(request.Entity).Set<T>().AsNoTracking();
        query = DynamicQueryBuilder.ApplySearch(query, request.SearchTerm, searchable);
        query = DynamicQueryBuilder.ApplyFilters(query, request.Filters);
        query = DynamicQueryBuilder.ApplySort(query, request.SortColumn, request.SortDescending);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var rows = items.Select(i => DynamicEntityMapper.ToDictionary(i, propertyColumns)).ToList();
        await GridDisplayEnricher.EnrichAsync(rows, gridColumns, _lookupService, formatTemporalColumns: false, cancellationToken);

        return new PagedResult<Dictionary<string, object?>>
        {
            Items = rows,
            TotalCount = total,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    public async Task<Dictionary<string, object?>> GetByIdAsync(string entityName, object id, CancellationToken cancellationToken = default)
    {
        var entity = await FindEntityAsync(entityName, id, cancellationToken)
            ?? throw new NotFoundException($"{entityName} with id {id} was not found.");

        var data = DynamicEntityMapper.ToDictionary(entity);
        await _mappingAssociationService.EnrichAsync(entityName, data, id, cancellationToken);
        return data;
    }

    public async Task<object> CreateAsync(string entityName, Dictionary<string, object?> data, CancellationToken cancellationToken = default)
    {
        data = DynamicEntityMapper.NormalizeDictionary(data);
        var form = await _formCache.GetByEntityNameAsync(entityName, cancellationToken);
        Dictionary<string, object?> mappingData = [];
        if (form != null)
            _mappingAssociationService.ExtractMappingFields(form, data, out mappingData);

        await _validationService.ValidateAsync(entityName, MergeForValidation(data, mappingData), cancellationToken);

        var entityType = _typeResolver.Resolve(entityName);
        var entity = DynamicEntityMapper.CreateEntity(entityType, data);

        await using var transaction = await BeginTransactionIfNeededAsync(entityName, cancellationToken);

        var dbContext = GetContext(entityName);
        dbContext.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        var masterId = entityType.GetProperty("Id")!.GetValue(entity)!;
        await _mappingAssociationService.SyncAsync(entityName, masterId, mappingData, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction != null)
            await transaction.CommitAsync(cancellationToken);

        await _lookupService.InvalidateCacheAsync(entityName, cancellationToken);

        await _auditService.LogAsync(entityName, masterId.ToString() ?? "", "Insert", null, JsonSerializer.Serialize(MergeForValidation(data, mappingData)), cancellationToken);

        if (int.TryParse(masterId.ToString(), out var createdId))
            await _emailTriggerService.TriggerAsync(entityName, createdId, EmailTriggerEvent.OnCreate, cancellationToken: cancellationToken);

        return entity;
    }

    public async Task UpdateAsync(string entityName, object id, Dictionary<string, object?> data, CancellationToken cancellationToken = default)
    {
        data = DynamicEntityMapper.NormalizeDictionary(data);
        var form = await _formCache.GetByEntityNameAsync(entityName, cancellationToken);
        Dictionary<string, object?> mappingData = [];
        if (form != null)
            _mappingAssociationService.ExtractMappingFields(form, data, out mappingData);

        await _validationService.ValidateAsync(entityName, MergeForValidation(data, mappingData), cancellationToken);

        var entity = await FindEntityAsync(entityName, id, cancellationToken)
            ?? throw new NotFoundException($"{entityName} with id {id} was not found.");

        var oldValue = JsonSerializer.Serialize(DynamicEntityMapper.ToDictionary(entity));
        DynamicEntityMapper.UpdateEntity(entity, data);

        await using var transaction = await BeginTransactionIfNeededAsync(entityName, cancellationToken);

        await _mappingAssociationService.SyncAsync(entityName, id, mappingData, cancellationToken);
        await GetContext(entityName).SaveChangesAsync(cancellationToken);
        if (transaction != null)
            await transaction.CommitAsync(cancellationToken);

        await _lookupService.InvalidateCacheAsync(entityName, cancellationToken);

        await _auditService.LogAsync(entityName, id.ToString()!, "Update", oldValue, JsonSerializer.Serialize(MergeForValidation(data, mappingData)), cancellationToken);

        if (int.TryParse(id.ToString(), out var updatedId))
            await _emailTriggerService.TriggerAsync(entityName, updatedId, EmailTriggerEvent.OnUpdate, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(string entityName, object id, CancellationToken cancellationToken = default)
    {
        var entity = await FindEntityAsync(entityName, id, cancellationToken)
            ?? throw new NotFoundException($"{entityName} with id {id} was not found.");

        var oldValue = JsonSerializer.Serialize(DynamicEntityMapper.ToDictionary(entity));

        await using var transaction = await BeginTransactionIfNeededAsync(entityName, cancellationToken);

        await _mappingAssociationService.DeleteMappingsAsync(entityName, id, cancellationToken);
        var dbContext = GetContext(entityName);
        dbContext.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction != null)
            await transaction.CommitAsync(cancellationToken);

        await _lookupService.InvalidateCacheAsync(entityName, cancellationToken);

        await _auditService.LogAsync(entityName, id.ToString()!, "Delete", oldValue, null, cancellationToken);

        if (int.TryParse(id.ToString(), out var deletedId))
            await _emailTriggerService.TriggerAsync(entityName, deletedId, EmailTriggerEvent.OnDelete, cancellationToken: cancellationToken);
    }

    private static Dictionary<string, object?> MergeForValidation(
        Dictionary<string, object?> entityData,
        Dictionary<string, object?> mappingData)
    {
        var merged = new Dictionary<string, object?>(entityData, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in mappingData)
            merged[key] = value;
        return merged;
    }

    private async Task<object?> FindEntityAsync(string entityName, object id, CancellationToken cancellationToken)
    {
        var entityType = _typeResolver.Resolve(entityName);
        var idProperty = entityType.GetProperty("Id")
            ?? throw new BusinessException($"Entity '{entityName}' does not expose an Id property.");
        var key = DynamicEntityMapper.ConvertKey(id, idProperty.PropertyType);
        return await GetContext(entityName).FindAsync(entityType, [key], cancellationToken);
    }

    private DbContext GetContext(string entityName) => _contextResolver.ResolveForEntity(entityName);

    private async Task<IDbContextTransaction?> BeginTransactionIfNeededAsync(string entityName, CancellationToken cancellationToken)
    {
        var dbContext = GetContext(entityName);
        if (!dbContext.Database.IsRelational() || dbContext.Database.CurrentTransaction != null)
            return null;

        return await dbContext.Database.BeginTransactionAsync(cancellationToken);
    }
}

/// <summary>
/// Grid configuration and export service.
/// </summary>
public class GridService : IGridService
{
    private readonly IFormMetadataCache _formCache;
    private readonly IGenericCrudService _crudService;
    private readonly ExportOptions _exportOptions;

    public GridService(
        IFormMetadataCache formCache,
        IGenericCrudService crudService,
        IOptions<ExportOptions> exportOptions)
    {
        _formCache = formCache;
        _crudService = crudService;
        _exportOptions = exportOptions.Value;
    }

    public async Task<GridDefinition?> GetGridDefinitionAsync(string formCode, CancellationToken cancellationToken = default)
    {
        var form = await _formCache.GetByCodeAsync(formCode, cancellationToken);
        if (form == null) return null;

        return MapGrid(form);
    }

    internal static GridDefinition MapGrid(Domain.Metadata.ForgeForm form) => new()
        {
            Entity = form.EntityName,
            FormCode = form.Code,
            FormName = form.Name,
            Columns = form.GridColumns
                .Where(c => c.IsVisible)
                .OrderBy(c => c.DisplayOrder)
                .Select(c => MapColumn(c, form))
                .ToList(),
            Actions = form.GridActions
                .Where(a => a.IsActive)
                .OrderBy(a => a.DisplayOrder)
                .Select(MapAction)
                .ToList()
        };

    internal static GridColumnDefinition MapColumn(Domain.Metadata.ForgeGridColumn column, Domain.Metadata.ForgeForm form)
    {
        var field = form.Fields.FirstOrDefault(f =>
            string.Equals(f.PropertyName, column.PropertyName, StringComparison.OrdinalIgnoreCase));

        var lookupEntity = field?.LookupEntity;
        if (string.IsNullOrWhiteSpace(lookupEntity)
            && column.PropertyName.EndsWith("Id", StringComparison.Ordinal)
            && !string.Equals(column.PropertyName, "Id", StringComparison.OrdinalIgnoreCase))
        {
            lookupEntity = column.PropertyName[..^2];
        }

        return new GridColumnDefinition
        {
            PropertyName = column.PropertyName,
            Label = field?.Label ?? column.Label,
            IsSortable = column.IsSortable,
            IsSearchable = column.IsSearchable,
            IsVisible = column.IsVisible,
            ControlType = field?.ControlType,
            LookupEntity = lookupEntity,
            LookupParentField = field?.LookupParentField,
            LookupFilterField = field?.LookupFilterField,
            DisplayFormat = GridDisplayFormats.ResolveGridColumnDisplayFormat(field?.ControlType)
        };
    }

    internal static GridActionDefinition MapAction(Domain.Metadata.ForgeFormAction action) => new()
    {
        Code = action.Code,
        Label = action.Label,
        Icon = action.Icon,
        Placement = action.Placement,
        HandlerType = action.HandlerType,
        HandlerTarget = action.HandlerTarget,
        HttpMethod = action.HttpMethod,
        RequestBody = action.RequestBody,
        PermissionAction = action.PermissionAction,
        ConfirmMessage = action.ConfirmMessage,
        ButtonStyle = action.ButtonStyle
    };

    public async Task<byte[]> ExportExcelAsync(string formCode, GridQueryRequest request, CancellationToken cancellationToken = default)
    {
        var grid = await GetGridDefinitionAsync(formCode, cancellationToken)
            ?? throw new NotFoundException($"Form '{formCode}' was not found.");

        request.Entity = grid.Entity;
        request.Page = 1;
        request.PageSize = Math.Min(_exportOptions.MaxExportRows, int.MaxValue);

        var data = await _crudService.GetAllAsync(request, cancellationToken);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(grid.FormName);

        for (var i = 0; i < grid.Columns.Count; i++)
            worksheet.Cell(1, i + 1).Value = grid.Columns[i].Label;

        for (var row = 0; row < data.Items.Count; row++)
        {
            for (var col = 0; col < grid.Columns.Count; col++)
            {
                var prop = grid.Columns[col].PropertyName;
                data.Items[row].TryGetValue(prop, out var value);
                worksheet.Cell(row + 2, col + 1).Value = value?.ToString() ?? "";
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<byte[]> ExportCsvAsync(string formCode, GridQueryRequest request, CancellationToken cancellationToken = default)
    {
        var grid = await GetGridDefinitionAsync(formCode, cancellationToken)
            ?? throw new NotFoundException($"Form '{formCode}' was not found.");

        request.Entity = grid.Entity;
        request.Page = 1;
        request.PageSize = Math.Min(_exportOptions.MaxExportRows, int.MaxValue);

        var data = await _crudService.GetAllAsync(request, cancellationToken);
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", grid.Columns.Select(c => $"\"{c.Label}\"")));

        foreach (var row in data.Items)
        {
            var values = grid.Columns.Select(c =>
            {
                row.TryGetValue(c.PropertyName, out var val);
                return $"\"{val?.ToString()?.Replace("\"", "\"\"") ?? ""}\"";
            });
            sb.AppendLine(string.Join(",", values));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}
