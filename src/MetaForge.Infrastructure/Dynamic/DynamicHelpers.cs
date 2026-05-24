using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;

namespace MetaForge.Infrastructure.Dynamic;

/// <summary>
/// Resolves CLR entity types from configured entity names.
/// </summary>
public interface IEntityTypeResolver
{
    Type Resolve(string entityName);

    IReadOnlyList<Type> GetAllEntityTypes();
}

public sealed class EntityTypeResolver : IEntityTypeResolver
{
    private readonly MetaForgeDbContext _dbContext;

    public EntityTypeResolver(MetaForgeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Type Resolve(string entityName)
    {
        var entityType = _dbContext.Model.GetEntityTypes()
            .FirstOrDefault(t => string.Equals(t.ClrType.Name, entityName, StringComparison.OrdinalIgnoreCase))
            ?.ClrType;

        return entityType ?? throw new NotFoundException($"Entity '{entityName}' was not found in the model.");
    }

    public IReadOnlyList<Type> GetAllEntityTypes() =>
        _dbContext.Model.GetEntityTypes()
            .Where(t => !t.ClrType.Namespace!.StartsWith("MetaForge.Domain.Metadata", StringComparison.Ordinal)
                     && !t.ClrType.Namespace!.StartsWith("MetaForge.Domain.Security", StringComparison.Ordinal)
                     && t.ClrType != typeof(Domain.Audit.AuditLog))
            .Select(t => t.ClrType)
            .ToList();
}

/// <summary>
/// Maps between dynamic dictionaries and EF entities.
/// </summary>
public static class DynamicEntityMapper
{
    public static Dictionary<string, object?> ToDictionary(object entity, IEnumerable<string>? includeProperties = null)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var props = entity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && IsSimpleType(p.PropertyType));

        if (includeProperties != null)
        {
            var set = new HashSet<string>(includeProperties, StringComparer.OrdinalIgnoreCase);
            props = props.Where(p => set.Contains(p.Name));
        }

        foreach (var prop in props)
        {
            result[prop.Name] = prop.GetValue(entity);
        }

