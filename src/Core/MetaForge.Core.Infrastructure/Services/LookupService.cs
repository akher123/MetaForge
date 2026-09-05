using System.Linq.Expressions;
using MetaForge.Infrastructure.Dynamic;
using MetaForge.Modules.Abstractions;
using MetaForge.Shared.Constants;
using Microsoft.Extensions.Caching.Memory;

namespace MetaForge.Infrastructure.Services;

/// <summary>
/// Configuration-driven lookup dropdown engine with optional parent-field filtering.
/// </summary>
public class LookupService : ILookupService
{
    private readonly MetaForgeDbContext _dbContext;
    private readonly IModuleDbContextResolver _contextResolver;
    private readonly IEntityTypeResolver _typeResolver;
    private readonly IMemoryCache _cache;

    public LookupService(
        MetaForgeDbContext dbContext,
        IModuleDbContextResolver contextResolver,
        IEntityTypeResolver typeResolver,
        IMemoryCache cache)
    {
        _dbContext = dbContext;
        _contextResolver = contextResolver;
        _typeResolver = typeResolver;
        _cache = cache;
    }

    public async Task<IReadOnlyList<LookupItemDto>> GetLookupItemsAsync(
        string entityName,
        string? filterField = null,
        string? filterValue = null,
        CancellationToken cancellationToken = default)
    {
        EnsureBusinessLookupEntity(entityName);
        var canonicalEntity = GetCanonicalEntityName(entityName);
        var version = GetCacheVersion(canonicalEntity);
        var cacheKey =
            $"{AppConstants.LookupCacheKeyPrefix}{canonicalEntity}:v{version}:{filterField ?? "all"}:{filterValue ?? "all"}";

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
            return await LoadLookupItemsAsync(canonicalEntity, filterField, filterValue, cancellationToken);
        }) ?? [];
    }

    public async Task<LookupSearchResultDto> SearchLookupItemsAsync(
        string entityName,
        string? search = null,
        int skip = 0,
        int take = AppConstants.DefaultLookupPageSize,
        string? filterField = null,
        string? filterValue = null,
        CancellationToken cancellationToken = default)
    {
        EnsureBusinessLookupEntity(entityName);
        var canonicalEntity = GetCanonicalEntityName(entityName);
        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, AppConstants.MaxLookupListSize);

        return await LoadLookupSearchAsync(
            canonicalEntity,
            search,
            skip,
            take,
            filterField,
            filterValue,
            cancellationToken);
    }

    public async Task<LookupItemDto?> GetLookupItemByValueAsync(
        string entityName,
        string value,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        EnsureBusinessLookupEntity(entityName);
        var canonicalEntity = GetCanonicalEntityName(entityName);
        var cacheKey = $"{AppConstants.LookupCacheKeyPrefix}{canonicalEntity}:item:{value}";

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
            return await LoadLookupItemByValueAsync(canonicalEntity, value, cancellationToken);
        });
    }

    public async Task<IReadOnlyList<LookupItemDto>> GetLookupItemsByValuesAsync(
        string entityName,
        IEnumerable<string> values,
        CancellationToken cancellationToken = default)
    {
        EnsureBusinessLookupEntity(entityName);
        var canonicalEntity = GetCanonicalEntityName(entityName);
        var distinctValues = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinctValues.Count == 0)
            return Array.Empty<LookupItemDto>();

        var resolved = new Dictionary<string, LookupItemDto>(StringComparer.OrdinalIgnoreCase);
        var missing = new List<string>();

        foreach (var value in distinctValues)
        {
            var cacheKey = $"{AppConstants.LookupCacheKeyPrefix}{canonicalEntity}:item:{value}";
            if (_cache.TryGetValue(cacheKey, out LookupItemDto? cached) && cached != null)
                resolved[value] = cached;
            else
                missing.Add(value);
        }

        if (missing.Count > 0)
        {
            var loaded = await LoadLookupItemsByValuesAsync(canonicalEntity, missing, cancellationToken);
            foreach (var item in loaded)
            {
                resolved[item.Value] = item;
                _cache.Set(
                    $"{AppConstants.LookupCacheKeyPrefix}{canonicalEntity}:item:{item.Value}",
                    item,
                    TimeSpan.FromMinutes(15));
            }
        }

        return distinctValues
            .Select(value => resolved.TryGetValue(value, out var item)
                ? item
                : new LookupItemDto { Value = value, Text = value })
            .ToList();
    }

    public async Task<IReadOnlyDictionary<string, string>> ResolveLookupTextsAsync(
        string entityName,
        IEnumerable<string> values,
        CancellationToken cancellationToken = default)
    {
        var items = await GetLookupItemsByValuesAsync(entityName, values, cancellationToken);
        return items
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .ToDictionary(item => item.Value, item => item.Text, StringComparer.OrdinalIgnoreCase);
    }

    public Task InvalidateCacheAsync(string entityName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entityName))
            return Task.CompletedTask;

        var canonicalEntity = GetCanonicalEntityName(entityName);
        BumpCacheVersion(canonicalEntity);
        return Task.CompletedTask;
    }

    private string GetCanonicalEntityName(string entityName) =>
        _typeResolver.Resolve(entityName).Name;

    private void EnsureBusinessLookupEntity(string entityName)
    {
        if (!_typeResolver.IsBusinessEntity(entityName))
            throw new BusinessException($"Lookups are not available for entity '{entityName}'.");
    }

    private static string VersionKey(string entityName) =>
        $"{AppConstants.LookupCacheKeyPrefix}version:{entityName}";

    private int GetCacheVersion(string entityName) =>
        _cache.GetOrCreate(VersionKey(entityName), entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromDays(7);
            return 0;
        });

    private void BumpCacheVersion(string entityName)
    {
        var key = VersionKey(entityName);
        var current = _cache.TryGetValue(key, out int version) ? version : 0;
        _cache.Set(key, current + 1, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromDays(7)
        });
    }

    private async Task<IReadOnlyList<LookupItemDto>> LoadLookupItemsAsync(
        string entityName,
        string? filterField,
        string? filterValue,
        CancellationToken cancellationToken)
    {
        var config = await _dbContext.LookupConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.EntityName == entityName && c.IsActive, cancellationToken);

        var entityType = _typeResolver.Resolve(entityName);
        var valueField = LookupFieldResolver.ResolveValueField(entityType, config?.ValueField);
        var display = LookupDisplayExpression.Create(entityType, config?.TextField);

        var query = BuildBaseQuery(entityName, entityType, display);

        if (!string.IsNullOrWhiteSpace(filterField) && !string.IsNullOrWhiteSpace(filterValue))
        {
            query = ApplyDynamicFilter(query, entityType, filterField.Trim(), filterValue.Trim());
        }
        else if (!string.IsNullOrWhiteSpace(config?.FilterExpression))
        {
            query = ApplyStaticFilter(query, entityType, config.FilterExpression);
        }

        query = ApplyOrderBy(query, entityType, display);
        query = ApplyTake(query, entityType, AppConstants.MaxLookupListSize);

        var items = await ToListAsync(query, entityType, cancellationToken);

        return DeduplicateByValue(items.Cast<object>().Select(i => MapLookupItem(i, entityType, valueField, display)).ToList());
    }

    private async Task<LookupSearchResultDto> LoadLookupSearchAsync(
        string entityName,
        string? search,
        int skip,
        int take,
        string? filterField,
        string? filterValue,
        CancellationToken cancellationToken)
    {
        var config = await _dbContext.LookupConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.EntityName == entityName && c.IsActive, cancellationToken);

        var entityType = _typeResolver.Resolve(entityName);
        var valueField = LookupFieldResolver.ResolveValueField(entityType, config?.ValueField);
        var display = LookupDisplayExpression.Create(entityType, config?.TextField);

        var query = BuildBaseQuery(entityName, entityType, display);

        if (!string.IsNullOrWhiteSpace(filterField) && !string.IsNullOrWhiteSpace(filterValue))
        {
            query = ApplyDynamicFilter(query, entityType, filterField.Trim(), filterValue.Trim());
        }
        else if (!string.IsNullOrWhiteSpace(config?.FilterExpression))
        {
            query = ApplyStaticFilter(query, entityType, config.FilterExpression);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = ApplyTextSearch(query, entityType, display, search.Trim());
        }

        query = ApplyOrderBy(query, entityType, display);
        query = ApplySkip(query, entityType, skip);
        query = ApplyTake(query, entityType, take + 1);

        var items = await ToListAsync(query, entityType, cancellationToken);
        var mapped = DeduplicateByValue(items.Cast<object>().Select(i => MapLookupItem(i, entityType, valueField, display)).ToList());
        var hasMore = mapped.Count > take;

        if (hasMore)
            mapped = mapped.Take(take).ToList();

        return new LookupSearchResultDto
        {
            Items = mapped,
            HasMore = hasMore
        };
    }

    private async Task<LookupItemDto?> LoadLookupItemByValueAsync(
        string entityName,
        string value,
        CancellationToken cancellationToken)
    {
        var config = await _dbContext.LookupConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.EntityName == entityName && c.IsActive, cancellationToken);

        var entityType = _typeResolver.Resolve(entityName);
        var valueField = LookupFieldResolver.ResolveValueField(entityType, config?.ValueField);
        var display = LookupDisplayExpression.Create(entityType, config?.TextField);
        var query = BuildBaseQuery(entityName, entityType, display);
        query = ApplyDynamicFilter(query, entityType, valueField, value);

        var items = await ToListAsync(query, entityType, cancellationToken);
        var entity = items.Cast<object>().FirstOrDefault();
        return entity == null ? null : MapLookupItem(entity, entityType, valueField, display);
    }

    private async Task<IReadOnlyList<LookupItemDto>> LoadLookupItemsByValuesAsync(
        string entityName,
        IReadOnlyList<string> values,
        CancellationToken cancellationToken)
    {
        if (values.Count == 0)
            return Array.Empty<LookupItemDto>();

        var config = await _dbContext.LookupConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.EntityName == entityName && c.IsActive, cancellationToken);

        var entityType = _typeResolver.Resolve(entityName);
        var valueField = LookupFieldResolver.ResolveValueField(entityType, config?.ValueField);
        var display = LookupDisplayExpression.Create(entityType, config?.TextField);
        var query = BuildBaseQuery(entityName, entityType, display);
        query = ApplyValuesInFilter(query, entityType, valueField, values);

        var items = await ToListAsync(query, entityType, cancellationToken);
        return DeduplicateByValue(items.Cast<object>().Select(i => MapLookupItem(i, entityType, valueField, display)).ToList());
    }

    private static object ApplyValuesInFilter(
        object query,
        Type entityType,
        string valueField,
        IReadOnlyList<string> values)
    {
        var property = entityType.GetProperty(valueField);
        if (property == null || values.Count == 0)
            return query;

        var parameter = Expression.Parameter(entityType, "e");
        var propertyAccess = Expression.Property(parameter, property);
        Expression? predicate = null;

        foreach (var rawValue in values)
        {
            var converted = ConvertFilterValue(rawValue, property.PropertyType);
            if (converted == null)
                continue;

            var constant = Expression.Constant(converted, property.PropertyType);
            var equals = Expression.Equal(propertyAccess, constant);
            predicate = predicate == null ? equals : Expression.OrElse(predicate, equals);
        }

        if (predicate == null)
            return query;

        var lambda = Expression.Lambda(predicate, parameter);
        var whereMethod = typeof(Queryable).GetMethods()
            .First(m => m.Name == nameof(Queryable.Where) && m.GetParameters().Length == 2)
            .MakeGenericMethod(entityType);

        return whereMethod.Invoke(null, [query, lambda])!;
    }

    private object BuildBaseQuery(string entityName, Type entityType, LookupDisplayExpression display)
    {
        var entityContext = _contextResolver.ResolveForEntity(entityName);
        var setMethod = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!.MakeGenericMethod(entityType);
        var dbSet = setMethod.Invoke(entityContext, null)!;

        var asNoTrackingMethod = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods()
            .First(m => m.Name == nameof(EntityFrameworkQueryableExtensions.AsNoTracking) && m.GetParameters().Length == 1)
            .MakeGenericMethod(entityType);

        var query = asNoTrackingMethod.Invoke(null, [dbSet])!;
        return ApplyIncludes(query, entityType, display.GetIncludePaths(entityType));
    }

    private static object ApplyIncludes(object query, Type entityType, IReadOnlyList<string> includePaths)
    {
        foreach (var includePath in includePaths)
        {
            var includeMethod = typeof(EntityFrameworkQueryableExtensions)
                .GetMethods()
                .First(m => m.Name == nameof(EntityFrameworkQueryableExtensions.Include)
                    && m.GetParameters().Length == 2
                    && m.GetParameters()[1].ParameterType == typeof(string))
                .MakeGenericMethod(entityType);

            query = includeMethod.Invoke(null, [query, includePath])!;
        }

        return query;
    }

    private static IReadOnlyList<LookupItemDto> DeduplicateByValue(IReadOnlyList<LookupItemDto> items) =>
        items
            .GroupBy(item => item.Value, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

    private static LookupItemDto MapLookupItem(
        object entity,
        Type entityType,
        string valueField,
        LookupDisplayExpression display) =>
        new()
        {
            Value = entityType.GetProperty(valueField)?.GetValue(entity)?.ToString() ?? "",
            Text = display.Format(entity, entityType)
        };

    private static async Task<System.Collections.IEnumerable> ToListAsync(
        object query,
        Type entityType,
        CancellationToken cancellationToken)
    {
        var toListAsyncMethod = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods()
            .First(m => m.Name == nameof(EntityFrameworkQueryableExtensions.ToListAsync) && m.GetParameters().Length == 2)
            .MakeGenericMethod(entityType);

        var task = (Task)toListAsyncMethod.Invoke(null, [query, cancellationToken])!;
        await task.ConfigureAwait(false);
        return (System.Collections.IEnumerable)task.GetType().GetProperty("Result")!.GetValue(task)!;
    }

    private static object ApplyStaticFilter(object query, Type entityType, string filterExpression)
    {
        var parts = filterExpression.Split('=', 2);
        if (parts.Length != 2) return query;

        var fieldName = parts[0].Trim();
        var expected = parts[1].Trim().Trim('\'');
        return ApplyDynamicFilter(query, entityType, fieldName, expected);
    }

    private static object ApplyDynamicFilter(object query, Type entityType, string filterField, string filterValue)
    {
        var property = entityType.GetProperty(filterField);
        if (property == null) return query;

        var parameter = Expression.Parameter(entityType, "e");
        var propertyAccess = Expression.Property(parameter, property);
        var convertedValue = ConvertFilterValue(filterValue, property.PropertyType);
        var constant = Expression.Constant(convertedValue, property.PropertyType);
        var equality = Expression.Equal(propertyAccess, constant);
        var lambda = Expression.Lambda(equality, parameter);

        var whereMethod = typeof(Queryable).GetMethods()
            .First(m => m.Name == nameof(Queryable.Where) && m.GetParameters().Length == 2)
            .MakeGenericMethod(entityType);

        return whereMethod.Invoke(null, [query, lambda])!;
    }

    private static object ApplyTextSearch(object query, Type entityType, LookupDisplayExpression display, string search)
    {
        var paths = display.GetSearchablePaths(entityType);
        if (paths.Count == 0)
            return query;

        var parameter = Expression.Parameter(entityType, "e");
        var toLowerMethod = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
        var containsMethod = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
        var searchLower = search.ToLowerInvariant();

        Expression? predicate = null;
        foreach (var path in paths)
        {
            var (memberAccess, nullGuard) = path.BuildStringSearchAccess(parameter);
            var notNull = Expression.NotEqual(memberAccess, Expression.Constant(null, typeof(string)));
            var lowerProperty = Expression.Call(memberAccess, toLowerMethod);
            var contains = Expression.Call(lowerProperty, containsMethod, Expression.Constant(searchLower));
            var fieldPredicate = Expression.AndAlso(notNull, contains);
            if (nullGuard != null)
                fieldPredicate = Expression.AndAlso(nullGuard, fieldPredicate);

            predicate = predicate == null ? fieldPredicate : Expression.OrElse(predicate, fieldPredicate);
        }

        if (predicate == null)
            return query;

        var lambda = Expression.Lambda(predicate, parameter);

        var whereMethod = typeof(Queryable).GetMethods()
            .First(m => m.Name == nameof(Queryable.Where) && m.GetParameters().Length == 2)
            .MakeGenericMethod(entityType);

        return whereMethod.Invoke(null, [query, lambda])!;
    }

    private static object ApplyOrderBy(object query, Type entityType, LookupDisplayExpression display)
    {
        var path = display.GetPrimaryOrderPath(entityType);
        if (path == null)
            return query;

        var parameter = Expression.Parameter(entityType, "e");
        var propertyAccess = path.BuildMemberAccess(parameter);
        var propertyType = path.LeafType;
        var lambda = Expression.Lambda(propertyAccess, parameter);

        var orderByMethod = typeof(Queryable).GetMethods()
            .First(m => m.Name == nameof(Queryable.OrderBy) && m.GetParameters().Length == 2)
            .MakeGenericMethod(entityType, propertyType);

        return orderByMethod.Invoke(null, [query, lambda])!;
    }

    private static object ApplySkip(object query, Type entityType, int skip)
    {
        if (skip <= 0)
            return query;

        var skipMethod = typeof(Queryable).GetMethods()
            .First(m => m.Name == nameof(Queryable.Skip) && m.GetParameters().Length == 2)
            .MakeGenericMethod(entityType);

        return skipMethod.Invoke(null, [query, skip])!;
    }

    private static object ApplyTake(object query, Type entityType, int take)
    {
        var takeMethod = typeof(Queryable).GetMethods()
            .First(m => m.Name == nameof(Queryable.Take) && m.GetParameters().Length == 2)
            .MakeGenericMethod(entityType);

        return takeMethod.Invoke(null, [query, take])!;
    }

    private static object? ConvertFilterValue(string filterValue, Type targetType)
    {
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlying == typeof(string))
            return filterValue;

        if (underlying == typeof(int))
            return int.TryParse(filterValue, out var intVal) ? intVal : 0;

        if (underlying == typeof(long))
            return long.TryParse(filterValue, out var longVal) ? longVal : 0L;

        if (underlying == typeof(bool))
            return bool.TryParse(filterValue, out var boolVal) && boolVal;

        if (underlying == typeof(decimal))
            return decimal.TryParse(filterValue, out var decVal) ? decVal : 0m;

        if (underlying == typeof(Guid))
            return Guid.TryParse(filterValue, out var guidVal) ? guidVal : Guid.Empty;

        return Convert.ChangeType(filterValue, underlying);
    }
}
