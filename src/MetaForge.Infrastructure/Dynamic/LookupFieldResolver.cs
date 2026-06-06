using System.Reflection;
using MetaForge.Application.DTOs;

namespace MetaForge.Infrastructure.Dynamic;

/// <summary>
/// Resolves lookup value/text fields when entity metadata has no <c>Name</c> property.
/// </summary>
public static class LookupFieldResolver
{
    public const string DefaultValueField = "Id";
    public const string DefaultTextField = "Name";

    private static readonly string[] PreferredTextFields =
        ["Name", "Title", "Code", "Description", "Label", "DisplayName", "OrderNo", "VehicleNumber"];

    public static string ResolveValueField(Type entityType, string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var prop = entityType.GetProperty(configured.Trim());
            if (prop != null)
                return prop.Name;
        }

        return entityType.GetProperty(DefaultValueField) != null
            ? DefaultValueField
            : entityType.GetProperties().FirstOrDefault()?.Name ?? DefaultValueField;
    }

    public static string ResolveTextField(Type entityType, string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var prop = entityType.GetProperty(configured.Trim());
            if (prop != null && IsDisplayableType(prop.PropertyType))
                return prop.Name;
        }

        var preferred = ResolvePreferredTextProperty(entityType.GetProperties());
        if (preferred != null)
            return preferred.Name;

        return DefaultTextField;
    }

    public static string InferTextField(EntityMetadataDto metadata)
    {
        var properties = metadata.Properties
            .Where(p => p.ClrType.Contains("String", StringComparison.Ordinal))
            .Select(p => new DisplayPropertyCandidate(p.Name, p.IsNullable, p.IsForeignKey, p.IsKey))
            .ToList();

        var preferred = ResolvePreferredTextProperty(properties);
        return preferred?.Name ?? DefaultTextField;
    }

    private sealed record DisplayPropertyCandidate(string Name, bool IsNullable, bool IsForeignKey, bool IsKey);

    private static System.Reflection.PropertyInfo? ResolvePreferredTextProperty(System.Reflection.PropertyInfo[] properties)
    {
        var candidates = properties
            .Where(p => p.PropertyType == typeof(string)
                || Nullable.GetUnderlyingType(p.PropertyType) == typeof(string))
            .Select(p => new DisplayPropertyCandidate(
                p.Name,
                IsNullableStringProperty(p),
                p.Name.EndsWith("Id", StringComparison.Ordinal) && !p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase),
                p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var match = ResolvePreferredTextProperty(candidates);
        return match == null ? null : properties.FirstOrDefault(p => p.Name == match.Name);
    }

    private static bool IsNullableStringProperty(System.Reflection.PropertyInfo property)
    {
        if (property.PropertyType != typeof(string))
            return Nullable.GetUnderlyingType(property.PropertyType) == typeof(string);

        return new NullabilityInfoContext().Create(property).WriteState is NullabilityState.Nullable;
    }

    private static DisplayPropertyCandidate? ResolvePreferredTextProperty(IReadOnlyList<DisplayPropertyCandidate> properties)
    {
        foreach (var candidate in PreferredTextFields)
        {
            var prop = properties.FirstOrDefault(p =>
                p.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase) && !p.IsNullable);
            if (prop != null)
                return prop;
        }

        foreach (var candidate in PreferredTextFields)
        {
            var prop = properties.FirstOrDefault(p =>
                p.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase));
            if (prop != null)
                return prop;
        }

        return properties.FirstOrDefault(p =>
            !p.IsForeignKey
            && !p.IsKey
            && !p.Name.EndsWith("Id", StringComparison.Ordinal)
            && !p.Name.Equals("CreatedBy", StringComparison.OrdinalIgnoreCase)
            && !p.Name.Equals("ModifiedBy", StringComparison.OrdinalIgnoreCase)
            && !p.IsNullable)
            ?? properties.FirstOrDefault(p =>
                !p.IsForeignKey
                && !p.IsKey
                && !p.Name.EndsWith("Id", StringComparison.Ordinal)
                && !p.Name.Equals("CreatedBy", StringComparison.OrdinalIgnoreCase)
                && !p.Name.Equals("ModifiedBy", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDisplayableType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying == typeof(string)
            || underlying == typeof(int)
            || underlying == typeof(long)
            || underlying == typeof(decimal)
            || underlying == typeof(short)
            || underlying == typeof(Guid);
    }
}
