namespace MetaForge.Domain.Enums;

/// <summary>
/// Navigation menu node types.
/// </summary>
public static class MenuItemType
{
    public const string Folder = "Folder";
    public const string Form = "Form";
    public const string Url = "Url";

    public static readonly IReadOnlyList<string> All = [Folder, Form, Url];
}
