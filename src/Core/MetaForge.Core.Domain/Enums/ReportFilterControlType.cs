namespace MetaForge.Domain.Enums;

/// <summary>
/// Runtime filter UI control types for dynamic reports.
/// </summary>
public static class ReportFilterControlType
{
    public const string TextBox = "TextBox";
    public const string Dropdown = "Dropdown";
    public const string Autocomplete = "Autocomplete";
    public const string DateRange = "DateRange";

    public static readonly IReadOnlyList<string> All = [TextBox, Dropdown, Autocomplete, DateRange];
}
