namespace MetaForge.Application.DTOs;

/// <summary>
/// Effective form permissions for the current user.
/// </summary>
public class FormPermissionsDto
{
    public string FormCode { get; set; } = string.Empty;

    public bool CanView { get; set; }

    public bool CanCreate { get; set; }

    public bool CanEdit { get; set; }

    public bool CanDelete { get; set; }

    public bool CanExport { get; set; }

    public bool CanApprove { get; set; }

    /// <summary>All granted action names for this form (View, Create, Edit, …).</summary>
    public HashSet<string> GrantedActions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool Has(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
            return CanView;

        if (GrantedActions.Contains(action))
            return true;

        return action switch
        {
            "View" => CanView,
            "Create" => CanCreate,
            "Edit" => CanEdit,
            "Delete" => CanDelete,
            "Export" => CanExport,
            "Approve" => CanApprove,
            _ => false
        };
    }
}
