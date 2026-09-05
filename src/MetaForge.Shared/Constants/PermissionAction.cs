namespace MetaForge.Shared.Constants;

/// <summary>
/// Standard permission actions per module.
/// </summary>
public static class PermissionAction
{
    public const string View = "View";
    public const string Create = "Create";
    public const string Edit = "Edit";
    public const string Delete = "Delete";
    public const string Export = "Export";
    public const string Approve = "Approve";

    public static readonly IReadOnlyList<string> All =
        [View, Create, Edit, Delete, Export, Approve];
}
