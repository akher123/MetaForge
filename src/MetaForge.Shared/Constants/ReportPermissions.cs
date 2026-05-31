namespace MetaForge.Shared.Constants;

/// <summary>
/// Standard permission actions for configured reports.
/// </summary>
public static class ReportPermissionAction
{
    public const string Run = "Run";
    public const string Export = "Export";

    public static readonly IReadOnlyList<string> All = [Run, Export];
}

/// <summary>
/// System-level Report Builder permission codes.
/// </summary>
public static class ReportConfigPermissions
{
    public const string FormCode = "reportconfig";

    public const string View = "reportconfig.View";
    public const string Manage = "reportconfig.Manage";

    public static readonly IReadOnlyList<(string Code, string Name, string Action)> All =
    [
        (View, "View Report Builder", "View"),
        (Manage, "Manage Report Builder", "Manage")
    ];
}

/// <summary>
/// Per-report runtime permission code helpers.
/// </summary>
public static class ReportPermissions
{
    public static string Run(string reportCode) =>
        $"{reportCode.Trim().ToLowerInvariant()}.{ReportPermissionAction.Run}";

    public static string Export(string reportCode) =>
        $"{reportCode.Trim().ToLowerInvariant()}.{ReportPermissionAction.Export}";
}
