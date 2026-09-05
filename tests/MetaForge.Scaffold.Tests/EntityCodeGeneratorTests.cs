using MetaForge.Scaffold.Config;
using MetaForge.Scaffold.Generation;
using MetaForge.Scaffold.Models;
using Xunit;

namespace MetaForge.Scaffold.Tests;

public class EntityCodeGeneratorTests
{
    private static ModuleScaffoldProfile HrmProfile =>
        MetaForgeConfigLoader.ResolveModule(
            MetaForgeConfigLoader.Load(FindSolutionRoot()),
            "Hrm",
            FindSolutionRoot());

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "metaforge.json")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate solution root with metaforge.json.");
    }

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

        var code = EntityCodeGenerator.Generate(table, HrmProfile, includeNavigations: false);

        Assert.Contains("namespace MetaForge.Hrm.Domain.Entities;", code);
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

        var code = EntityCodeGenerator.Generate(table, HrmProfile, includeNavigations: false);

        Assert.Contains("public class ExternalOrder : BaseEntity<Guid>", code);
        Assert.DoesNotContain("public Guid Id", code);
    }

    [Fact]
    public void Configuration_GeneratesModuleSchemaAndNamespace()
    {
        var table = new TableModel
        {
            TableName = "LeaveRequests",
            EntityName = "LeaveRequest",
            Columns =
            [
                new ColumnModel { Name = "Id", ClrTypeName = "int", IsPrimaryKey = true, IsNullable = false },
                new ColumnModel { Name = "Code", ClrTypeName = "string", IsNullable = false, MaxLength = 20 }
            ]
        };

        var code = ConfigurationCodeGenerator.Generate(table, HrmProfile, includeNavigations: false);

        Assert.Contains("namespace MetaForge.Hrm.Infrastructure.Persistence.Configurations.Generated;", code);
        Assert.Contains("builder.ToTable(\"LeaveRequests\", \"hrm\");", code);
    }
}
