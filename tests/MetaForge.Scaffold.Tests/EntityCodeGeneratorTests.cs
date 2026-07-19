using MetaForge.Scaffold.Generation;
using MetaForge.Scaffold.Models;
using Xunit;

namespace MetaForge.Scaffold.Tests;

public class EntityCodeGeneratorTests
{
    [Fact]
    public void Generate_SkipsIdAndUsesBaseEntity()
    {
        var table = new TableModel
        {
            TableName = "Warehouses",
            EntityName = "Warehouse",
            Columns =
            [
                new ColumnModel { Name = "Id", ClrTypeName = "int", IsPrimaryKey = true, IsNullable = false },
                new ColumnModel { Name = "Code", ClrTypeName = "string", IsNullable = false, MaxLength = 50 },
                new ColumnModel { Name = "Notes", ClrTypeName = "string", IsNullable = true }
            ]
        };

        var code = EntityCodeGenerator.Generate(table, includeNavigations: false);

        Assert.Contains("public class Warehouse : BaseEntity", code);
        Assert.DoesNotContain("public int Id", code);
        Assert.Contains("public string Code { get; set; } = string.Empty;", code);
        Assert.Contains("public string? Notes { get; set; }", code);
    }

    [Fact]
    public void Generate_UsesGenericBaseEntity_ForGuidKey()
    {
        var table = new TableModel
        {
            TableName = "ExternalOrders",
            EntityName = "ExternalOrder",
            Columns =
            [
                new ColumnModel { Name = "Id", ClrTypeName = "Guid", IsPrimaryKey = true, IsNullable = false },
                new ColumnModel { Name = "Code", ClrTypeName = "string", IsNullable = false, MaxLength = 50 }
            ]
        };

        var code = EntityCodeGenerator.Generate(table, includeNavigations: false);

        Assert.Contains("public class ExternalOrder : BaseEntity<Guid>", code);
        Assert.DoesNotContain("public Guid Id", code);
    }

    [Fact]
    public void Generate_UsesGenericBaseEntity_ForLongKey()
    {
        var table = new TableModel
        {
            TableName = "BigRows",
            EntityName = "BigRow",
            Columns =
            [
                new ColumnModel { Name = "Id", ClrTypeName = "long", IsPrimaryKey = true, IsNullable = false },
                new ColumnModel { Name = "Name", ClrTypeName = "string", IsNullable = false, MaxLength = 100 }
            ]
        };

        var code = EntityCodeGenerator.Generate(table, includeNavigations: false);

        Assert.Contains("public class BigRow : BaseEntity<long>", code);
    }
}
