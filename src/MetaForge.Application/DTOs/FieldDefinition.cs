namespace MetaForge.Application.DTOs;

/// <summary>
/// Field metadata for dynamic form rendering.
/// </summary>
public class FieldDefinition
{
    public string PropertyName { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string ControlType { get; set; } = "TextBox";

    public bool IsRequired { get; set; }

    public bool IsVisible { get; set; } = true;

    public bool IsReadOnly { get; set; }

    public int DisplayOrder { get; set; }

    public string? ValidationRule { get; set; }

    public string? ConditionalRule { get; set; }

    public string? LookupEntity { get; set; }

    public string? LookupParentField { get; set; }

    public string? LookupFilterField { get; set; }

    public string? MappingEntity { get; set; }

    public string? MappingParentKey { get; set; }

    public string? MappingRelatedKey { get; set; }

    public string? SectionName { get; set; }

    public string? ClrType { get; set; }
}
