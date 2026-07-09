using MetaForge.Application.DTOs;
using MetaForge.Infrastructure.Services;

namespace MetaForge.UnitTests;

public class FormSchemaSyncPlannerTests
{
    [Fact]
    public void BuildPreview_DetectsNewEntityPropertyAsAddField()
    {
        var form = SampleForm(fields: ["OrderNo", "OrderDate"]);
        var metadata = SampleMetadata("SalesOrder", ["OrderNo", "OrderDate", "CustomerId"]);
        var draft = SampleDraft("SalesOrder", ["OrderNo", "OrderDate", "CustomerId"]);

        var preview = FormSchemaSyncPlanner.BuildPreview(form, metadata, draft);

        var addField = Assert.Single(preview.Changes.Where(c =>
            c.ChangeType == FormSchemaSyncChangeTypes.Add && c.Target == FormSchemaSyncTargets.Field));
        Assert.Equal("field:CustomerId", addField.Key);
        Assert.True(addField.SelectedByDefault);
    }

    [Fact]
    public void BuildPreview_DetectsRemovedPropertyAsRemoveField()
    {
        var form = SampleForm(fields: ["OrderNo", "LegacyField"]);
        var metadata = SampleMetadata("SalesOrder", ["OrderNo"]);
        var draft = SampleDraft("SalesOrder", ["OrderNo"]);

        var preview = FormSchemaSyncPlanner.BuildPreview(form, metadata, draft);

        var removeField = Assert.Single(preview.Changes.Where(c =>
            c.ChangeType == FormSchemaSyncChangeTypes.Remove && c.Target == FormSchemaSyncTargets.Field));
        Assert.Equal("field:LegacyField", removeField.Key);
        Assert.False(removeField.SelectedByDefault);
    }

    [Fact]
    public void Apply_AddsSelectedFieldWithoutRemovingExistingConfig()
    {
        var form = SampleForm(fields: ["OrderNo"]);
        form.Fields[0].ConditionalRule = """{"rules":[{"action":"disable","sourceField":"Status","operator":"equals","value":"Approved"}]}""";

        var metadata = SampleMetadata("SalesOrder", ["OrderNo", "Status"]);
        var draft = SampleDraft("SalesOrder", ["OrderNo", "Status"]);
        var preview = FormSchemaSyncPlanner.BuildPreview(form, metadata, draft);

        var merged = FormSchemaSyncPlanner.Apply(form, preview, ["field:Status"]);

        Assert.Equal(2, merged.Fields.Count);
        Assert.Equal("OrderNo", merged.Fields[0].PropertyName);
        Assert.NotNull(merged.Fields[0].ConditionalRule);
        Assert.Equal("Status", merged.Fields[1].PropertyName);
    }

    [Fact]
    public void PrefixKey_AndTryParsePrefixedKey_RoundTrip()
    {
        var prefixed = FormSchemaSyncPlanner.PrefixKey("SalesOrderItem", "field:Quantity");

        Assert.True(FormSchemaSyncPlanner.TryParsePrefixedKey(prefixed, out var entityName, out var localKey));
        Assert.Equal("SalesOrderItem", entityName);
        Assert.Equal("field:Quantity", localKey);
    }

    [Fact]
    public void PrefixChanges_PrefixesAllChangeKeys()
    {
        var changes = new List<FormSchemaSyncChangeDto>
        {
            new() { Key = "field:Qty", ChangeType = FormSchemaSyncChangeTypes.Add },
            new() { Key = "column:Qty", ChangeType = FormSchemaSyncChangeTypes.Add }
        };

        FormSchemaSyncPlanner.PrefixChanges(changes, "SalesOrderItem");

        Assert.Equal("SalesOrderItem|field:Qty", changes[0].Key);
        Assert.Equal("SalesOrderItem|column:Qty", changes[1].Key);
    }

    [Fact]
    public void BuildPreview_DetectsNewGridColumnForMasterForm()
    {
        var form = SampleForm(fields: ["OrderNo", "CustomerId"], columns: ["OrderNo"]);
        var metadata = SampleMetadata("SalesOrder", ["OrderNo", "CustomerId"]);
        var draft = SampleDraft("SalesOrder", ["OrderNo", "CustomerId"]);

        var preview = FormSchemaSyncPlanner.BuildPreview(form, metadata, draft);

        Assert.Contains(preview.Changes, c =>
            c.ChangeType == FormSchemaSyncChangeTypes.Add
            && c.Target == FormSchemaSyncTargets.GridColumn
            && c.Key == "column:CustomerId");
    }

    private static FormConfigDto SampleForm(IEnumerable<string> fields, IEnumerable<string>? columns = null)
    {
        var fieldList = fields.Select((name, i) => new FormFieldConfigDto
        {
            PropertyName = name,
            Label = name,
            ControlType = ControlType.TextBox,
            IsVisible = true,
            DisplayOrder = i
        }).ToList();

        var columnList = (columns ?? fields).Select((name, i) => new FormGridColumnConfigDto
        {
            PropertyName = name,
            Label = name,
            DisplayOrder = i,
            IsVisible = true
        }).ToList();

        return new FormConfigDto
        {
            Id = 1,
            Code = "salesorder",
            Name = "Sales Order",
            EntityName = "SalesOrder",
            TableName = "SalesOrders",
            FormType = FormType.Master.ToString(),
            Fields = fieldList,
            GridColumns = columnList
        };
    }

    private static EntityMetadataDto SampleMetadata(string entityName, IEnumerable<string> properties) =>
        new()
        {
            EntityName = entityName,
            TableName = entityName + "s",
            Properties = properties.Select(name => new EntityPropertyMetadataDto
            {
                Name = name,
                ClrType = "System.String",
                IsNullable = true
            }).ToList()
        };

    private static FormConfigDto SampleDraft(string entityName, IEnumerable<string> properties) =>
        SampleForm(properties, properties);
}
