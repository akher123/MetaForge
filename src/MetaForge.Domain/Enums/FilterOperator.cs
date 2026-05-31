namespace MetaForge.Domain.Enums;

/// <summary>
/// Filter operators for report and grid queries.
/// </summary>
public enum FilterOperator
{
    Equals,
    NotEquals,
    Contains,
    StartsWith,
    GreaterThan,
    LessThan,
    GreaterOrEqual,
    LessOrEqual,
    Between
}
