namespace MetaForge.Application.DTOs;

/// <summary>
/// Master-detail screen payload.
/// </summary>
public class MasterDetailScreenDto
{
    /// <summary>Single = one inline detail grid. Tabular = multiple detail tabs.</summary>
    public string ScreenMode { get; set; } = "Single";

    public FormDefinition MasterForm { get; set; } = new();

    public FormDefinition DetailForm { get; set; } = new();

    public RelationDefinition DetailRelation { get; set; } = new();

    public GridDefinition DetailGrid { get; set; } = new();

    public Dictionary<string, object?>? MasterData { get; set; }

    public List<Dictionary<string, object?>>? DetailData { get; set; }

    public List<DetailSectionDto> DetailSections { get; set; } = [];
}
