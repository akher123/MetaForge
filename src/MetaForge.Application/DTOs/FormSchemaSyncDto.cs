namespace MetaForge.Application.DTOs;

/// <summary>Schema sync change types when comparing a configured form to EF Core entity metadata.</summary>
public static class FormSchemaSyncChangeTypes
{
    public const string Add = "Add";
    public const string Remove = "Remove";
    public const string Update = "Update";
}

/// <summary>Targets of a schema sync change.</summary>
public static class FormSchemaSyncTargets
{
    public const string Field = "Field";
    public const string GridColumn = "GridColumn";
    public const string Relation = "Relation";
}

/// <summary>Single diff item between saved form metadata and discovered entity schema.</summary>
public class FormSchemaSyncChangeDto
{
    /// <summary>Stable key used when applying selected changes (e.g. field:TaxId).</summary>
    public string Key { get; set; } = string.Empty;

    public string ChangeType { get; set; } = FormSchemaSyncChangeTypes.Add;

    public string Target { get; set; } = FormSchemaSyncTargets.Field;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? CurrentSummary { get; set; }

    public string? ProposedSummary { get; set; }

    public bool SelectedByDefault { get; set; } = true;

    public FormFieldConfigDto? ProposedField { get; set; }

    public FormGridColumnConfigDto? ProposedColumn { get; set; }

    public FormRelationConfigDto? ProposedRelation { get; set; }

    public string? ProposedControlType { get; set; }

    public bool? ProposedIsRequired { get; set; }
}

/// <summary>Preview of schema differences for a child detail form in a master/detail screen.</summary>
public class FormSchemaSyncChildPreviewDto
{
    public int FormId { get; set; }

    public string EntityName { get; set; } = string.Empty;

    public string FormName { get; set; } = string.Empty;

    public string? TabLabel { get; set; }

    public string ForeignKey { get; set; } = string.Empty;

    public bool IsNewForm { get; set; }

    public List<FormSchemaSyncChangeDto> Changes { get; set; } = [];

    public bool HasChanges => Changes.Count > 0;
}

/// <summary>Preview of schema differences for a configured form.</summary>
public class FormSchemaSyncPreviewDto
{
    public int FormId { get; set; }

    public string EntityName { get; set; } = string.Empty;

    public string FormName { get; set; } = string.Empty;

    public int CurrentFieldCount { get; set; }

    public int EntityPropertyCount { get; set; }

    public List<FormSchemaSyncChangeDto> Changes { get; set; } = [];

    /// <summary>True when preview includes master and detail child forms.</summary>
    public bool IsCascadeSync { get; set; }

    /// <summary>Master, MasterDetail, or MasterDetailTabular when cascade sync is active.</summary>
    public string? ScreenType { get; set; }

    public List<FormSchemaSyncChildPreviewDto> ChildForms { get; set; } = [];

    public bool HasChanges => Changes.Count > 0 || ChildForms.Any(c => c.HasChanges);
}

/// <summary>Apply request — keys from <see cref="FormSchemaSyncChangeDto.Key"/>.</summary>
public class FormSchemaSyncApplyDto
{
    public List<string> AcceptedKeys { get; set; } = [];
}

/// <summary>Result after applying schema sync to a child detail form.</summary>
public class FormSchemaSyncChildResultDto
{
    public int FormId { get; set; }

    public string EntityName { get; set; } = string.Empty;

    public int AppliedChangeCount { get; set; }

    public bool WasCreated { get; set; }

    public FormConfigDto Form { get; set; } = new();
}

/// <summary>Result after applying schema sync.</summary>
public class FormSchemaSyncResultDto
{
    public int FormId { get; set; }

    public int AppliedChangeCount { get; set; }

    public FormConfigDto Form { get; set; } = new();

    public bool IsCascadeSync { get; set; }

    public List<FormSchemaSyncChildResultDto> ChildForms { get; set; } = [];
}
