using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentValidation.Results;
using MetaForge.Application.Validation;
using MetaForge.Infrastructure.Dynamic;

namespace MetaForge.Infrastructure.Validation;

/// <summary>
/// Parses and evaluates field validation rules from JSON or legacy string format.
/// </summary>
public static class FieldValidationRuleEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Regex PhonePattern = new(
        @"^\+?[\d\s\-\(\)\.]{7,20}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex UrlPattern = new(
        @"^(https?://)?([\w\-]+\.)+[\w\-]+(/[\w\-._~:/?#\[\]@!$&'()*+,;=%]*)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static FieldValidationRuleSet? Parse(string? validationRule)
    {
        if (string.IsNullOrWhiteSpace(validationRule))
            return null;

        validationRule = validationRule.Trim();
        if (validationRule.StartsWith('{'))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<FieldValidationRuleSet>(validationRule, JsonOptions);
                NormalizeRuleSet(parsed);
                return parsed;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        var rules = new List<FieldValidationRuleDefinition>();
        foreach (var part in validationRule.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var legacy = ParseLegacyRule(part);
            if (legacy != null)
                rules.Add(legacy);
        }

        return rules.Count > 0 ? new FieldValidationRuleSet { Rules = rules } : null;
    }

    private static void NormalizeRuleSet(FieldValidationRuleSet? ruleSet)
    {
        if (ruleSet?.Rules == null)
            return;

        foreach (var rule in ruleSet.Rules)
        {
            if (!string.IsNullOrWhiteSpace(rule.Type))
                rule.Type = rule.Type.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(rule.Operator))
                rule.Operator = rule.Operator.Trim().ToLowerInvariant();
        }
    }

    public static string Serialize(FieldValidationRuleSet ruleSet) =>
        JsonSerializer.Serialize(ruleSet, JsonOptions);

    public static string Summarize(string? validationRule)
    {
        var ruleSet = Parse(validationRule);
        if (ruleSet?.Rules == null || ruleSet.Rules.Count == 0)
            return string.Empty;

        return string.Join(", ", ruleSet.Rules.Select(SummarizeRule));
    }

    public static void ApplyRules(
        string propertyName,
        string label,
        string? validationRule,
        Dictionary<string, object?> data,
        List<ValidationFailure> failures)
    {
        var ruleSet = Parse(validationRule);
        if (ruleSet?.Rules == null || ruleSet.Rules.Count == 0)
            return;

        data.TryGetValue(propertyName, out var rawValue);
        var strValue = DynamicEntityMapper.ToStringValue(rawValue);

        foreach (var rule in ruleSet.Rules)
        {
            if (IsUniqueRule(rule))
                continue;

            if (string.IsNullOrWhiteSpace(strValue) && !IsCompareFieldRule(rule))
                continue;

            ApplyRule(propertyName, label, rule, strValue ?? string.Empty, rawValue, data, failures);
        }
    }

    public static bool IsUniqueRule(FieldValidationRuleDefinition rule) =>
        string.Equals(rule.Type, ValidationRuleTypes.Unique, StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<string> ResolveUniqueColumns(FieldValidationRuleDefinition rule, string defaultColumn)
    {
        var source = !string.IsNullOrWhiteSpace(rule.Columns)
            ? rule.Columns
            : rule.Value;

        if (string.IsNullOrWhiteSpace(source))
            return [defaultColumn];

        return source
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static bool IsCompareFieldRule(FieldValidationRuleDefinition rule) =>
        string.Equals(rule.Type, ValidationRuleTypes.CompareField, StringComparison.OrdinalIgnoreCase);

    private static FieldValidationRuleDefinition? ParseLegacyRule(string rule)
    {
        var parts = rule.Split(':', 2, StringSplitOptions.TrimEntries);
        var ruleName = parts[0].ToLowerInvariant();
        var ruleValue = parts.Length > 1 ? parts[1] : null;

        return ruleName switch
        {
            ValidationRuleTypes.MaxLength when !string.IsNullOrWhiteSpace(ruleValue) => new FieldValidationRuleDefinition
            {
                Type = ValidationRuleTypes.MaxLength,
                Value = ruleValue
            },
            ValidationRuleTypes.MinLength when !string.IsNullOrWhiteSpace(ruleValue) => new FieldValidationRuleDefinition
            {
                Type = ValidationRuleTypes.MinLength,
                Value = ruleValue
            },
            ValidationRuleTypes.Range when !string.IsNullOrWhiteSpace(ruleValue) => ParseLegacyRange(ruleValue),
            ValidationRuleTypes.Regex when !string.IsNullOrWhiteSpace(ruleValue) => new FieldValidationRuleDefinition
            {
                Type = ValidationRuleTypes.Regex,
                Value = ruleValue
            },
            ValidationRuleTypes.Email => new FieldValidationRuleDefinition { Type = ValidationRuleTypes.Email },
            ValidationRuleTypes.Phone => new FieldValidationRuleDefinition { Type = ValidationRuleTypes.Phone },
            ValidationRuleTypes.Url => new FieldValidationRuleDefinition { Type = ValidationRuleTypes.Url },
            _ => null
        };
    }

    private static FieldValidationRuleDefinition? ParseLegacyRange(string ruleValue)
    {
        var rangeParts = ruleValue.Split('-', 2, StringSplitOptions.TrimEntries);
        if (rangeParts.Length != 2)
            return null;

        return new FieldValidationRuleDefinition
        {
            Type = ValidationRuleTypes.Range,
            Min = rangeParts[0],
            Max = rangeParts[1]
        };
    }

    private static void ApplyRule(
        string propertyName,
        string label,
        FieldValidationRuleDefinition rule,
        string strValue,
        object? rawValue,
        Dictionary<string, object?> data,
        List<ValidationFailure> failures)
    {
        var type = rule.Type?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(type))
            return;

        switch (type)
        {
            case ValidationRuleTypes.MaxLength:
                if (int.TryParse(rule.Value, out var maxLen) && strValue.Length > maxLen)
                {
                    failures.Add(new ValidationFailure(propertyName,
                        rule.Message ?? $"{label} must not exceed {maxLen} characters."));
                }
                break;

            case ValidationRuleTypes.MinLength:
                if (int.TryParse(rule.Value, out var minLen) && strValue.Length < minLen)
                {
                    failures.Add(new ValidationFailure(propertyName,
                        rule.Message ?? $"{label} must be at least {minLen} characters."));
                }
                break;

            case ValidationRuleTypes.Range:
                if (TryParseDecimal(strValue, out var num)
                    && TryParseDecimal(rule.Min, out var min)
                    && TryParseDecimal(rule.Max, out var max)
                    && (num < min || num > max))
                {
                    failures.Add(new ValidationFailure(propertyName,
                        rule.Message ?? $"{label} must be between {min} and {max}."));
                }
                break;

            case ValidationRuleTypes.Regex:
                if (!string.IsNullOrWhiteSpace(rule.Value))
                {
                    try
                    {
                        if (!Regex.IsMatch(strValue, rule.Value))
                        {
                            failures.Add(new ValidationFailure(propertyName,
                                rule.Message ?? $"{label} format is invalid."));
                        }
                    }
                    catch (ArgumentException)
                    {
                        failures.Add(new ValidationFailure(propertyName,
                            rule.Message ?? $"{label} has an invalid validation pattern."));
                    }
                }
                break;

            case ValidationRuleTypes.Email:
                if (!IsValidEmail(strValue))
                {
                    failures.Add(new ValidationFailure(propertyName,
                        rule.Message ?? $"{label} must be a valid email address."));
                }
                break;

            case ValidationRuleTypes.Phone:
                if (!PhonePattern.IsMatch(strValue))
                {
                    failures.Add(new ValidationFailure(propertyName,
                        rule.Message ?? $"{label} must be a valid phone number."));
                }
                break;

            case ValidationRuleTypes.Url:
                if (!UrlPattern.IsMatch(strValue))
                {
                    failures.Add(new ValidationFailure(propertyName,
                        rule.Message ?? $"{label} must be a valid URL."));
                }
                break;

            case ValidationRuleTypes.CompareField:
                ApplyCompareFieldRule(propertyName, label, rule, rawValue, strValue, data, failures);
                break;
        }
    }

    private static void ApplyCompareFieldRule(
        string propertyName,
        string label,
        FieldValidationRuleDefinition rule,
        object? rawValue,
        string strValue,
        Dictionary<string, object?> data,
        List<ValidationFailure> failures)
    {
        if (string.IsNullOrWhiteSpace(rule.OtherField))
            return;

        if (!data.TryGetValue(rule.OtherField, out var otherRaw))
            return;

        var otherStr = DynamicEntityMapper.ToStringValue(otherRaw);
        if (string.IsNullOrWhiteSpace(otherStr))
            return;

        var op = rule.Operator?.Trim().ToLowerInvariant() ?? "equal";
        var message = rule.Message ?? BuildCompareMessage(label, rule.OtherField, op);

        if (TryParseComparable(rawValue, strValue, out var left)
            && TryParseComparable(otherRaw, otherStr, out var right))
        {
            if (!CompareValues(left, right, op))
                failures.Add(new ValidationFailure(propertyName, message));
            return;
        }

        if (!CompareStrings(strValue, otherStr, op))
            failures.Add(new ValidationFailure(propertyName, message));
    }

    private static bool CompareValues(IComparable left, IComparable right, string op)
    {
        var comparison = left.CompareTo(right);
        return op switch
        {
            "gt" => comparison > 0,
            "gte" => comparison >= 0,
            "lt" => comparison < 0,
            "lte" => comparison <= 0,
            "equal" => comparison == 0,
            "notequal" => comparison != 0,
            _ => comparison == 0
        };
    }

    private static bool CompareStrings(string left, string right, string op) =>
        op switch
        {
            "gt" => string.Compare(left, right, StringComparison.OrdinalIgnoreCase) > 0,
            "gte" => string.Compare(left, right, StringComparison.OrdinalIgnoreCase) >= 0,
            "lt" => string.Compare(left, right, StringComparison.OrdinalIgnoreCase) < 0,
            "lte" => string.Compare(left, right, StringComparison.OrdinalIgnoreCase) <= 0,
            "equal" => string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
            "notequal" => !string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
            _ => string.Equals(left, right, StringComparison.OrdinalIgnoreCase)
        };

    private static bool TryParseComparable(object? rawValue, string strValue, out IComparable comparable)
    {
        comparable = strValue;

        if (rawValue is DateTime dt)
        {
            comparable = dt;
            return true;
        }

        if (rawValue is DateOnly dateOnly)
        {
            comparable = dateOnly;
            return true;
        }

        if (TryParseDecimal(strValue, out var number))
        {
            comparable = number;
            return true;
        }

        if (DateTime.TryParse(strValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedDate))
        {
            comparable = parsedDate;
            return true;
        }

        if (DateOnly.TryParse(strValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDateOnly))
        {
            comparable = parsedDateOnly;
            return true;
        }

        return !string.IsNullOrWhiteSpace(strValue);
    }

    private static bool TryParseDecimal(string? value, out decimal result) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);

    private static bool IsValidEmail(string value)
    {
        var atIndex = value.IndexOf('@');
        if (atIndex <= 0 || atIndex >= value.Length - 1)
            return false;

        var dotIndex = value.LastIndexOf('.');
        return dotIndex > atIndex + 1 && dotIndex < value.Length - 1;
    }

    private static string BuildCompareMessage(string label, string otherField, string op) =>
        op switch
        {
            "gt" => $"{label} must be greater than {otherField}.",
            "gte" => $"{label} must be greater than or equal to {otherField}.",
            "lt" => $"{label} must be less than {otherField}.",
            "lte" => $"{label} must be less than or equal to {otherField}.",
            "equal" => $"{label} must equal {otherField}.",
            "notequal" => $"{label} must not equal {otherField}.",
            _ => $"{label} must match {otherField}."
        };

    private static string SummarizeRule(FieldValidationRuleDefinition rule) =>
        rule.Type?.ToLowerInvariant() switch
        {
            ValidationRuleTypes.MaxLength => $"Max {rule.Value} chars",
            ValidationRuleTypes.MinLength => $"Min {rule.Value} chars",
            ValidationRuleTypes.Range => $"Range {rule.Min}-{rule.Max}",
            ValidationRuleTypes.Email => "Email",
            ValidationRuleTypes.Phone => "Phone",
            ValidationRuleTypes.Url => "URL",
            ValidationRuleTypes.Regex => "Pattern",
            ValidationRuleTypes.CompareField => $"Compare {rule.Operator} {rule.OtherField}",
            ValidationRuleTypes.Unique => SummarizeUniqueRule(rule),
            _ => rule.Type ?? "Rule"
        };

    private static string SummarizeUniqueRule(FieldValidationRuleDefinition rule)
    {
        var columns = ResolveUniqueColumns(rule, string.Empty).Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
        return columns.Count > 0
            ? $"Unique ({string.Join(", ", columns)})"
            : "Unique";
    }
}
