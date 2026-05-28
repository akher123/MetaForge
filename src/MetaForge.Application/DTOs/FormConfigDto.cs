namespace MetaForge.Application.DTOs;

/// <summary>
/// Full form configuration for create/edit screens.
/// </summary>
public class FormConfigDto
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string TableName { get; set; } = string.Empty;

    public string GroupName { get; set; } = "Master Data";

    public string FormType { get; set; } = "Master";

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public List<FormFieldConfigDto> Fields { get; set; } = [];

    public List<FormGridColumnConfigDto> GridColumns { get; set; } = [];

    public List<FormGridActionConfigDto> GridActions { get; set; } = [];

    public List<FormRelationConfigDto> Relations { get; set; } = [];
}

public class FormFieldConfigDto
{
    public int Id { get; set; }

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

    public string? SectionName { get; set; }
}

public class FormGridColumnConfigDto
{
    public int Id { get; set; }

    public string PropertyName { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public bool IsSortable { get; set; } = true;

    public bool IsSearchable { get; set; } = true;

    public bool IsVisible { get; set; } = true;
}

public class FormGridActionConfigDto
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string? Icon { get; set; }

    public string Placement { get; set; } = "Row";

    public string HandlerType { get; set; } = "Api";

    public string HandlerTarget { get; set; } = string.Empty;

    public string HttpMethod { get; set; } = "POST";

    public string? RequestBody { get; set; }

    public string? PermissionAction { get; set; }

    public string? ConfirmMessage { get; set; }

    public string ButtonStyle { get; set; } = "outline-primary";

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

public class FormRelationConfigDto
{
    public int Id { get; set; }

    public string RelationType { get; set; } = string.Empty;

    public string ParentEntity { get; set; } = string.Empty;

    public string ChildEntity { get; set; } = string.Empty;

    public string ForeignKey { get; set; } = string.Empty;

    public string? NavigationProperty { get; set; }

    public string? TabLabel { get; set; }

    public int DisplayOrder { get; set; }
}

public class FormConfigListItemDto
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string GroupName { get; set; } = string.Empty;

    public string FormType { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public int FieldCount { get; set; }

    public string Url { get; set; } = string.Empty;
}

public class DiscoveredEntityOptionDto
{
    public string EntityName { get; set; } = string.Empty;

    public string TableName { get; set; } = string.Empty;

    public bool IsConfigured { get; set; }

    public EntityMetadataDto Metadata { get; set; } = new();
}

/// <summary>
/// Form Builder screen payload — master form plus optional detail form.
/// </summary>
public class FormBuilderScreenDto
{
    /// <summary>Master, MasterDetail, or MasterDetailTabular</summary>
    public string ScreenType { get; set; } = "Master";

    public FormConfigDto Master { get; set; } = new();

    public FormConfigDto? Detail { get; set; }
}

/// <summary>
/// Save request for Form Builder (master + optional detail form).
/// </summary>
public class FormBuilderSaveDto
{
    public string ScreenType { get; set; } = "Master";

    public FormConfigDto Master { get; set; } = new();

    public FormConfigDto? Detail { get; set; }
}
