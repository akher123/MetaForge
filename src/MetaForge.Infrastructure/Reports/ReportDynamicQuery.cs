using System.Linq.Expressions;
using System.Reflection;
using MetaForge.Infrastructure.Dynamic;

namespace MetaForge.Infrastructure.Reports;

/// <summary>
/// Dynamic LINQ filters, sort, and search for dotted property paths on entity graphs.
/// </summary>
internal static class ReportDynamicQuery
{
    public static IQueryable<T> ApplyIncludes<T>(IQueryable<T> query, IEnumerable<string> includePaths) where T : class
    {
        foreach (var path in includePaths)
            query = query.Include(path);

        return query;
    }

    public static IQueryable<T> ApplySearch<T>(IQueryable<T> query, string? searchTerm, IEnumerable<string> searchablePaths) where T : class
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return query;

        var parameter = Expression.Parameter(typeof(T), "x");
        Expression? combined = null;

        foreach (var path in searchablePaths)
        {
            if (!ReportPropertyPathResolver.TryResolve(typeof(T), path, out var leaf, out _)
                || leaf?.PropertyType != typeof(string))
                continue;

            var propertyAccess = BuildPropertyAccess(parameter, path);
            if (propertyAccess == null)
                continue;

            var containsMethod = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
            var notNull = Expression.NotEqual(propertyAccess, Expression.Constant(null, typeof(string)));
            var contains = Expression.Call(propertyAccess, containsMethod, Expression.Constant(searchTerm));
            var predicate = Expression.AndAlso(notNull, contains);
            combined = combined == null ? predicate : Expression.OrElse(combined, predicate);
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

        if (!ReportPropertyPathResolver.TryResolve(typeof(T), sortColumn, out var leaf, out _)
            || leaf == null)
            return query;

        var parameter = Expression.Parameter(typeof(T), "x");
        var propertyAccess = BuildPropertyAccess(parameter, sortColumn);
        if (propertyAccess == null)
            return query;

        var lambda = Expression.Lambda(propertyAccess, parameter);
        var methodName = descending ? "OrderByDescending" : "OrderBy";
        var method = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == methodName && m.GetParameters().Length == 2);
        var genericMethod = method.MakeGenericMethod(typeof(T), leaf.PropertyType);

        return (IQueryable<T>)genericMethod.Invoke(null, [query, lambda])!;
    }

    public static IQueryable<T> ApplyFilters<T>(IQueryable<T> query, Dictionary<string, string>? filters) where T : class
    {
        if (filters == null || filters.Count == 0)
            return query;

        foreach (var (rawKey, rawValue) in filters)
        {
            if (string.IsNullOrWhiteSpace(rawKey) || string.IsNullOrWhiteSpace(rawValue))
                continue;

            var (propertyName, op) = DynamicQueryBuilder.ParseFilterKey(rawKey);
            if (!ReportPropertyPathResolver.TryResolve(typeof(T), propertyName, out var leaf, out _)
                || leaf == null)
                continue;

            var predicate = BuildFilterPredicate<T>(propertyName, leaf, op, rawValue.Trim());
            if (predicate == null)
                continue;

            query = query.Where(predicate);
        }

        return query;
    }

    private static Expression<Func<T, bool>>? BuildFilterPredicate<T>(
        string propertyPath,
        PropertyInfo leaf,
        string op,
        string rawValue) where T : class
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var propertyAccess = BuildPropertyAccess(parameter, propertyPath);
        if (propertyAccess == null)
            return null;

        var propertyType = Nullable.GetUnderlyingType(leaf.PropertyType) ?? leaf.PropertyType;

        if (op == "between")
        {
            var parts = rawValue.Split('|', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
                return null;

            var lower = BuildComparison(propertyAccess, propertyType, parts[0], Expression.GreaterThanOrEqual);
            var upper = BuildComparison(propertyAccess, propertyType, parts[1], Expression.LessThanOrEqual);
            if (lower == null || upper == null)
                return null;

            return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(lower, upper), parameter);
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
        Expression propertyAccess,
        Type propertyType,
        string rawValue,
        Func<Expression, Expression, BinaryExpression> comparisonFactory)
    {
        if (propertyType == typeof(string))
            return comparisonFactory(propertyAccess, Expression.Constant(rawValue));

        if (!DynamicQueryBuilder.TryConvertFilterValue(rawValue, propertyType, out var converted))
            return null;

        var valueExpression = Expression.Constant(converted, propertyType);
        var left = propertyAccess;
        if (Nullable.GetUnderlyingType(propertyAccess.Type) != null)
            left = Expression.Property(propertyAccess, "Value");

        return comparisonFactory(left, valueExpression);
    }

    private static Expression? BuildPropertyAccess(ParameterExpression parameter, string path)
    {
        Expression? current = parameter;

        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var prop = current!.Type.GetProperty(segment, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop == null)
                return null;

            current = Expression.Property(current, prop);
        }

        return current;
    }
}
