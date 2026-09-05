using System.Linq.Expressions;
using System.Reflection;

namespace MetaForge.Infrastructure.Dynamic;

/// <summary>
/// Resolves a dot-separated property path (including navigation properties) on a lookup entity.
/// </summary>
public sealed class LookupPropertyPath
{
    private LookupPropertyPath(string raw, IReadOnlyList<PropertyInfo> segments)
    {
        Raw = raw;
        Segments = segments;
    }

    public string Raw { get; }

    public IReadOnlyList<PropertyInfo> Segments { get; }

    public Type LeafType => Segments[^1].PropertyType;

    public bool IsStringLeaf
    {
        get
        {
            var underlying = Nullable.GetUnderlyingType(LeafType) ?? LeafType;
            return underlying == typeof(string);
        }
    }

    public static bool TryParse(Type entityType, string path, out LookupPropertyPath? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var segments = new List<PropertyInfo>();
        var currentType = entityType;

        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var property = currentType.GetProperty(
                part,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property == null)
                return false;

            segments.Add(property);
            currentType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        }

        if (segments.Count == 0 || !LookupFieldResolver.IsDisplayableType(segments[^1].PropertyType))
            return false;

        result = new LookupPropertyPath(path.Trim(), segments);
        return true;
    }

    public string GetText(object entity)
    {
        object? current = entity;
        foreach (var segment in Segments)
        {
            if (current == null)
                return string.Empty;

            current = segment.GetValue(current);
        }

        return current?.ToString() ?? string.Empty;
    }

    public Expression BuildMemberAccess(ParameterExpression parameter)
    {
        Expression current = parameter;
        foreach (var segment in Segments)
            current = Expression.Property(current, segment);

        return current;
    }

    public (Expression MemberAccess, Expression? NullGuard) BuildStringSearchAccess(ParameterExpression parameter)
    {
        Expression current = parameter;
        Expression? nullGuard = null;

        for (var i = 0; i < Segments.Count; i++)
        {
            current = Expression.Property(current, Segments[i]);
            if (i < Segments.Count - 1)
            {
                var notNull = Expression.NotEqual(current, Expression.Constant(null, current.Type));
                nullGuard = nullGuard == null ? notNull : Expression.AndAlso(nullGuard, notNull);
            }
        }

        return (current, nullGuard);
    }

    public IEnumerable<string> GetIncludePaths()
    {
        if (Segments.Count <= 1)
            yield break;

        var parts = new List<string>();
        for (var i = 0; i < Segments.Count - 1; i++)
        {
            if (!IsNavigationProperty(Segments[i]))
                yield break;

            parts.Add(Segments[i].Name);
            yield return string.Join('.', parts);
        }
    }

    internal static bool IsNavigationProperty(PropertyInfo property)
    {
        var type = property.PropertyType;
        if (type == typeof(string))
            return false;

        if (Nullable.GetUnderlyingType(type) != null)
            return false;

        if (type.IsValueType)
            return false;

        if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
            return false;

        return type.IsClass;
    }
}
