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
    public void GetSearchablePaths_IncludesAllTemplateStringFields()
    {
        var display = LookupDisplayExpression.Create(typeof(VehicleMake), "{Code} - {Name}");

        var searchable = display.GetSearchablePaths(typeof(VehicleMake));

        Assert.Equal(2, searchable.Count);
        Assert.Contains(searchable, p => p.Raw == "Code");
        Assert.Contains(searchable, p => p.Raw == "Name");
    }

    [Fact]
    public void Format_UsesNavigationPropertyPath()
    {
        var display = LookupDisplayExpression.Create(typeof(VehicleAssignment), "Vehicle.VehicleNumber");
        var entity = new VehicleAssignment
        {
            Vehicle = new Vehicle { VehicleNumber = "VH-001", Name = "Fleet Truck" }
        };

        Assert.Equal("VH-001", display.Format(entity, typeof(VehicleAssignment)));
    }

    [Fact]
    public void Format_UsesNavigationPropertyTemplate()
    {
        var display = LookupDisplayExpression.Create(typeof(VehicleAssignment), "{Vehicle.VehicleNumber} - {Vehicle.Name}");
        var entity = new VehicleAssignment
        {
            Vehicle = new Vehicle { VehicleNumber = "VH-001", Name = "Fleet Truck" }
        };

        Assert.Equal("VH-001 - Fleet Truck", display.Format(entity, typeof(VehicleAssignment)));
    }

    [Fact]
    public void GetIncludePaths_ReturnsNavigationPrefix()
    {
        var display = LookupDisplayExpression.Create(typeof(VehicleAssignment), "Vehicle.VehicleNumber");

        var includes = display.GetIncludePaths(typeof(VehicleAssignment));

        Assert.Single(includes);
        Assert.Equal("Vehicle", includes[0]);
    }
}
