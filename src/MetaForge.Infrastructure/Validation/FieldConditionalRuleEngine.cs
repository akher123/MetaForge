using System.Globalization;
using System.Text.Json;
using MetaForge.Application.Validation;
using MetaForge.Domain.Metadata;
using MetaForge.Infrastructure.Dynamic;

namespace MetaForge.Infrastructure.Validation;

/// <summary>
/// Parses and evaluates field conditional rules for server-side validation.
/// </summary>
public static class FieldConditionalRuleEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static FieldConditionalRuleSet? Parse(string? conditionalRule)
    {
        if (string.IsNullOrWhiteSpace(conditionalRule))
            return null;

        conditionalRule = conditionalRule.Trim();
        if (!conditionalRule.StartsWith('{'))
            return null;

        try
        {
            var parsed = JsonSerializer.Deserialize<FieldConditionalRuleSet>(conditionalRule, JsonOptions);
            NormalizeRuleSet(parsed);
            return parsed;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string Serialize(FieldConditionalRuleSet ruleSet) =>
        JsonSerializer.Serialize(ruleSet, JsonOptions);

    public static string Summarize(string? conditionalRule)
    {
        var ruleSet = Parse(conditionalRule);
        if (ruleSet?.Rules == null || ruleSet.Rules.Count == 0)
            return string.Empty;

        return string.Join(", ", ruleSet.Rules.Select(SummarizeRule));
    }

    public static FieldEffectiveState EvaluateEffectiveState(
        ForgeField field,
        IReadOnlyDictionary<string, object?> data)
    {
        var state = new FieldEffectiveState
        {
            IsVisible = field.IsVisible,
            IsRequired = field.IsRequired,
            IsReadOnly = field.IsReadOnly
        };

        var ruleSet = Parse(field.ConditionalRule);
        if (ruleSet?.Rules == null)
            return state;

        foreach (var rule in ruleSet.Rules)
        {
            if (!EvaluateCondition(rule, data))
                continue;

            ApplyAction(state, rule.Action);
        }

        return state;
    }

    public static FieldEffectiveState EvaluateEffectiveState(
        bool isVisible,
        bool isRequired,
        bool isReadOnly,
        string? conditionalRule,
        IReadOnlyDictionary<string, object?> data)
    {
        var state = new FieldEffectiveState
        {
            IsVisible = isVisible,
            IsRequired = isRequired,
            IsReadOnly = isReadOnly
        };

        var ruleSet = Parse(conditionalRule);
        if (ruleSet?.Rules == null)
            return state;

        foreach (var rule in ruleSet.Rules)
        {
            if (!EvaluateCondition(rule, data))
                continue;

            ApplyAction(state, rule.Action);
        }

        return state;
    }

    private static void NormalizeRuleSet(FieldConditionalRuleSet? ruleSet)
    {
        if (ruleSet?.Rules == null)
            return;

        foreach (var rule in ruleSet.Rules)
        {
            if (!string.IsNullOrWhiteSpace(rule.Action))
                rule.Action = rule.Action.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(rule.Operator))
                rule.Operator = rule.Operator.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(rule.SourceField))
                rule.SourceField = rule.SourceField.Trim();
        }
    }

    private static void ApplyAction(FieldEffectiveState state, string? action)
    {
        switch (action?.Trim().ToLowerInvariant())
        {
            case ConditionalRuleActions.Show:
                state.IsVisible = true;
                break;
            case ConditionalRuleActions.Hide:
                state.IsVisible = false;
                break;
            case ConditionalRuleActions.Enable:
                state.IsReadOnly = false;
                break;
            case ConditionalRuleActions.Disable:
                state.IsReadOnly = true;
                break;
            case ConditionalRuleActions.Require:
                state.IsRequired = true;
                break;
            case ConditionalRuleActions.Optional:
                state.IsRequired = false;
                break;
        }
    }

    private static bool EvaluateCondition(FieldConditionalRuleDefinition rule, IReadOnlyDictionary<string, object?> data)
    {
        if (string.IsNullOrWhiteSpace(rule.SourceField))
            return false;

        data.TryGetValue(rule.SourceField, out var rawValue);
        var strValue = DynamicEntityMapper.ToStringValue(rawValue) ?? string.Empty;
        var op = rule.Operator?.Trim().ToLowerInvariant() ?? ConditionalRuleOperators.Equal;
        var compareValue = rule.Value ?? string.Empty;

        return op switch
        {
            ConditionalRuleOperators.Empty => string.IsNullOrWhiteSpace(strValue),
            ConditionalRuleOperators.NotEmpty => !string.IsNullOrWhiteSpace(strValue),
            ConditionalRuleOperators.Contains => strValue.Contains(compareValue, StringComparison.OrdinalIgnoreCase),
            ConditionalRuleOperators.NotEqual => !EqualsValues(strValue, compareValue, rawValue),
            ConditionalRuleOperators.GreaterThan => CompareNumeric(strValue, compareValue, rawValue, op),
            ConditionalRuleOperators.GreaterThanOrEqual => CompareNumeric(strValue, compareValue, rawValue, op),
            ConditionalRuleOperators.LessThan => CompareNumeric(strValue, compareValue, rawValue, op),
            ConditionalRuleOperators.LessThanOrEqual => CompareNumeric(strValue, compareValue, rawValue, op),
            _ => EqualsValues(strValue, compareValue, rawValue)
        };
    }

    private static bool EqualsValues(string strValue, string compareValue, object? rawValue)
    {
        if (bool.TryParse(compareValue, out var boolCompare))
        {
            if (rawValue is bool boolRaw)
                return boolRaw == boolCompare;

            if (bool.TryParse(strValue, out var boolValue))
                return boolValue == boolCompare;
        }

        if (decimal.TryParse(strValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var leftNum)
            && decimal.TryParse(compareValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var rightNum))
        {
            return leftNum == rightNum;
        }

        return string.Equals(strValue, compareValue, StringComparison.OrdinalIgnoreCase);
    }

    private static bool CompareNumeric(string strValue, string compareValue, object? rawValue, string op)
    {
        if (!decimal.TryParse(strValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var left)
            || !decimal.TryParse(compareValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var right))
        {
            return false;
        }

        return op switch
        {
            ConditionalRuleOperators.GreaterThan => left > right,
            ConditionalRuleOperators.GreaterThanOrEqual => left >= right,
            ConditionalRuleOperators.LessThan => left < right,
            ConditionalRuleOperators.LessThanOrEqual => left <= right,
            _ => false
        };
    }

    private static string SummarizeRule(FieldConditionalRuleDefinition rule)
    {
        var action = rule.Action ?? "rule";
        var source = rule.SourceField ?? "?";
        var op = rule.Operator ?? ConditionalRuleOperators.Equal;

        if (op is ConditionalRuleOperators.Empty or ConditionalRuleOperators.NotEmpty)
            return $"{action} when {source} {op}";

        return $"{action} when {source} {op} {rule.Value}";
    }
}
