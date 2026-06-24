using System.Reflection;
using System.Text.RegularExpressions;
using MetaForge.Application.DTOs;

namespace MetaForge.Infrastructure.Dynamic;

public enum LookupDisplayMode
{
    Single,
    Concatenated,
    Template
}

/// <summary>
/// Parses and formats lookup display text from one or more entity properties,
/// including navigation property paths such as <c>Vehicle.VehicleNumber</c>.
/// </summary>
public sealed partial class LookupDisplayExpression
{
    private const string DefaultSeparator = " - ";

    public string Raw { get; }

    public LookupDisplayMode Mode { get; }

    public IReadOnlyList<string> PropertyNames { get; }

    public string? Template { get; }

    public string Separator { get; }

    private LookupDisplayExpression(
        string raw,
        LookupDisplayMode mode,
        IReadOnlyList<string> propertyNames,
        string? template,
        string separator)
    {
        Raw = raw;
        Mode = mode;
        PropertyNames = propertyNames;
        Template = template;
        Separator = separator;
    }

    public static LookupDisplayExpression Create(Type entityType, string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
            return FromInferred(entityType);

        configured = configured.Trim();

        if (configured.Contains('{', StringComparison.Ordinal))
            return FromTemplate(entityType, configured);

        if (configured.Contains(',', StringComparison.Ordinal))
            return FromConcatenated(entityType, configured);

        var resolved = LookupFieldResolver.ResolveTextField(entityType, configured);
        return new LookupDisplayExpression(
            configured,
            LookupDisplayMode.Single,
            [resolved],
            null,
            DefaultSeparator);
    }

    public string Format(object entity, Type entityType)
    {
        return Mode switch
        {
            LookupDisplayMode.Template when !string.IsNullOrWhiteSpace(Template) =>
                FormatTemplate(entity, entityType, Template),
            LookupDisplayMode.Concatenated =>
                FormatConcatenated(entity, entityType),
            _ => GetPropertyText(entity, entityType, PropertyNames.FirstOrDefault() ?? LookupFieldResolver.DefaultTextField)
        };
    }

    public IReadOnlyList<LookupPropertyPath> GetSearchablePaths(Type entityType) =>
        ResolvePaths(entityType)
            .Where(path => path.IsStringLeaf)
            .ToList();

    public LookupPropertyPath? GetPrimaryOrderPath(Type entityType) =>
        ResolvePaths(entityType).FirstOrDefault();

    public IReadOnlyList<string> GetIncludePaths(Type entityType) =>
        ResolvePaths(entityType)
            .SelectMany(path => path.GetIncludePaths())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

    private IEnumerable<LookupPropertyPath> ResolvePaths(Type entityType)
    {
        foreach (var name in PropertyNames)
        {
            if (LookupPropertyPath.TryParse(entityType, name, out var path) && path != null)
                yield return path;
        }
    }

    private static LookupDisplayExpression FromInferred(Type entityType)
    {
        var inferred = LookupFieldResolver.ResolveTextField(entityType, null);
        return new LookupDisplayExpression(
            inferred,
            LookupDisplayMode.Single,
            [inferred],
            null,
            DefaultSeparator);
    }

    private static LookupDisplayExpression FromTemplate(Type entityType, string template)
    {
        var propertyNames = PropertyTokenRegex()
            .Matches(template)
            .Select(m => m.Groups[1].Value.Trim())
            .Where(name => LookupPropertyPath.TryParse(entityType, name, out _))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (propertyNames.Count == 0)
            return FromInferred(entityType);

        return new LookupDisplayExpression(
            template,
            LookupDisplayMode.Template,
            propertyNames,
            template,
            DefaultSeparator);
    }

    private static LookupDisplayExpression FromConcatenated(Type entityType, string configured)
    {
        var propertyNames = configured
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(name => LookupPropertyPath.TryParse(entityType, name, out _))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (propertyNames.Count == 0)
            return FromInferred(entityType);

        if (propertyNames.Count == 1)
        {
            return new LookupDisplayExpression(
                propertyNames[0],
                LookupDisplayMode.Single,
                propertyNames,
                null,
                DefaultSeparator);
        }

        return new LookupDisplayExpression(
            configured,
            LookupDisplayMode.Concatenated,
            propertyNames,
            null,
            DefaultSeparator);
    }

    private string FormatTemplate(object entity, Type entityType, string template) =>
        PropertyTokenRegex().Replace(template, match =>
        {
            var propertyName = match.Groups[1].Value.Trim();
            return GetPropertyText(entity, entityType, propertyName);
        });

    private string FormatConcatenated(object entity, Type entityType)
    {
        var parts = PropertyNames
            .Select(name => GetPropertyText(entity, entityType, name))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        return string.Join(Separator, parts);
    }

    private static string GetPropertyText(object entity, Type entityType, string propertyName) =>
        LookupPropertyPath.TryParse(entityType, propertyName, out var path) && path != null
            ? path.GetText(entity)
            : string.Empty;

    [GeneratedRegex(@"\{([^}]+)\}", RegexOptions.Compiled)]
    private static partial Regex PropertyTokenRegex();
}
