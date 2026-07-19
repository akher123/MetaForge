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

    bool IsBusinessEntity(string entityName);
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
            .Where(t => FeatureDiscoveryConstants.IsFeatureEntityNamespace(t.ClrType.Namespace))
            .Select(t => t.ClrType)
            .ToList();

    public bool IsBusinessEntity(string entityName)
    {
        try
        {
            var entityType = Resolve(entityName);
            return FeatureDiscoveryConstants.IsFeatureEntityNamespace(entityType.Namespace);
        }
        catch (NotFoundException)
        {
            return false;
        }
    }
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
    /// Converts a dynamic id value to the entity's primary-key CLR type.
    /// </summary>
    public static object ConvertKey(object? value, Type keyType)
    {
        var converted = ConvertValue(value, keyType);
        if (converted == null)
            throw new BusinessException($"Unable to convert key value '{value}' to {keyType.Name}.");

        return converted;
    }

    /// <summary>
    /// Returns true when <paramref name="value"/> represents a non-default primary key for <paramref name="keyType"/>.
    /// </summary>
    public static bool HasAssignedKey(object? value, Type keyType)
    {
        if (value == null)
            return false;

        var converted = ConvertValue(value, keyType);
        if (converted == null)
            return false;

        var underlying = Nullable.GetUnderlyingType(keyType) ?? keyType;
        if (underlying == typeof(string))
            return !string.IsNullOrWhiteSpace(converted as string);

        var defaultValue = underlying.IsValueType ? Activator.CreateInstance(underlying) : null;
        return !Equals(converted, defaultValue);
    }

    /// <summary>
    /// Converts dynamic request values to a distinct list of positive integers (MultiSelect payloads).
    /// </summary>
    public static List<int> ToInt32List(object? value)
    {
        if (value == null)
            return [];

        if (value is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.Array)
            {
                var list = new List<int>();
                foreach (var item in jsonElement.EnumerateArray())
                    AddInt32(list, UnwrapJsonElement(item, typeof(int), false));
                return list.Distinct().Where(id => id > 0).ToList();
            }

            if (jsonElement.ValueKind == JsonValueKind.Null)
                return [];

            return ToInt32List(UnwrapJsonElement(jsonElement, typeof(int), true));
        }

        if (value is string str)
        {
            if (string.IsNullOrWhiteSpace(str))
                return [];

            str = str.Trim();
            if (str.StartsWith('[') && str.EndsWith(']'))
            {
                try
                {
                    using var doc = JsonDocument.Parse(str);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        return ToInt32List(doc.RootElement);
                }
                catch (JsonException)
                {
                    // fall through to comma-separated parsing
                }
            }

            return str.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => ToInt32(s))
                .Where(id => id > 0)
                .Distinct()
                .ToList();
        }

        if (value is IEnumerable<int> intEnumerable)
            return intEnumerable.Where(id => id > 0).Distinct().ToList();

        if (value is IEnumerable<object> objectEnumerable)
        {
            var list = new List<int>();
            foreach (var item in objectEnumerable)
                AddInt32(list, item);
            return list.Distinct().Where(id => id > 0).ToList();
        }

        var single = ToInt32(value);
        return single > 0 ? [single] : [];
    }

    private static void AddInt32(List<int> list, object? value)
    {
        if (value == null)
            return;

        var id = ToInt32(value);
        if (id > 0)
            list.Add(id);
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
            case JsonValueKind.Array:
            {
                var list = new List<object?>();
                foreach (var item in element.EnumerateArray())
                    list.Add(NormalizeValue(item));
                return list;
            }
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

    /// <summary>
    /// Applies column filters. Keys may include an operator suffix: PropertyName__gte, PropertyName__contains, etc.
    /// Supported suffixes: eq (default), ne, contains, startswith, gt, lt, gte, lte, between (value: min|max).
    /// </summary>
    public static IQueryable<T> ApplyFilters<T>(IQueryable<T> query, Dictionary<string, string>? filters) where T : class
    {
        if (filters == null || filters.Count == 0)
            return query;

        foreach (var (rawKey, rawValue) in filters)
        {
            if (string.IsNullOrWhiteSpace(rawKey) || string.IsNullOrWhiteSpace(rawValue))
                continue;

            var (propertyName, op) = ParseFilterKey(rawKey);
            var prop = typeof(T).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop == null)
                continue;

            var predicate = BuildFilterPredicate<T>(prop, op, rawValue.Trim());
            if (predicate == null)
                continue;

            query = query.Where(predicate);
        }

        return query;
    }

    internal static (string PropertyName, string Operator) ParseFilterKey(string rawKey)
    {
        const string separator = "__";
        var index = rawKey.LastIndexOf(separator, StringComparison.Ordinal);
        if (index <= 0)
            return (rawKey, "eq");

        var propertyName = rawKey[..index];
        var op = rawKey[(index + separator.Length)..].ToLowerInvariant();
        return (propertyName, op);
    }

    private static Expression<Func<T, bool>>? BuildFilterPredicate<T>(PropertyInfo prop, string op, string rawValue) where T : class
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var propertyAccess = Expression.Property(parameter, prop);
        var propertyType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

        if (op == "between")
        {
            var parts = rawValue.Split('|', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
                return null;

            var lower = BuildComparison(propertyAccess, propertyType, parts[0], Expression.GreaterThanOrEqual);
            var upper = BuildComparison(propertyAccess, propertyType, parts[1], Expression.LessThanOrEqual);
            if (lower == null || upper == null)
                return null;

            var combined = Expression.AndAlso(lower, upper);
            return Expression.Lambda<Func<T, bool>>(combined, parameter);
        }

        if (op is "contains" or "startswith")
        {
            if (propertyType != typeof(string))
                return null;

            var notNull = Expression.NotEqual(propertyAccess, Expression.Constant(null, typeof(string)));
            var methodName = op == "contains" ? nameof(string.Contains) : nameof(string.StartsWith);
            var method = typeof(string).GetMethod(methodName, [typeof(string)])!;
            var call = Expression.Call(propertyAccess, method, Expression.Constant(rawValue));
            return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(notNull, call), parameter);
        }

        Func<Expression, Expression, BinaryExpression> comparisonOp = op switch
        {
            "eq" => Expression.Equal,
            "ne" => Expression.NotEqual,
            "gt" => Expression.GreaterThan,
            "lt" => Expression.LessThan,
            "gte" => Expression.GreaterThanOrEqual,
            "lte" => Expression.LessThanOrEqual,
            _ => Expression.Equal
        };

        var comparison = BuildComparison(propertyAccess, propertyType, rawValue, comparisonOp);
        return comparison == null
            ? null
            : Expression.Lambda<Func<T, bool>>(comparison, parameter);
    }

    private static BinaryExpression? BuildComparison(
        MemberExpression propertyAccess,
        Type propertyType,
        string rawValue,
        Func<Expression, Expression, BinaryExpression> comparisonFactory)
    {
        if (propertyType == typeof(string))
        {
            var constant = Expression.Constant(rawValue);
            return comparisonFactory(propertyAccess, constant);
        }

        if (!TryConvertFilterValue(rawValue, propertyType, out var converted))
            return null;

        var valueExpression = Expression.Constant(converted, propertyType);
        var left = propertyAccess;
        if (Nullable.GetUnderlyingType(propertyAccess.Type) != null)
            left = Expression.Property(propertyAccess, "Value");

        return comparisonFactory(left, valueExpression);
    }

    internal static bool TryConvertFilterValue(string rawValue, Type targetType, out object? converted)
    {
        converted = null;
        try
        {
            if (targetType == typeof(Guid))
            {
                if (!Guid.TryParse(rawValue, out var guid))
                    return false;

                converted = guid;
                return true;
            }

            if (targetType.IsEnum)
            {
                converted = Enum.Parse(targetType, rawValue, true);
                return true;
            }

            if (targetType == typeof(DateTime))
            {
                if (!DateTime.TryParse(rawValue, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var date))
                    return false;

                converted = date;
                return true;
            }

            if (targetType == typeof(DateTimeOffset))
            {
                if (!DateTimeOffset.TryParse(rawValue, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var date))
                    return false;

                converted = date;
                return true;
            }

            converted = Convert.ChangeType(rawValue, targetType, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
