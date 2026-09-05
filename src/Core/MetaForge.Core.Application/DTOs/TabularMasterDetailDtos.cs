namespace MetaForge.Application.DTOs;

/// <summary>
/// One child grid section inside a tabular master-detail screen.
/// </summary>
public class DetailSectionDto
{
    public string ChildEntity { get; set; } = string.Empty;

    public string ForeignKey { get; set; } = string.Empty;

    public string TabLabel { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public FormDefinition DetailForm { get; set; } = new();

    public RelationDefinition Relation { get; set; } = new();

    public GridDefinition DetailGrid { get; set; } = new();

    public List<Dictionary<string, object?>>? DetailData { get; set; }
}

/// <summary>
/// Save payload for one tabular detail section.
/// </summary>
public class DetailSectionSaveDto
{
    public string ChildEntity { get; set; } = string.Empty;

    public List<Dictionary<string, object?>> Rows { get; set; } = [];

    public List<object> DeletedIds { get; set; } = [];
}
