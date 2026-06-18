namespace MetaForge.Domain.Enums;

/// <summary>
/// Supported dynamic form control types.
/// </summary>
public static class ControlType
{
    public const string TextBox = "TextBox";
    public const string TextArea = "TextArea";
    public const string Number = "Number";
    public const string Date = "Date";
    public const string DateTime = "DateTime";
    public const string Checkbox = "Checkbox";
    public const string Dropdown = "Dropdown";
    public const string Autocomplete = "Autocomplete";
    public const string Radio = "Radio";
    public const string FileUpload = "FileUpload";

    /// <summary>HTML rich text editor for long formatted content on any form field.</summary>
    public const string RichText = "RichText";

    public const string Hidden = "Hidden";

    public static readonly IReadOnlyList<string> All =
    [
        TextBox, TextArea, Number, Date, DateTime,
        Checkbox, Dropdown, Autocomplete, Radio, FileUpload, RichText, Hidden
    ];

    public static bool IsRichText(string? controlType) =>
        string.Equals(controlType, RichText, StringComparison.OrdinalIgnoreCase);
}
