namespace MetaForge.Application.DTOs;

/// <summary>
/// Complete form definition built from metadata.
/// </summary>
public class FormDefinition
{
    public int FormId { get; set; }

    public string FormCode { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string FormName { get; set; } = string.Empty;

    public string FormType { get; set; } = "Master";

    public List<FieldDefinition> Fields { get; set; } = [];

    public List<RelationDefinition> Relations { get; set; } = [];
}
