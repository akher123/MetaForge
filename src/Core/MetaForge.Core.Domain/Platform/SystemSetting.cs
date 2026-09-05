namespace MetaForge.Domain.Platform;

/// <summary>
/// Platform-wide configuration stored in the database (global defaults).
/// </summary>
public class SystemSetting
{
    public int Id { get; set; }

    /// <summary>Stable key, e.g. localization.defaultCulture.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Stored as string; parsed according to <see cref="ValueType"/>.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>bool, string, int, json.</summary>
    public string ValueType { get; set; } = "string";

    public string? Description { get; set; }

    /// <summary>localization, appearance, etc.</summary>
    public string Category { get; set; } = string.Empty;

    public bool IsEditable { get; set; } = true;

    public DateTime UpdatedAtUtc { get; set; }

    public int? UpdatedByUserId { get; set; }
}
