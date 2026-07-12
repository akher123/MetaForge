namespace MetaForge.Application.DTOs;

/// <summary>
/// One level in a multi-table tree form builder configuration.
/// </summary>
public class TreeLevelConfigDto
{
    public int Id { get; set; }

    public int LevelIndex { get; set; }

    public string EntityName { get; set; } = string.Empty;

    public string? ParentEntity { get; set; }

    public string? ForeignKey { get; set; }

    public string DisplayColumn { get; set; } = "Name";

    public List<TreeDisplayColumnDto> DisplayColumns { get; set; } = [];

    public List<FormFieldConfigDto> Fields { get; set; } = [];

    public List<FormGridColumnConfigDto> GridColumns { get; set; } = [];
}

/// <summary>
/// Runtime tree screen definition.
/// </summary>
public class TreeScreenDto
{
    public string FormCode { get; set; } = string.Empty;

    public string FormName { get; set; } = string.Empty;

    public List<TreeLevelDefinitionDto> Levels { get; set; } = [];
}

public class TreeLevelDefinitionDto
{
    public int LevelIndex { get; set; }

    public string EntityName { get; set; } = string.Empty;

    public string? ParentEntity { get; set; }

    public string? ForeignKey { get; set; }

    public string DisplayColumn { get; set; } = "Name";

    public List<TreeDisplayColumnDto> DisplayColumns { get; set; } = [];

    public FormDefinition Form { get; set; } = new();

    public GridDefinition Grid { get; set; } = new();
}

public class TreeNodeDto
{
    public int LevelIndex { get; set; }

    public string EntityName { get; set; } = string.Empty;

    public int Id { get; set; }

    public string Label { get; set; } = string.Empty;

    public bool HasChildren { get; set; }

    public int? ParentId { get; set; }

    public Dictionary<string, object?> Data { get; set; } = [];
}

public class TreeDisplayColumnDto
{
    public string PropertyName { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;
}
