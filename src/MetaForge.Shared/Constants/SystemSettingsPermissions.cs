namespace MetaForge.Shared.Constants;

/// <summary>
/// System-level preferences permission codes.
/// </summary>
public static class SystemSettingsPermissions
{
    public const string FormCode = "systemsettings";

    public const string View = "systemsettings.View";
    public const string Manage = "systemsettings.Manage";

    public static readonly IReadOnlyList<(string Code, string Name, string Action)> All =
    [
        (View, "View System Settings", "View"),
        (Manage, "Manage System Settings", "Manage")
    ];
}
