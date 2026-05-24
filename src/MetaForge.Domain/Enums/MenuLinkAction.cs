namespace MetaForge.Domain.Enums;

/// <summary>
/// Target action when a menu item links to a module.
/// </summary>
public static class MenuLinkAction
{
    public const string Index = "Index";
    public const string Create = "Create";

    /// <summary>Legacy value — normalized to <see cref="Index"/>.</summary>
    public const string MasterDetail = "MasterDetail";

    public static readonly IReadOnlyList<string> All = [Index, Create];
}
