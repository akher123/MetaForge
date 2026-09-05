namespace MetaForge.Application.DTOs;

/// <summary>Health issue categories for form metadata checks.</summary>
public static class FormHealthIssueCategories
{
    public const string Schema = "Schema";
    public const string Permission = "Permission";
    public const string Lookup = "Lookup";
    public const string Relation = "Relation";
    public const string Menu = "Menu";
    public const string Configuration = "Configuration";
    public const string Discovery = "Discovery";
}

/// <summary>Health issue severity levels.</summary>
public static class FormHealthSeverity
{
    public const string Error = "Error";
    public const string Warning = "Warning";
    public const string Info = "Info";
}

/// <summary>Overall health status for a form or report summary.</summary>
public static class FormHealthStatus
{
    public const string Healthy = "Healthy";
    public const string Warning = "Warning";
    public const string Error = "Error";
}

public class FormHealthIssueDto
{
    public string Category { get; set; } = string.Empty;

    public string Severity { get; set; } = FormHealthSeverity.Warning;

    public string Message { get; set; } = string.Empty;

    public string? ActionUrl { get; set; }
}

public class FormHealthGlobalIssueDto : FormHealthIssueDto;

public class FormHealthItemDto
{
    public int FormId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string GroupName { get; set; } = string.Empty;

    public string FormType { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public string Status { get; set; } = FormHealthStatus.Healthy;

    public int IssueCount { get; set; }

    public string EditUrl { get; set; } = string.Empty;

    public string ModuleUrl { get; set; } = string.Empty;

    public List<FormHealthIssueDto> Issues { get; set; } = [];
}

public class FormHealthReportDto
{
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;

    public int TotalForms { get; set; }

    public int HealthyCount { get; set; }

    public int WarningCount { get; set; }

    public int ErrorCount { get; set; }

    public int FormsNeedingAttention { get; set; }

    public List<FormHealthGlobalIssueDto> GlobalIssues { get; set; } = [];

    public List<FormHealthItemDto> Items { get; set; } = [];
}
