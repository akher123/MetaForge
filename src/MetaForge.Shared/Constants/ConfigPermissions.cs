namespace MetaForge.Shared.Constants;

/// <summary>
/// System-level module configuration permission codes.
/// </summary>
public static class ConfigPermissions
{
    public const string FormCode = "config";

    public const string View = "config.View";
    public const string Manage = "config.Manage";

    public static readonly IReadOnlyList<(string Code, string Name, string Action)> All =
    [
        (View, "View Form Builder", "View"),
        (Manage, "Manage Form Builder", "Manage")
    ];
}