        return result;
    }

    public static object CreateEntity(Type entityType, Dictionary<string, object?> data)
    {
        var entity = Activator.CreateInstance(entityType)
            ?? throw new BusinessException($"Unable to create instance of {entityType.Name}.");

        SetProperties(entity, data, isCreate: true);
        return entity;
    }

    public static void UpdateEntity(object entity, Dictionary<string, object?> data)
    {
        SetProperties(entity, data, isCreate: false);
    }

    private static void SetProperties(object entity, Dictionary<string, object?> data, bool isCreate)
    {
        var type = entity.GetType();

        foreach (var (key, value) in data)
        {
            var prop = type.GetProperty(key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop == null || !prop.CanWrite || !IsSimpleType(prop.PropertyType))
                continue;

            if (!isCreate && string.Equals(key, "Id", StringComparison.OrdinalIgnoreCase))
                continue;

            var converted = ConvertValue(value, prop.PropertyType);
            prop.SetValue(entity, converted);
        }
    }

    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value == null)
            return null;

        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
        var isNullable = Nullable.GetUnderlyingType(targetType) != null;

        if (value is JsonElement jsonElement)
            value = UnwrapJsonElement(jsonElement, underlying, isNullable);

        if (value == null)
            return null;

        if (value is string str)
        {
            if (string.IsNullOrWhiteSpace(str))
                return isNullable ? null : (underlying == typeof(string) ? string.Empty : value);

            if (underlying == typeof(string))
                return str;
            if (underlying == typeof(bool))
                return bool.Parse(str);
            if (underlying == typeof(int))
            {
                var parsed = int.Parse(str, CultureInfo.InvariantCulture);
                return isNullable && parsed == 0 ? null : parsed;
            }
            if (underlying == typeof(long))
                return long.Parse(str, CultureInfo.InvariantCulture);
            if (underlying == typeof(decimal))
                return decimal.Parse(str, CultureInfo.InvariantCulture);
            if (underlying == typeof(double))
                return double.Parse(str, CultureInfo.InvariantCulture);
            if (underlying == typeof(float))
                return float.Parse(str, CultureInfo.InvariantCulture);
            if (underlying == typeof(DateTime))
                return DateTime.Parse(str, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (underlying == typeof(DateTimeOffset))
                return DateTimeOffset.Parse(str, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (underlying == typeof(Guid))
                return Guid.Parse(str);
            if (underlying.IsEnum)
                return Enum.Parse(underlying, str, ignoreCase: true);
        }

        if (underlying.IsEnum)
            return Enum.Parse(underlying, value.ToString()!, ignoreCase: true);

        if (underlying == typeof(Guid))
            return value is Guid guid ? guid : Guid.Parse(value.ToString()!);

        if (underlying == typeof(DateTime) && value is DateTime dt)
            return dt;

        if (underlying == typeof(decimal))
        {
            return value switch
            {
                decimal d => d,
                double dbl => Convert.ToDecimal(dbl),
                float f => Convert.ToDecimal(f),
                int i => i,
                long l => l,
                _ => Convert.ToDecimal(value, CultureInfo.InvariantCulture)
            };
        }

        if (value.GetType() == underlying)
            return value;

        var converted = Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
        if (isNullable && converted is int zeroInt && zeroInt == 0)
            return null;

        return converted;
    }

    /// <summary>
    /// Converts dynamic request values (JsonElement, string, number) to int.
    /// </summary>
    public static int ToInt32(object? value)
    {
        if (value == null)
            return 0;

        if (value is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.Null)
                return 0;
            if (jsonElement.ValueKind == JsonValueKind.Number)
                return jsonElement.TryGetInt32(out var n) ? n : (int)jsonElement.GetInt64();
            if (jsonElement.ValueKind == JsonValueKind.String && int.TryParse(jsonElement.GetString(), out var parsed))
                return parsed;
        }

        if (value is int i)
            return i;
        if (value is long l)
            return (int)l;
        if (value is string s && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fromString))
            return fromString;

        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Unwraps JsonElement values from API payloads into CLR types.
    /// </summary>
    public static Dictionary<string, object?> NormalizeDictionary(Dictionary<string, object?> data)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in data)
            result[key] = NormalizeValue(value);
        return result;
    }

    public static object? NormalizeValue(object? value)
    {
        if (value is not JsonElement element)
            return value;

        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Number:
                if (element.TryGetInt32(out var intVal))
                    return intVal;
                if (element.TryGetInt64(out var longVal))
                    return longVal;
                if (element.TryGetDecimal(out var decVal))
                    return decVal;
                return element.GetDouble();
            default:
                return element.ToString();
        }
    }

    public static string? ToStringValue(object? value)
    {
        if (value == null)
            return null;

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                JsonValueKind.String => element.GetString(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                _ => element.ToString()
            };
        }

        return value.ToString();
    }

    private static object? UnwrapJsonElement(JsonElement element, Type underlying, bool isNullable)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;
            case JsonValueKind.String:
                var str = element.GetString();
                return string.IsNullOrWhiteSpace(str) && isNullable ? null : str;
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Number:
                if (underlying == typeof(int) || underlying == typeof(short) || underlying == typeof(byte))
                    return element.TryGetInt32(out var i) ? i : (int)element.GetInt64();
                if (underlying == typeof(long))
                    return element.GetInt64();
                if (underlying == typeof(decimal))
                    return element.GetDecimal();
                if (underlying == typeof(double))
                    return element.GetDouble();
                if (underlying == typeof(float))
                    return element.GetSingle();
                return element.GetRawText();
            default:
                return JsonSerializer.Deserialize(element.GetRawText(), underlying);
        }
    }

    private static bool IsSimpleType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsPrimitive
            || underlying == typeof(string)
            || underlying == typeof(decimal)
            || underlying == typeof(DateTime)
            || underlying == typeof(DateTimeOffset)
            || underlying == typeof(Guid)
            || underlying.IsEnum;
    }
}

/// <summary>
/// Builds dynamic LINQ expressions for grid queries.
/// </summary>
public static class DynamicQueryBuilder
{
    public static IQueryable<T> ApplySearch<T>(IQueryable<T> query, string? searchTerm, IEnumerable<string> searchableColumns) where T : class
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return query;

        var parameter = Expression.Parameter(typeof(T), "x");
        Expression? combined = null;

        foreach (var column in searchableColumns)
        {
            var prop = typeof(T).GetProperty(column, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop == null || prop.PropertyType != typeof(string))
                continue;

            var propertyAccess = Expression.Property(parameter, prop);
            var containsMethod = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
            var contains = Expression.Call(propertyAccess, containsMethod, Expression.Constant(searchTerm));
            combined = combined == null ? contains : Expression.OrElse(combined, contains);
        }

        if (combined == null)
            return query;

        var lambda = Expression.Lambda<Func<T, bool>>(combined, parameter);
        return query.Where(lambda);
    }

    public static IQueryable<T> ApplySort<T>(IQueryable<T> query, string? sortColumn, bool descending) where T : class
    {
        if (string.IsNullOrWhiteSpace(sortColumn))
            return query;

        var prop = typeof(T).GetProperty(sortColumn, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (prop == null)
            return query;

        var parameter = Expression.Parameter(typeof(T), "x");
        var propertyAccess = Expression.Property(parameter, prop);
        var lambda = Expression.Lambda(propertyAccess, parameter);

        var methodName = descending ? "OrderByDescending" : "OrderBy";
        var method = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == methodName && m.GetParameters().Length == 2);
        var genericMethod = method.MakeGenericMethod(typeof(T), prop.PropertyType);

        return (IQueryable<T>)genericMethod.Invoke(null, [query, lambda])!;
    }
}
