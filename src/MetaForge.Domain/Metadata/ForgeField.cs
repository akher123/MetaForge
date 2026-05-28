namespace MetaForge.Domain.Metadata;

/// <summary>
/// Field-level configuration for dynamic forms.
/// </summary>
public class ForgeField
{
    public int Id { get; set; }

    public int FormId { get; set; }

    public string PropertyName { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string ControlType { get; set; } = "TextBox";

    public bool IsRequired { get; set; }

    public bool IsVisible { get; set; } = true;

    public bool IsReadOnly { get; set; }

    public int DisplayOrder { get; set; }

    public string? ValidationRule { get; set; }

    /// <summary>JSON rules for show/hide, enable/disable, require/optional based on other field values.</summary>
    public string? ConditionalRule { get; set; }

    public string? LookupEntity { get; set; }

    /// <summary>Parent dropdown property name that drives this lookup filter.</summary>
    public string? LookupParentField { get; set; }

    /// <summary>Property on the lookup entity to filter by (defaults to LookupParentField).</summary>
    public string? LookupFilterField { get; set; }

    public string? SectionName { get; set; }

    public ForgeForm Form { get; set; } = null!;
}
