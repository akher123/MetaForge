using System.Globalization;

namespace MetaForge.Shared.Culture;

/// <summary>
/// Read-only catalog of cultures provided by the .NET runtime.
/// </summary>
public static class CultureCatalog
{
    private static readonly Lazy<IReadOnlyDictionary<string, CultureInfo>> SpecificCulturesByName = new(LoadSpecificCultures);

    public static IReadOnlyList<CultureInfo> GetSpecificCultures() =>
        SpecificCulturesByName.Value.Values
            .OrderBy(c => c.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    public static bool IsSupported(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
            return false;

        return TryNormalize(culture, out _);
    }

    public static bool TryNormalize(string? culture, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(culture))
            return false;

        try
        {
            var cultureInfo = CultureInfo.GetCultureInfo(culture.Trim());
            normalized = cultureInfo.Name;
            return SpecificCulturesByName.Value.ContainsKey(normalized);
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }

    public static string NormalizeOrThrow(string culture)
    {
        if (!TryNormalize(culture, out var normalized))
            throw new CultureNotFoundException($"Culture '{culture}' is not supported by the .NET runtime.");

        return normalized;
    }

    private static IReadOnlyDictionary<string, CultureInfo> LoadSpecificCultures()
    {
        var cultures = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .Where(c => !string.IsNullOrWhiteSpace(c.Name))
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First());

        return cultures.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
    }
}
