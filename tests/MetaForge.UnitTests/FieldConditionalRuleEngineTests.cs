using MetaForge.Application.Validation;
using MetaForge.Infrastructure.Validation;

namespace MetaForge.UnitTests;

public class FieldConditionalRuleEngineTests
{
    [Fact]
    public void EvaluateEffectiveState_ShowRule_MakesHiddenFieldVisible()
    {
        const string conditionalRule = """
            {"rules":[{"action":"show","sourceField":"DeliveryType","operator":"equals","value":"Ship"}]}
            """;

        var data = new Dictionary<string, object?> { ["DeliveryType"] = "Ship" };
        var state = FieldConditionalRuleEngine.EvaluateEffectiveState(
            isVisible: false,
            isRequired: false,
            isReadOnly: false,
            conditionalRule,
            data);

        Assert.True(state.IsVisible);
    }

    [Fact]
    public void EvaluateEffectiveState_HideRule_HidesVisibleField()
    {
        const string conditionalRule = """
            {"rules":[{"action":"hide","sourceField":"Status","operator":"equals","value":"Closed"}]}
            """;

        var data = new Dictionary<string, object?> { ["Status"] = "Closed" };
        var state = FieldConditionalRuleEngine.EvaluateEffectiveState(
            isVisible: true,
            isRequired: false,
            isReadOnly: false,
            conditionalRule,
            data);

        Assert.False(state.IsVisible);
    }

    [Fact]
    public void EvaluateEffectiveState_RequireRule_MakesFieldRequired()
    {
        const string conditionalRule = """
            {"rules":[{"action":"require","sourceField":"Country","operator":"equals","value":"US"}]}
            """;

        var data = new Dictionary<string, object?> { ["Country"] = "US" };
        var state = FieldConditionalRuleEngine.EvaluateEffectiveState(
            isVisible: true,
            isRequired: false,
            isReadOnly: false,
            conditionalRule,
            data);

        Assert.True(state.IsRequired);
    }

    [Fact]
    public void EvaluateEffectiveState_DisableRule_MakesFieldReadOnly()
    {
        const string conditionalRule = """
            {"rules":[{"action":"disable","sourceField":"IsApproved","operator":"equals","value":"true"}]}
            """;

        var data = new Dictionary<string, object?> { ["IsApproved"] = true };
        var state = FieldConditionalRuleEngine.EvaluateEffectiveState(
            isVisible: true,
            isRequired: false,
            isReadOnly: false,
            conditionalRule,
            data);

        Assert.True(state.IsReadOnly);
    }

    [Fact]
    public void EvaluateEffectiveState_EmptyOperator_MatchesNullValue()
    {
        const string conditionalRule = """
            {"rules":[{"action":"hide","sourceField":"Notes","operator":"empty"}]}
            """;

        var data = new Dictionary<string, object?> { ["Notes"] = null };
        var state = FieldConditionalRuleEngine.EvaluateEffectiveState(
            isVisible: true,
            isRequired: false,
            isReadOnly: false,
            conditionalRule,
            data);

        Assert.False(state.IsVisible);
    }

    [Fact]
    public void EvaluateEffectiveState_LaterRulesOverrideEarlierOnes()
    {
        const string conditionalRule = """
            {"rules":[
                {"action":"show","sourceField":"Type","operator":"equals","value":"A"},
                {"action":"hide","sourceField":"Type","operator":"equals","value":"A"}
            ]}
            """;

        var data = new Dictionary<string, object?> { ["Type"] = "A" };
        var state = FieldConditionalRuleEngine.EvaluateEffectiveState(
            isVisible: false,
            isRequired: false,
            isReadOnly: false,
            conditionalRule,
            data);

        Assert.False(state.IsVisible);
    }

    [Fact]
    public void Serialize_RoundTripsRuleSet()
    {
        var ruleSet = new FieldConditionalRuleSet
        {
            Rules =
            [
                new FieldConditionalRuleDefinition
                {
                    Action = ConditionalRuleActions.Show,
                    SourceField = "DeliveryType",
                    Operator = ConditionalRuleOperators.Equal,
                    Value = "Ship"
                }
            ]
        };

        var json = FieldConditionalRuleEngine.Serialize(ruleSet);
        var parsed = FieldConditionalRuleEngine.Parse(json);

        Assert.NotNull(parsed);
        Assert.Single(parsed!.Rules);
        Assert.Equal(ConditionalRuleActions.Show, parsed.Rules[0].Action);
        Assert.Equal("DeliveryType", parsed.Rules[0].SourceField);
    }
}
