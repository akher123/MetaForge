namespace MetaForge.UnitTests;

public class MultiSelectFieldInferenceTests
{
    [Theory]
    [InlineData("RegionIds", "Region", true)]
    [InlineData("TagIds", "Tag", true)]
    [InlineData("RegionId", "", false)]
    [InlineData("Ids", "", false)]
    public void TryParseRelatedEntityName_ParsesPluralIdSuffix(string propertyName, string expected, bool expectedResult)
    {
        var result = MultiSelectFieldInference.TryParseRelatedEntityName(propertyName, out var relatedEntity);
        Assert.Equal(expectedResult, result);
        Assert.Equal(expected, relatedEntity);
    }
}
