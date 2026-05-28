using FluentValidation.Results;
using MetaForge.Application.Validation;
using MetaForge.Infrastructure.Validation;

namespace MetaForge.UnitTests;

public class FieldValidationRuleEngineTests
{
    [Fact]
    public void Parse_LegacyMaxLengthRule_ParsesCorrectly()
    {
        var ruleSet = FieldValidationRuleEngine.Parse("MaxLength:50");

        Assert.NotNull(ruleSet);
        Assert.Single(ruleSet!.Rules);
        Assert.Equal(ValidationRuleTypes.MaxLength, ruleSet.Rules[0].Type);
        Assert.Equal("50", ruleSet.Rules[0].Value);
    }

    [Fact]
    public void Parse_JsonRuleSet_ParsesCorrectly()
    {
        const string json = """{"rules":[{"type":"email"},{"type":"maxLength","value":"100"}]}""";

        var ruleSet = FieldValidationRuleEngine.Parse(json);

        Assert.NotNull(ruleSet);
        Assert.Equal(2, ruleSet!.Rules.Count);
        Assert.Equal(ValidationRuleTypes.Email, ruleSet.Rules[0].Type);
        Assert.Equal(ValidationRuleTypes.MaxLength, ruleSet.Rules[1].Type);
    }

    [Fact]
    public void ApplyRules_MaxLength_AddsFailureWhenExceeded()
    {
        var failures = new List<ValidationFailure>();
        var data = new Dictionary<string, object?> { ["Code"] = "ABCDEF" };

        FieldValidationRuleEngine.ApplyRules(
            "Code",
            "Code",
            FieldValidationRuleEngine.Serialize(new FieldValidationRuleSet
            {
                Rules = [new FieldValidationRuleDefinition { Type = ValidationRuleTypes.MaxLength, Value = "3" }]
            }),
            data,
            failures);

        Assert.Single(failures);
        Assert.Contains("must not exceed 3", failures[0].ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyRules_Range_AddsFailureWhenOutOfBounds()
    {
        var failures = new List<ValidationFailure>();
        var data = new Dictionary<string, object?> { ["Quantity"] = "150" };

        FieldValidationRuleEngine.ApplyRules(
            "Quantity",
            "Quantity",
            FieldValidationRuleEngine.Serialize(new FieldValidationRuleSet
            {
                Rules =
                [
                    new FieldValidationRuleDefinition
                    {
                        Type = ValidationRuleTypes.Range,
                        Min = "1",
                        Max = "100"
                    }
                ]
            }),
            data,
            failures);

        Assert.Single(failures);
    }

    [Fact]
    public void ApplyRules_CompareField_AddsFailureWhenLessThanOtherField()
    {
        var failures = new List<ValidationFailure>();
        var data = new Dictionary<string, object?>
        {
            ["StartDate"] = "2026-01-10",
            ["EndDate"] = "2026-01-01"
        };

        FieldValidationRuleEngine.ApplyRules(
            "EndDate",
            "End Date",
            FieldValidationRuleEngine.Serialize(new FieldValidationRuleSet
            {
                Rules =
                [
                    new FieldValidationRuleDefinition
                    {
                        Type = ValidationRuleTypes.CompareField,
                        OtherField = "StartDate",
                        Operator = "gte"
                    }
                ]
            }),
            data,
            failures);

        Assert.Single(failures);
    }

    [Fact]
    public void ApplyRules_EmailLegacyFormat_StillWorks()
    {
        var failures = new List<ValidationFailure>();
        var data = new Dictionary<string, object?> { ["Email"] = "not-an-email" };

        FieldValidationRuleEngine.ApplyRules("Email", "Email", "Email", data, failures);

        Assert.Single(failures);
    }

    [Fact]
    public void Summarize_ReturnsReadableText()
    {
        const string json = """{"rules":[{"type":"maxLength","value":"50"},{"type":"email"}]}""";

        var summary = FieldValidationRuleEngine.Summarize(json);

        Assert.Contains("Max 50 chars", summary, StringComparison.Ordinal);
        Assert.Contains("Email", summary, StringComparison.Ordinal);
    }
}
