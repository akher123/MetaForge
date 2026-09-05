namespace MetaForge.Application.Validation;

/// <summary>
/// JSON-serialized validation rules stored on <see cref="Domain.Metadata.ForgeField.ValidationRule"/>.
/// Legacy single-string rules (e.g. "MaxLength:50", "Email") are converted at runtime.
/// </summary>
public sealed class FieldValidationRuleSet
{
    public List<FieldValidationRuleDefinition> Rules { get; set; } = [];
}

public sealed class FieldValidationRuleDefinition
{
    /// <summary>Rule type: maxLength, minLength, range, regex, email, phone, url, compareField, unique.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Primary value (maxLength count, regex pattern, etc.).</summary>
    public string? Value { get; set; }

    public string? Min { get; set; }

    public string? Max { get; set; }

    /// <summary>compareField operator: gte, gt, lte, lt, equal, notEqual.</summary>
    public string? Operator { get; set; }

    /// <summary>Other field name for compareField rules.</summary>
    public string? OtherField { get; set; }

    /// <summary>Comma-separated column names for unique rules (defaults to the field property name).</summary>
    public string? Columns { get; set; }

    public string? Message { get; set; }
}

/// <summary>Catalog entry describing a validation rule type for the Form Builder UI.</summary>
public sealed class ValidationRuleTypeDto
{
    public string Type { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public List<ValidationRuleParameterDto> Parameters { get; set; } = [];
}

public sealed class ValidationRuleParameterDto
{
    public string Name { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string InputType { get; set; } = "text";

    public bool Required { get; set; }

    public string? Placeholder { get; set; }

    public List<ValidationRuleOptionDto>? Options { get; set; }
}

public sealed class ValidationRuleOptionDto
{
    public string Value { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;
}

public static class ValidationRuleTypes
{
    public const string MaxLength = "maxlength";
    public const string MinLength = "minlength";
    public const string Range = "range";
    public const string Regex = "regex";
    public const string Email = "email";
    public const string Phone = "phone";
    public const string Url = "url";
    public const string CompareField = "comparefield";
    public const string Unique = "unique";

    public static readonly IReadOnlyList<string> All =
    [
        MaxLength, MinLength, Range, Regex, Email, Phone, Url, CompareField, Unique
    ];
}

public static class ValidationRuleCatalog
{
    public static IReadOnlyList<ValidationRuleTypeDto> GetAll() =>
    [
        new ValidationRuleTypeDto
        {
            Type = ValidationRuleTypes.MaxLength,
            Label = "Maximum Length",
            Description = "Limit the number of characters.",
            Category = "Text",
            Parameters =
            [
                new ValidationRuleParameterDto
                {
                    Name = "value",
                    Label = "Max characters",
                    InputType = "number",
                    Required = true,
                    Placeholder = "50"
                }
            ]
        },
        new ValidationRuleTypeDto
        {
            Type = ValidationRuleTypes.MinLength,
            Label = "Minimum Length",
            Description = "Require a minimum number of characters.",
            Category = "Text",
            Parameters =
            [
                new ValidationRuleParameterDto
                {
                    Name = "value",
                    Label = "Min characters",
                    InputType = "number",
                    Required = true,
                    Placeholder = "2"
                }
            ]
        },
        new ValidationRuleTypeDto
        {
            Type = ValidationRuleTypes.Range,
            Label = "Numeric Range",
            Description = "Value must fall between a minimum and maximum.",
            Category = "Number",
            Parameters =
            [
                new ValidationRuleParameterDto { Name = "min", Label = "Minimum", InputType = "number", Required = true, Placeholder = "0" },
                new ValidationRuleParameterDto { Name = "max", Label = "Maximum", InputType = "number", Required = true, Placeholder = "100" }
            ]
        },
        new ValidationRuleTypeDto
        {
            Type = ValidationRuleTypes.Email,
            Label = "Email Address",
            Description = "Value must contain @ and look like an email.",
            Category = "Format",
            Parameters = []
        },
        new ValidationRuleTypeDto
        {
            Type = ValidationRuleTypes.Phone,
            Label = "Phone Number",
            Description = "Basic phone number format (digits, spaces, dashes, parentheses).",
            Category = "Format",
            Parameters = []
        },
        new ValidationRuleTypeDto
        {
            Type = ValidationRuleTypes.Url,
            Label = "URL / Website",
            Description = "Value must look like a web address (http/https optional).",
            Category = "Format",
            Parameters = []
        },
        new ValidationRuleTypeDto
        {
            Type = ValidationRuleTypes.Regex,
            Label = "Custom Pattern (Regex)",
            Description = "Advanced pattern matching using a regular expression.",
            Category = "Advanced",
            Parameters =
            [
                new ValidationRuleParameterDto
                {
                    Name = "value",
                    Label = "Regular expression",
                    InputType = "text",
                    Required = true,
                    Placeholder = "^[A-Z0-9]+$"
                }
            ]
        },
        new ValidationRuleTypeDto
        {
            Type = ValidationRuleTypes.CompareField,
            Label = "Compare to Another Field",
            Description = "Compare this field's value to another field on the same form.",
            Category = "Cross-field",
            Parameters =
            [
                new ValidationRuleParameterDto
                {
                    Name = "otherField",
                    Label = "Other field",
                    InputType = "text",
                    Required = true,
                    Placeholder = "EndDate"
                },
                new ValidationRuleParameterDto
                {
                    Name = "operator",
                    Label = "Comparison",
                    InputType = "select",
                    Required = true,
                    Options =
                    [
                        new ValidationRuleOptionDto { Value = "gte", Label = "Greater than or equal" },
                        new ValidationRuleOptionDto { Value = "gt", Label = "Greater than" },
                        new ValidationRuleOptionDto { Value = "lte", Label = "Less than or equal" },
                        new ValidationRuleOptionDto { Value = "lt", Label = "Less than" },
                        new ValidationRuleOptionDto { Value = "equal", Label = "Equal to" },
                        new ValidationRuleOptionDto { Value = "notEqual", Label = "Not equal to" }
                    ]
                }
            ]
        },
        new ValidationRuleTypeDto
        {
            Type = ValidationRuleTypes.Unique,
            Label = "Must Be Unique",
            Description = "Value must not already exist in the database for this entity.",
            Category = "Data Integrity",
            Parameters =
            [
                new ValidationRuleParameterDto
                {
                    Name = "columns",
                    Label = "Columns (comma-separated, optional)",
                    InputType = "text",
                    Required = false,
                    Placeholder = "Code"
                }
            ]
        }
    ];
}
