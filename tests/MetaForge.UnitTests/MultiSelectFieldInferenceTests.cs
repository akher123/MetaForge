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

    [Fact]
    public void DiscoverJunctionFields_FindsCustomerRegionMapping()
    {
        var options = new DbContextOptionsBuilder<MetaForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new MetaForgeDbContext(options);
        var metadata = new EntityMetadataDto
        {
            EntityName = "Customer",
            Properties =
            [
                new EntityPropertyMetadataDto { Name = "CountryId", IsForeignKey = true }
            ],
            Relations =
            [
                new EntityRelationMetadataDto
                {
                    RelationType = "ManyToOne",
                    ParentEntity = "Country",
                    ChildEntity = "Customer",
                    ForeignKey = "CountryId"
                },
                new EntityRelationMetadataDto
                {
                    RelationType = "ManyToOne",
                    ParentEntity = "Region",
                    ChildEntity = "Customer",
                    ForeignKey = "RegionId"
                }
            ]
        };

        var fields = MultiSelectFieldInference.DiscoverJunctionFields(context, metadata);

        var regionField = Assert.Single(fields, f => f.PropertyName == "RegionIds");
        Assert.Equal(ControlType.MultiSelect, regionField.ControlType);
        Assert.Equal("Region", regionField.LookupEntity);
        Assert.Equal("CustomerRegion", regionField.MappingEntity);
        Assert.Equal("CustomerId", regionField.MappingParentKey);
        Assert.Equal("RegionId", regionField.MappingRelatedKey);
        Assert.Equal("CountryId", regionField.LookupParentField);
    }

    [Fact]
    public void ApplyDefaults_FillsMissingMappingFromPropertyName()
    {
        var options = new DbContextOptionsBuilder<MetaForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new MetaForgeDbContext(options);
        var metadata = new EntityMetadataDto
        {
            EntityName = "Customer",
            Properties =
            [
                new EntityPropertyMetadataDto { Name = "CountryId", IsForeignKey = true }
            ],
            Relations =
            [
                new EntityRelationMetadataDto
                {
                    ParentEntity = "Region",
                    ChildEntity = "Customer",
                    ForeignKey = "RegionId"
                }
            ]
        };

        var field = new FormFieldConfigDto
        {
            PropertyName = "RegionIds",
            ControlType = ControlType.MultiSelect
        };

        MultiSelectFieldInference.ApplyDefaults(field, metadata, context);

        Assert.Equal("Region", field.LookupEntity);
        Assert.Equal("CustomerRegion", field.MappingEntity);
        Assert.Equal("CustomerId", field.MappingParentKey);
        Assert.Equal("RegionId", field.MappingRelatedKey);
        Assert.Equal("CountryId", field.LookupParentField);
    }
}
