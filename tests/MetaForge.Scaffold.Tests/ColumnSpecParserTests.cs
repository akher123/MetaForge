using MetaForge.Scaffold.Generation;
using Xunit;

namespace MetaForge.Scaffold.Tests;

public class ColumnSpecParserTests
{
    [Fact]
    public void Parse_BuildsTableWithIdAndColumns()
    {
        var table = ColumnSpecParser.Parse("Warehouse", "Warehouses", "Code:string:50!, Name:string:200");

        Assert.Equal("Warehouse", table.EntityName);
        Assert.Equal("Warehouses", table.TableName);
        Assert.Contains(table.Columns, c => c.Name == "Id" && c.IsPrimaryKey);
        Assert.Contains(table.Columns, c => c.Name == "Code" && c.MaxLength == 50 && !c.IsNullable);
        Assert.Contains(table.Columns, c => c.Name == "Name" && c.MaxLength == 200);
    }

    [Fact]
    public void DefaultTableName_PluralizesEntity()
    {
        Assert.Equal("Warehouses", ColumnSpecParser.DefaultTableName("Warehouse"));
    }
}
