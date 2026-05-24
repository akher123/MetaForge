using System.Linq.Expressions;
using MetaForge.Infrastructure.Dynamic;
using MetaForge.Shared.Constants;
using Microsoft.Extensions.Caching.Memory;

namespace MetaForge.Infrastructure.Services;

/// <summary>
/// Configuration-driven lookup dropdown engine with optional parent-field filtering.
/// </summary>
public class LookupService : ILookupService
{
    private readonly MetaForgeDbContext _dbContext;
    private readonly IEntityTypeResolver _typeResolver;
    private readonly IMemoryCache _cache;

    public LookupService(MetaForgeDbContext dbContext, IEntityTypeResolver typeResolver, IMemoryCache cache)
    {
        _dbContext = dbContext;
        _typeResolver = typeResolver;
        _cache = cache;
    }

    public async Task<IReadOnlyList<LookupItemDto>> GetLookupItemsAsync(
        string entityName,
        string? filterField = null,
        string? filterValue = null,
        CancellationToken cancellationToken = default)
    {
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

        var valueField = config?.ValueField ?? "Id";
        var textField = config?.TextField ?? "Name";

        var entityType = _typeResolver.Resolve(entityName);
        var setMethod = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!.MakeGenericMethod(entityType);
        var dbSet = setMethod.Invoke(_dbContext, null)!;

        var asNoTrackingMethod = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods()
            .First(m => m.Name == nameof(EntityFrameworkQueryableExtensions.AsNoTracking) && m.GetParameters().Length == 1)
            .MakeGenericMethod(entityType);
        var query = asNoTrackingMethod.Invoke(null, [dbSet])!;

        if (!string.IsNullOrWhiteSpace(filterField) && !string.IsNullOrWhiteSpace(filterValue))
        {
            query = ApplyDynamicFilter(query, entityType, filterField.Trim(), filterValue.Trim());
        }
        else if (!string.IsNullOrWhiteSpace(config?.FilterExpression))
        {
            query = ApplyStaticFilter(query, entityType, config.FilterExpression);
        }

        var toListAsyncMethod = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods()
            .First(m => m.Name == nameof(EntityFrameworkQueryableExtensions.ToListAsync) && m.GetParameters().Length == 2)
            .MakeGenericMethod(entityType);

        var task = (Task)toListAsyncMethod.Invoke(null, [query, cancellationToken])!;
        await task.ConfigureAwait(false);

        var items = (System.Collections.IEnumerable)task.GetType().GetProperty("Result")!.GetValue(task)!;

        return items.Cast<object>().Select(i => new LookupItemDto
        {
            Value = entityType.GetProperty(valueField)?.GetValue(i)?.ToString() ?? "",
            Text = entityType.GetProperty(textField)?.GetValue(i)?.ToString() ?? ""
        }).ToList();
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
