namespace MetaForge.Shared.Constants;

/// <summary>
/// System-level Email Configuration permission codes.
/// </summary>
public static class EmailConfigPermissions
{
    public const string FormCode = "emailconfig";

    public const string View = "emailconfig.View";
    public const string Manage = "emailconfig.Manage";

    public static readonly IReadOnlyList<(string Code, string Name, string Action)> All =
    [
        (View, "View Email Configuration", "View"),
        (Manage, "Manage Email Configuration", "Manage")
    ];
}
