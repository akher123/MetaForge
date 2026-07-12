namespace MetaForge.Application.DTOs;

/// <summary>
/// Query parameters for the audit log explorer.
/// </summary>
public class AuditLogQuery
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 25;

    public string? EntityName { get; set; }

    public string? Action { get; set; }

    public string? UserName { get; set; }

    public string? RecordId { get; set; }

    public string? Search { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }
}

/// <summary>
/// Summary row for the audit log grid.
/// </summary>
public class AuditLogListItemDto
{
    public long Id { get; init; }

    public string EntityName { get; init; } = string.Empty;

    public string RecordId { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;

    public string? UserName { get; init; }

    public DateTime Timestamp { get; init; }

    public string Summary { get; init; } = string.Empty;

    public int ChangeCount { get; init; }
}

/// <summary>
/// Field-level change for audit detail view.
/// </summary>
public class AuditChangeDto
{
    public string Field { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string? OldValue { get; init; }

    public string? NewValue { get; init; }

    public string ChangeType { get; init; } = "Modified";
}

/// <summary>
/// Grouped section for complex audit payloads (e.g. master-detail saves).
/// </summary>
public class AuditSectionDto
{
    public string Name { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;
}

/// <summary>
/// Timeline entry for the same entity record.
/// </summary>
public class AuditTimelineItemDto
{
    public long Id { get; init; }

    public DateTime Timestamp { get; init; }

    public string Action { get; init; } = string.Empty;

    public string? UserName { get; init; }

    public string Summary { get; init; } = string.Empty;
}

/// <summary>
/// Full audit log detail for the review modal.
/// </summary>
public class AuditLogDetailDto
{
    public long Id { get; init; }

    public string EntityName { get; init; } = string.Empty;

    public string RecordId { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;

    public string? UserName { get; init; }

    public DateTime Timestamp { get; init; }

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<AuditChangeDto> Changes { get; init; } = [];

    public IReadOnlyList<AuditSectionDto> Sections { get; init; } = [];

    public string? OldValueJson { get; init; }

    public string? NewValueJson { get; init; }

    public IReadOnlyList<AuditTimelineItemDto> Timeline { get; init; } = [];
}

/// <summary>
/// Entity name option for audit filters.
/// </summary>
public class AuditEntityOptionDto
{
    public string EntityName { get; init; } = string.Empty;

    public string? FormName { get; init; }
}
