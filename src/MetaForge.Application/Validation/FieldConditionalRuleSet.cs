namespace MetaForge.Application.Validation;

/// <summary>
/// JSON-serialized conditional rules stored on <see cref="Domain.Metadata.ForgeField.ConditionalRule"/>.
/// Rules on a field define when to show/hide, enable/disable, or require/optional that field
/// based on values of other fields on the same form.
/// </summary>
public sealed class FieldConditionalRuleSet
{
    public List<FieldConditionalRuleDefinition> Rules { get; set; } = [];
}

public sealed class FieldConditionalRuleDefinition
{
    /// <summary>Action to apply when the condition matches: show, hide, enable, disable, require, optional.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Source field whose value is evaluated.</summary>
    public string SourceField { get; set; } = string.Empty;

    /// <summary>Operator: equals, notEquals, empty, notEmpty, contains, gt, gte, lt, lte.</summary>
    public string Operator { get; set; } = "equals";

    /// <summary>Comparison value (not used for empty/notEmpty operators).</summary>
    public string? Value { get; set; }
}

/// <summary>Effective runtime state for a field after applying base flags and conditional rules.</summary>
public sealed class FieldEffectiveState
{
    public bool IsVisible { get; set; }

    public bool IsRequired { get; set; }

    public bool IsReadOnly { get; set; }
}

public static class ConditionalRuleActions
{
    public const string Show = "show";
    public const string Hide = "hide";
    public const string Enable = "enable";
    public const string Disable = "disable";
    public const string Require = "require";
    public const string Optional = "optional";

    public static readonly IReadOnlyList<string> All =
        [Show, Hide, Enable, Disable, Require, Optional];
}

public static class ConditionalRuleOperators
{
    public const string Equal = "equals";
    public const string NotEqual = "notequals";
    public const string Empty = "empty";
    public const string NotEmpty = "notempty";
    public const string Contains = "contains";
    public const string GreaterThan = "gt";
    public const string GreaterThanOrEqual = "gte";
    public const string LessThan = "lt";
    public const string LessThanOrEqual = "lte";

    public static readonly IReadOnlyList<string> All =
    [
        Equal, NotEqual, Empty, NotEmpty, Contains,
        GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual
    ];
}

/// <summary>Catalog entry for the Form Builder conditional rule UI.</summary>
public sealed class ConditionalRuleActionDto
{
    public string Action { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}

public sealed class ConditionalRuleOperatorDto
{
    public string Operator { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool RequiresValue { get; set; } = true;
}

public static class ConditionalRuleCatalog
{
    public static IReadOnlyList<ConditionalRuleActionDto> GetActions() =>
    [
        new ConditionalRuleActionDto
        {
            Action = ConditionalRuleActions.Show,
            Label = "Show field",
            Description = "Make this field visible when the condition is true."
        },
        new ConditionalRuleActionDto
        {
            Action = ConditionalRuleActions.Hide,
            Label = "Hide field",
            Description = "Hide this field when the condition is true."
        },
        new ConditionalRuleActionDto
        {
            Action = ConditionalRuleActions.Enable,
            Label = "Enable field",
            Description = "Allow editing when the condition is true."
        },
        new ConditionalRuleActionDto
        {
            Action = ConditionalRuleActions.Disable,
            Label = "Disable field",
            Description = "Make read-only when the condition is true."
        },
        new ConditionalRuleActionDto
        {
            Action = ConditionalRuleActions.Require,
            Label = "Require field",
            Description = "Mark as required when the condition is true."
        },
        new ConditionalRuleActionDto
        {
            Action = ConditionalRuleActions.Optional,
            Label = "Make optional",
            Description = "Remove required flag when the condition is true."
        }
    ];

    public static IReadOnlyList<ConditionalRuleOperatorDto> GetOperators() =>
    [
        new ConditionalRuleOperatorDto { Operator = ConditionalRuleOperators.Equal, Label = "Equals", Description = "Source field value matches the comparison value (text or number).", RequiresValue = true },
        new ConditionalRuleOperatorDto { Operator = ConditionalRuleOperators.NotEqual, Label = "Does not equal", Description = "Source field value is different from the comparison value.", RequiresValue = true },
        new ConditionalRuleOperatorDto { Operator = ConditionalRuleOperators.Empty, Label = "Is empty", Description = "Source field has no value (blank, null, or zero for lookups).", RequiresValue = false },
        new ConditionalRuleOperatorDto { Operator = ConditionalRuleOperators.NotEmpty, Label = "Is not empty", Description = "Source field has any value entered.", RequiresValue = false },
        new ConditionalRuleOperatorDto { Operator = ConditionalRuleOperators.Contains, Label = "Contains", Description = "Source field text includes the comparison value.", RequiresValue = true },
        new ConditionalRuleOperatorDto { Operator = ConditionalRuleOperators.GreaterThan, Label = "Greater than", Description = "Source field number is greater than the comparison value.", RequiresValue = true },
        new ConditionalRuleOperatorDto { Operator = ConditionalRuleOperators.GreaterThanOrEqual, Label = "Greater than or equal", Description = "Source field number is greater than or equal to the comparison value.", RequiresValue = true },
        new ConditionalRuleOperatorDto { Operator = ConditionalRuleOperators.LessThan, Label = "Less than", Description = "Source field number is less than the comparison value.", RequiresValue = true },
        new ConditionalRuleOperatorDto { Operator = ConditionalRuleOperators.LessThanOrEqual, Label = "Less than or equal", Description = "Source field number is less than or equal to the comparison value.", RequiresValue = true }
    ];
}
