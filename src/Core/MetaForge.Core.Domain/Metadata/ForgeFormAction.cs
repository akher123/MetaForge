using MetaForge.Domain.Enums;

namespace MetaForge.Domain.Metadata;

/// <summary>
/// Configurable toolbar or row action for a dynamic list grid.
/// </summary>
public class ForgeFormAction
{
    public int Id { get; set; }

    public int FormId { get; set; }

    /// <summary>Unique action code within the form (e.g. approve, print).</summary>
    public string Code { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    /// <summary>Font Awesome icon name without the fa- prefix (e.g. check, print).</summary>
    public string? Icon { get; set; }

    public string Placement { get; set; } = GridActionPlacement.Row;

    public string HandlerType { get; set; } = GridActionHandlerType.Api;

    /// <summary>
    /// API path, redirect URL, or script handler name.
    /// Supports placeholders: {id}, {formCode}, {entity}, and row property names.
    /// </summary>
    public string HandlerTarget { get; set; } = string.Empty;

    public string HttpMethod { get; set; } = "POST";

    /// <summary>Optional JSON body template for API handlers.</summary>
    public string? RequestBody { get; set; }

    /// <summary>Permission action required (View, Edit, Delete, Export, Approve). Null defaults to View.</summary>
    public string? PermissionAction { get; set; }

    public string? ConfirmMessage { get; set; }

    /// <summary>Bootstrap button style suffix (e.g. outline-primary, outline-success).</summary>
    public string ButtonStyle { get; set; } = "outline-primary";

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public ForgeForm Form { get; set; } = null!;
}
