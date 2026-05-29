namespace MetaForge.Shared.Constants;

/// <summary>
/// Built-in UI themes. Keys are persisted on <see cref="MetaForge.Domain.Security.User.ThemeKey"/>.
/// </summary>
public static class AppThemes
{
    public const string Default = IndigoLight;

    public const string IndigoLight = "indigo-light";
    public const string IndigoDark = "indigo-dark";
    public const string OceanLight = "ocean-light";
    public const string SlateDark = "slate-dark";
    public const string EmeraldLight = "emerald-light";
    public const string EmeraldDark = "emerald-dark";
    public const string RoseLight = "rose-light";
    public const string VioletDark = "violet-dark";
    public const string AmberLight = "amber-light";
    public const string MidnightDark = "midnight-dark";
    public const string ForestLight = "forest-light";
    public const string GraphiteLight = "graphite-light";

    private static readonly HashSet<string> AllKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        IndigoLight,
        IndigoDark,
        OceanLight,
        SlateDark,
        EmeraldLight,
        EmeraldDark,
        RoseLight,
        VioletDark,
        AmberLight,
        MidnightDark,
        ForestLight,
        GraphiteLight
    };

    public static readonly IReadOnlyList<ThemeDefinition> Catalog =
    [
        new(IndigoLight, "Indigo Light", false, "#4f46e5", "#06b6d4", "#d5dce6", "#ffffff",
            "Default brand palette with indigo primary and cyan accents."),
        new(IndigoDark, "Indigo Dark", true, "#818cf8", "#22d3ee", "#0f172a", "#1e293b",
            "Low-glare dark workspace with indigo highlights."),
        new(OceanLight, "Ocean Light", false, "#0369a1", "#0891b2", "#e0f2fe", "#ffffff",
            "Cool sky blues for a calm, focused daytime UI."),
        new(SlateDark, "Slate Dark", true, "#94a3b8", "#38bdf8", "#020617", "#0f172a",
            "Neutral slate dark mode with crisp blue accents."),
        new(EmeraldLight, "Emerald Light", false, "#059669", "#14b8a6", "#d1fae5", "#ffffff",
            "Fresh greens and teals for finance and operations teams."),
        new(EmeraldDark, "Emerald Dark", true, "#34d399", "#2dd4bf", "#022c22", "#064e3b",
            "Deep forest dark theme with mint highlights."),
        new(RoseLight, "Rose Light", false, "#e11d48", "#f43f5e", "#ffe4e6", "#ffffff",
            "Warm rose accents with a clean, modern light shell."),
        new(VioletDark, "Violet Dark", true, "#a78bfa", "#c084fc", "#1e1b4b", "#2e1065",
            "Rich purple dark mode for creative and analytics work."),
        new(AmberLight, "Amber Light", false, "#d97706", "#f59e0b", "#fef3c7", "#ffffff",
            "Sunny amber tones that stay readable for long sessions."),
        new(MidnightDark, "Midnight Dark", true, "#60a5fa", "#38bdf8", "#030712", "#111827",
            "Deep midnight blues with bright sky accents."),
        new(ForestLight, "Forest Light", false, "#166534", "#65a30d", "#ecfccb", "#ffffff",
            "Earthy olive and lime for outdoor and logistics apps."),
        new(GraphiteLight, "Graphite Light", false, "#374151", "#6b7280", "#f3f4f6", "#ffffff",
            "Minimal grayscale professional theme without color noise.")
    ];

    public static bool IsValid(string? themeKey) =>
        !string.IsNullOrWhiteSpace(themeKey) && AllKeys.Contains(themeKey);

    public static string Normalize(string? themeKey) =>
        IsValid(themeKey) ? themeKey! : Default;

    public static bool IsDark(string themeKey) =>
        Catalog.FirstOrDefault(t => t.Key.Equals(themeKey, StringComparison.OrdinalIgnoreCase))?.IsDark == true;

    public sealed record ThemeDefinition(
        string Key,
        string DisplayName,
        bool IsDark,
        string PrimarySwatch,
        string AccentSwatch,
        string BackgroundSwatch,
        string SurfaceSwatch,
        string Description);
}
