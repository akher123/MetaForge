using MetaForge.Scaffold.Schema;
using Xunit;

namespace MetaForge.Scaffold.Tests;

public class TableIdentifierTests
{
    [Fact]
    public void Parse_TableOnly_UsesModuleSchema()
    {
        var id = TableIdentifier.Parse("Departments", "hrm");

        Assert.Equal("hrm", id.Schema);
        Assert.Equal("Departments", id.TableName);
        Assert.Equal("hrm.Departments", id.QualifiedName);
    }

    [Fact]
    public void Parse_SchemaQualifiedTable_ExplicitSchemaWins()
    {
        var id = TableIdentifier.Parse("accounting.Departments", "hrm");

        Assert.Equal("accounting", id.Schema);
        Assert.Equal("Departments", id.TableName);
    }

    [Fact]
    public void Parse_TableOnlyWithoutModuleSchema_FallsBackToDbo()
    {
        var id = TableIdentifier.Parse("Departments", null);

        Assert.Equal("dbo", id.Schema);
        Assert.Equal("Departments", id.TableName);
    }

    [Fact]
    public void Parse_InvalidQualifiedName_Throws()
    {
        Assert.Throws<ArgumentException>(() => TableIdentifier.Parse(".Departments", "hrm"));
        Assert.Throws<ArgumentException>(() => TableIdentifier.Parse("hrm.", "hrm"));
    }

    [Fact]
    public void Parse_EmptyTableName_Throws()
    {
        Assert.Throws<ArgumentException>(() => TableIdentifier.Parse("   ", "hrm"));
    }
}
