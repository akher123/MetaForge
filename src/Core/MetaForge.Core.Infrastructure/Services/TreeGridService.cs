using System.Linq.Expressions;
using System.Reflection;
using MetaForge.Application.Common;
using MetaForge.Application.DTOs;
using MetaForge.Infrastructure.Dynamic;
using MetaForge.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace MetaForge.Infrastructure.Services;

/// <summary>
/// Multi-table tree grid engine for TreeViewMultiTable screens.
/// </summary>
public class TreeGridService : ITreeGridService
{
    private readonly IFormMetadataCache _formCache;
    private readonly MetaForgeDbContext _dbContext;
    private readonly IEntityTypeResolver _typeResolver;
    private readonly ILookupService _lookupService;

    public TreeGridService(
        IFormMetadataCache formCache,
        MetaForgeDbContext dbContext,
        IEntityTypeResolver typeResolver,
        ILookupService lookupService)
    {
        _formCache = formCache;
        _dbContext = dbContext;
        _typeResolver = typeResolver;
        _lookupService = lookupService;
    }

    public async Task<TreeScreenDto?> LoadScreenAsync(string formCode, CancellationToken cancellationToken = default)
    {
        var form = await _formCache.GetByCodeAsync(formCode, cancellationToken);
        if (form == null || form.FormType != FormType.TreeViewMultiTable)
            return null;

        var levels = await _dbContext.ForgeTreeLevels
            .AsNoTracking()
            .Where(t => t.FormId == form.Id)
            .OrderBy(t => t.LevelIndex)
            .ToListAsync(cancellationToken);
        if (levels.Count == 0)
            return null;

        var screen = new TreeScreenDto
        {
            FormCode = form.Code,
            FormName = form.Name
        };

        foreach (var level in levels)
        {
            var levelForm = await ResolveLevelFormAsync(form, level, cancellationToken);
            var grid = levelForm != null ? GridService.MapGrid(levelForm) : new GridDefinition { Entity = level.EntityName };

            screen.Levels.Add(new TreeLevelDefinitionDto
            {
                LevelIndex = level.LevelIndex,
                EntityName = level.EntityName,
                ParentEntity = level.ParentEntity,
                ForeignKey = level.ForeignKey,
                DisplayColumn = level.DisplayColumn,
                DisplayColumns = TreeDisplayColumnParser.BuildColumns(level.DisplayColumn, grid.Columns),
                Form = levelForm != null
                    ? FormMetadataService.MapForm(levelForm)
                    : new FormDefinition { EntityName = level.EntityName, FormName = level.EntityName },
                Grid = grid
            });
        }

        return screen;
    }

