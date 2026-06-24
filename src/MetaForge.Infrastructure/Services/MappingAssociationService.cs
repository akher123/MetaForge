using System.Linq.Expressions;
using System.Reflection;
using MetaForge.Domain.Enums;
using MetaForge.Domain.Metadata;
using MetaForge.Infrastructure.Dynamic;

namespace MetaForge.Infrastructure.Services;

/// <summary>
/// Generic junction-table sync for MultiSelect form fields.
/// </summary>
public class MappingAssociationService : IMappingAssociationService
{
    private readonly MetaForgeDbContext _dbContext;
    private readonly IFormMetadataCache _formCache;
    private readonly IEntityTypeResolver _typeResolver;

    public MappingAssociationService(
        MetaForgeDbContext dbContext,
        IFormMetadataCache formCache,
        IEntityTypeResolver typeResolver)
    {
        _dbContext = dbContext;
        _formCache = formCache;
        _typeResolver = typeResolver;
    }

    public async Task EnrichAsync(string entityName, Dictionary<string, object?> data, object masterId, CancellationToken cancellationToken = default)
    {
        var form = await _formCache.GetByEntityNameAsync(entityName, cancellationToken);
        if (form == null)
            return;

        var masterIntId = DynamicEntityMapper.ToInt32(masterId);
        if (masterIntId <= 0)
            return;

        foreach (var field in GetMultiSelectFields(form))
        {
            var ids = await LoadRelatedIdsAsync(field, masterIntId, cancellationToken);
            data[field.PropertyName] = ids;
        }
    }

