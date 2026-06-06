using MetaForge.Domain.Features;
using MetaForge.Infrastructure.Dynamic;

namespace MetaForge.UnitTests;

public class LookupDisplayExpressionTests
{
    [Fact]
    public void Format_ConcatenatesCommaSeparatedFields()
    {
        var display = LookupDisplayExpression.Create(typeof(VehicleMake), "Code, Name");
        var entity = new VehicleMake { Code = "TOY", Name = "Toyota" };

        Assert.Equal("TOY - Toyota", display.Format(entity, typeof(VehicleMake)));
    }

    [Fact]
    public void Format_UsesTemplateWithCustomSeparator()
    {
        var display = LookupDisplayExpression.Create(typeof(VehicleMake), "{Code} | {Name}");
        var entity = new VehicleMake { Code = "TOY", Name = "Toyota" };

        Assert.Equal("TOY | Toyota", display.Format(entity, typeof(VehicleMake)));
    }

    [Fact]
    public void Format_UsesVehicleNumberAndNameTemplate()
    {
        var display = LookupDisplayExpression.Create(typeof(Vehicle), "{VehicleNumber} - {Name}");
        var entity = new Vehicle { VehicleNumber = "VH-001", Name = "Fleet Truck" };

        Assert.Equal("VH-001 - Fleet Truck", display.Format(entity, typeof(Vehicle)));
    }

    [Fact]
    public void GetSearchableProperties_IncludesAllTemplateStringFields()
    {
        var display = LookupDisplayExpression.Create(typeof(VehicleMake), "{Code} - {Name}");

        var searchable = display.GetSearchableProperties(typeof(VehicleMake));

        Assert.Equal(2, searchable.Count);
        Assert.Contains(searchable, p => p.Name == "Code");
        Assert.Contains(searchable, p => p.Name == "Name");
    }
}