    public async Task<PagedResult<TreeNodeDto>> GetLevelDataAsync(TreeLevelQueryRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FormCode))
            throw new BusinessException("Form code is required.");

        if (request.Page < 1) request.Page = 1;
        if (request.PageSize < 1) request.PageSize = 25;

        var form = await _formCache.GetByCodeAsync(request.FormCode, cancellationToken)
            ?? throw new NotFoundException($"Form '{request.FormCode}' was not found.");

        if (form.FormType != FormType.TreeViewMultiTable)
            throw new BusinessException($"Form '{request.FormCode}' is not a multi-table tree screen.");

        var levels = await _dbContext.ForgeTreeLevels
            .AsNoTracking()
            .Where(t => t.FormId == form.Id)
            .OrderBy(t => t.LevelIndex)
            .ToListAsync(cancellationToken);
        var level = levels.FirstOrDefault(l => l.LevelIndex == request.LevelIndex)
            ?? throw new BusinessException($"Tree level {request.LevelIndex} was not found.");

        if (level.LevelIndex > 0 && !request.ParentId.HasValue && string.IsNullOrWhiteSpace(request.SearchTerm))
            throw new BusinessException("ParentId is required for non-root tree levels unless searching.");

        var entityType = _typeResolver.Resolve(level.EntityName);
        var method = typeof(TreeGridService)
            .GetMethod(nameof(GetLevelDataTypedAsync), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(entityType);

        return await (Task<PagedResult<TreeNodeDto>>)method.Invoke(this, [request, level, levels, cancellationToken])!;
    }

    private async Task<PagedResult<TreeNodeDto>> GetLevelDataTypedAsync<T>(
        TreeLevelQueryRequest request,
        ForgeTreeLevel level,
        List<ForgeTreeLevel> levels,
        CancellationToken cancellationToken) where T : class
    {
        var levelForm = await _formCache.GetByEntityNameAsync(level.EntityName, cancellationToken);
        var propertyColumns = levelForm?.GridColumns.Where(c => c.IsVisible).Select(c => c.PropertyName).ToList()
            ?? typeof(T).GetProperties().Select(p => p.Name).ToList();

        var keyProperty = typeof(T).GetProperty("Id")?.Name ?? "Id";
        if (!propertyColumns.Any(c => string.Equals(c, keyProperty, StringComparison.OrdinalIgnoreCase)))
            propertyColumns = [keyProperty, ..propertyColumns];

        var displayColumns = TreeDisplayColumnParser.ParseProperties(level.DisplayColumn);
        foreach (var column in displayColumns)
        {
            if (!propertyColumns.Any(c => string.Equals(c, column, StringComparison.OrdinalIgnoreCase)))
                propertyColumns = [..propertyColumns, column];
        }

        var gridColumns = levelForm != null
            ? GridService.MapGrid(levelForm).Columns
            : propertyColumns.Select(c => new GridColumnDefinition { PropertyName = c }).ToList();

        var searchable = levelForm?.GridColumns.Where(c => c.IsSearchable).Select(c => c.PropertyName).ToList()
            ?? propertyColumns;

        IQueryable<T> query = _dbContext.Set<T>().AsNoTracking();

        if (level.LevelIndex > 0 && !string.IsNullOrWhiteSpace(level.ForeignKey) && request.ParentId.HasValue)
        {
            query = DynamicQueryBuilder.ApplyFilters(query, new Dictionary<string, string>
            {
                [$"{level.ForeignKey}__eq"] = request.ParentId.Value.ToString()!
            });
        }

        query = DynamicQueryBuilder.ApplySearch(query, request.SearchTerm, searchable);
        query = DynamicQueryBuilder.ApplySort(query, request.SortColumn, request.SortDescending);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var rows = items.Select(i => DynamicEntityMapper.ToDictionary(i, propertyColumns)).ToList();
        await GridDisplayEnricher.EnrichAsync(rows, gridColumns, _lookupService, formatTemporalColumns: false, cancellationToken);

        var nextLevel = levels.FirstOrDefault(l => l.LevelIndex == level.LevelIndex + 1);
        var parentIds = rows
            .Select(row => Convert.ToInt32(row[keyProperty] ?? row["Id"] ?? 0))
            .ToList();

        var parentsWithChildren = nextLevel != null && parentIds.Count > 0
            ? await GetParentIdsWithChildrenAsync(nextLevel, parentIds, cancellationToken)
            : [];

        var nodes = new List<TreeNodeDto>();

        foreach (var row in rows)
        {
            var id = Convert.ToInt32(row[keyProperty] ?? row["Id"] ?? 0);
            var label = TreeDisplayColumnParser.BuildLabel(row, displayColumns, id);

            var hasChildren = nextLevel != null && parentsWithChildren.Contains(id);

            nodes.Add(new TreeNodeDto
            {
                LevelIndex = level.LevelIndex,
                EntityName = level.EntityName,
                Id = id,
                Label = label,
                HasChildren = hasChildren,
                ParentId = request.ParentId,
                Data = row
            });
        }

        return new PagedResult<TreeNodeDto>
        {
            Items = nodes,
            TotalCount = total,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    private async Task<HashSet<int>> GetParentIdsWithChildrenAsync(
        ForgeTreeLevel childLevel,
        IReadOnlyList<int> parentIds,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(childLevel.ForeignKey) || parentIds.Count == 0)
            return [];

        var entityType = _typeResolver.Resolve(childLevel.EntityName);
        var method = typeof(TreeGridService)
            .GetMethod(nameof(GetParentIdsWithChildrenTypedAsync), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(entityType);

        return await (Task<HashSet<int>>)method.Invoke(this, [childLevel.ForeignKey, parentIds, cancellationToken])!;
    }

    private async Task<HashSet<int>> GetParentIdsWithChildrenTypedAsync<T>(
        string foreignKey,
        IReadOnlyList<int> parentIds,
        CancellationToken cancellationToken) where T : class
    {
        var fkProperty = typeof(T).GetProperty(foreignKey, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (fkProperty == null)
            return [];

        var fkType = Nullable.GetUnderlyingType(fkProperty.PropertyType) ?? fkProperty.PropertyType;
        var typedIds = parentIds
            .Select(id => Convert.ChangeType(id, fkType))
            .Distinct()
            .ToList();

        if (typedIds.Count == 0)
            return [];

        var parameter = Expression.Parameter(typeof(T), "e");
        var propertyAccess = Expression.Property(parameter, fkProperty);
        var containsMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
            .MakeGenericMethod(fkType);
        var idsConstant = Expression.Constant(typedIds);
        var containsCall = Expression.Call(containsMethod, idsConstant, propertyAccess);
        var lambda = Expression.Lambda<Func<T, bool>>(containsCall, parameter);

        var matchingValues = await _dbContext.Set<T>()
            .AsNoTracking()
            .Where(lambda)
            .Select(e => EF.Property<object>(e, fkProperty.Name))
            .Distinct()
            .ToListAsync(cancellationToken);

        return matchingValues
            .Select(value => Convert.ToInt32(value))
            .ToHashSet();
    }

    private async Task<bool> HasChildrenAsync(ForgeTreeLevel childLevel, int parentId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(childLevel.ForeignKey))
            return false;

        var entityType = _typeResolver.Resolve(childLevel.EntityName);
        var method = typeof(TreeGridService)
            .GetMethod(nameof(HasChildrenTypedAsync), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(entityType);

        return await (Task<bool>)method.Invoke(this, [childLevel.ForeignKey, parentId, cancellationToken])!;
    }

    private async Task<bool> HasChildrenTypedAsync<T>(string foreignKey, int parentId, CancellationToken cancellationToken) where T : class
    {
        var query = _dbContext.Set<T>().AsNoTracking();
        query = DynamicQueryBuilder.ApplyFilters(query, new Dictionary<string, string>
        {
            [$"{foreignKey}__eq"] = parentId.ToString()
        });
        return await query.AnyAsync(cancellationToken);
    }

    private async Task<ForgeForm?> ResolveLevelFormAsync(
        ForgeForm screenForm,
        ForgeTreeLevel level,
        CancellationToken cancellationToken)
    {
        if (level.LevelIndex == 0)
            return screenForm;

        return await _formCache.GetByEntityNameAsync(level.EntityName, cancellationToken);
    }
}