    public void ExtractMappingFields(ForgeForm form, Dictionary<string, object?> data, out Dictionary<string, object?> mappingData)
    {
        mappingData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in GetMultiSelectFields(form))
        {
            if (data.TryGetValue(field.PropertyName, out var value))
            {
                mappingData[field.PropertyName] = value;
                data.Remove(field.PropertyName);
            }
        }
    }

    public async Task SyncAsync(string entityName, object masterId, Dictionary<string, object?> mappingData, CancellationToken cancellationToken = default)
    {
        var form = await _formCache.GetByEntityNameAsync(entityName, cancellationToken);
        if (form == null)
            return;

        var masterIntId = DynamicEntityMapper.ToInt32(masterId);
        if (masterIntId <= 0)
            throw new BusinessException("Master record id is required to sync mapping associations.");

        foreach (var field in GetMultiSelectFields(form))
        {
            if (!mappingData.ContainsKey(field.PropertyName))
                continue;

            var relatedIds = DynamicEntityMapper.ToInt32List(mappingData[field.PropertyName]);
            await SyncFieldAsync(field, masterIntId, relatedIds, cancellationToken);
        }
    }

    public async Task DeleteMappingsAsync(string entityName, object masterId, CancellationToken cancellationToken = default)
    {
        var form = await _formCache.GetByEntityNameAsync(entityName, cancellationToken);
        if (form == null)
            return;

        var masterIntId = DynamicEntityMapper.ToInt32(masterId);
        if (masterIntId <= 0)
            return;

        foreach (var field in GetMultiSelectFields(form))
        {
            await RemoveExistingRowsAsync(field, masterIntId, cancellationToken);
        }
    }

    internal static IEnumerable<ForgeField> GetMultiSelectFields(ForgeForm form) =>
        form.Fields.Where(IsMultiSelectField);

    internal static bool IsMultiSelectField(ForgeField field) =>
        ControlType.IsMultiSelect(field.ControlType)
        && !string.IsNullOrWhiteSpace(field.MappingEntity)
        && !string.IsNullOrWhiteSpace(field.MappingParentKey)
        && !string.IsNullOrWhiteSpace(field.MappingRelatedKey);

    private async Task<List<int>> LoadRelatedIdsAsync(ForgeField field, int masterId, CancellationToken cancellationToken)
    {
        var mappingType = _typeResolver.Resolve(field.MappingEntity!);
        var query = GetMappingQuery(mappingType);
        query = ApplyEqualityFilter(query, mappingType, field.MappingParentKey!, masterId);
        return await SelectIntColumnAsync(query, mappingType, field.MappingRelatedKey!, cancellationToken);
    }

    private async Task SyncFieldAsync(ForgeField field, int masterId, IReadOnlyList<int> relatedIds, CancellationToken cancellationToken)
    {
        var mappingType = _typeResolver.Resolve(field.MappingEntity!);
        await RemoveExistingRowsAsync(field, masterId, cancellationToken);

        var parentProp = mappingType.GetProperty(field.MappingParentKey!, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
            ?? throw new BusinessException($"Mapping parent key '{field.MappingParentKey}' was not found on '{field.MappingEntity}'.");
        var relatedProp = mappingType.GetProperty(field.MappingRelatedKey!, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
            ?? throw new BusinessException($"Mapping related key '{field.MappingRelatedKey}' was not found on '{field.MappingEntity}'.");

        var setMethod = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!.MakeGenericMethod(mappingType);
        var dbSet = setMethod.Invoke(_dbContext, null)!;

        foreach (var relatedId in relatedIds.Distinct().Where(id => id > 0))
        {
            var row = Activator.CreateInstance(mappingType)
                ?? throw new BusinessException($"Unable to create instance of '{field.MappingEntity}'.");

            parentProp.SetValue(row, Convert.ChangeType(masterId, Nullable.GetUnderlyingType(parentProp.PropertyType) ?? parentProp.PropertyType));
            relatedProp.SetValue(row, Convert.ChangeType(relatedId, Nullable.GetUnderlyingType(relatedProp.PropertyType) ?? relatedProp.PropertyType));

            var addMethod = dbSet.GetType().GetMethod("Add")!;
            addMethod.Invoke(dbSet, [row]);
        }
    }

    private async Task RemoveExistingRowsAsync(ForgeField field, int masterId, CancellationToken cancellationToken)
    {
        var mappingType = _typeResolver.Resolve(field.MappingEntity!);
        var query = GetMappingQuery(mappingType);
        query = ApplyEqualityFilter(query, mappingType, field.MappingParentKey!, masterId);
        var existing = await ToListAsync(query, mappingType, cancellationToken);

        if (existing.Count == 0)
            return;

        _dbContext.RemoveRange(existing);
    }

    private IQueryable GetMappingQuery(Type mappingType)
    {
        var setMethod = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!.MakeGenericMethod(mappingType);
        return (IQueryable)setMethod.Invoke(_dbContext, null)!;
    }

    private static IQueryable ApplyEqualityFilter(IQueryable query, Type entityType, string propertyName, object value)
    {
        var parameter = Expression.Parameter(entityType, "e");
        var property = Expression.Property(parameter, propertyName);
        var constant = Expression.Constant(value);
        var converted = Expression.Convert(constant, property.Type);
        var equality = Expression.Equal(property, converted);
        var lambda = Expression.Lambda(equality, parameter);

        var whereMethod = typeof(Queryable).GetMethods()
            .First(m => m.Name == nameof(Queryable.Where) && m.GetParameters().Length == 2)
            .MakeGenericMethod(entityType);

        return (IQueryable)whereMethod.Invoke(null, [query, lambda])!;
    }

    private static async Task<List<int>> SelectIntColumnAsync(
        IQueryable query,
        Type entityType,
        string propertyName,
        CancellationToken cancellationToken)
    {
        var parameter = Expression.Parameter(entityType, "e");
        var property = Expression.Property(parameter, propertyName);
        var lambda = Expression.Lambda(property, parameter);

        var selectMethod = typeof(Queryable).GetMethods()
            .First(m => m.Name == nameof(Queryable.Select) && m.GetParameters().Length == 2)
            .MakeGenericMethod(entityType, property.Type);

        var projected = (IQueryable)selectMethod.Invoke(null, [query, lambda])!;

        var toListMethod = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods()
            .First(m => m.Name == nameof(EntityFrameworkQueryableExtensions.ToListAsync)
                        && m.GetParameters().Length == 2)
            .MakeGenericMethod(property.Type);

        var task = (Task)toListMethod.Invoke(null, [projected, cancellationToken])!;
        await task.ConfigureAwait(false);

        var resultProperty = task.GetType().GetProperty("Result")!;
        var values = (System.Collections.IEnumerable)resultProperty.GetValue(task)!;

        var ids = new List<int>();
        foreach (var value in values)
        {
            if (value == null) continue;
            ids.Add(DynamicEntityMapper.ToInt32(value));
        }

        return ids;
    }

    private static async Task<List<object>> ToListAsync(IQueryable query, Type entityType, CancellationToken cancellationToken)
    {
        var toListMethod = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods()
            .First(m => m.Name == nameof(EntityFrameworkQueryableExtensions.ToListAsync)
                        && m.GetParameters().Length == 2)
            .MakeGenericMethod(entityType);

        var task = (Task)toListMethod.Invoke(null, [query, cancellationToken])!;
        await task.ConfigureAwait(false);

        var resultProperty = task.GetType().GetProperty("Result")!;
        return ((System.Collections.IEnumerable)resultProperty.GetValue(task)!).Cast<object>().ToList();
    }
}
