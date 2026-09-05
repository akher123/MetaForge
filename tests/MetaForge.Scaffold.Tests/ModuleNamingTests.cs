using MetaForge.Scaffold.Module;
using Xunit;

namespace MetaForge.Scaffold.Tests;

public class ModuleNamingTests
{
    [Fact]
    public void Parse_Production_DerivesExpectedNames()
    {
        var naming = ModuleNaming.Parse("Production");

        Assert.Equal("Production", naming.Name);
        Assert.Equal("production", naming.SchemaName);
        Assert.Equal("MetaForge.Production.Domain", naming.DomainProject);
        Assert.Equal("ProductionDbContext", naming.DbContextName);
        Assert.Equal("MetaForge.Production.Domain.Entities", naming.EntityNamespace);
        Assert.Equal("AddProductionModule", naming.AddModuleMethodName);
        Assert.Equal("src/Modules/Production", naming.ModuleFolder);
        Assert.Equal("/src/Modules/Production/", naming.SolutionFolderName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("production")]
    [InlineData("123Bad")]
    public void Parse_InvalidName_Throws(string name)
    {
        Assert.ThrowsAny<Exception>(() => ModuleNaming.Parse(name));
    }
}
