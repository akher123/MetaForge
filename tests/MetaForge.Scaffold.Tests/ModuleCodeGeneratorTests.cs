using MetaForge.Scaffold.Module;
using Xunit;

namespace MetaForge.Scaffold.Tests;

public class ModuleCodeGeneratorTests
{
    [Fact]
    public void GenerateDbContext_ContainsSchemaAndContextName()
    {
        var naming = ModuleNaming.Parse("Production");
        var source = ModuleCodeGenerator.GenerateDbContext(naming);

        Assert.Contains("class ProductionDbContext", source);
        Assert.Contains("HasDefaultSchema(\"production\")", source);
        Assert.Contains("ApplyConfigurationsFromAssembly", source);
    }

    [Fact]
    public void GenerateDomainProject_ContainsDynamicProjectReferences()
    {
        var naming = ModuleNaming.Parse("Inventory");
        var root = @"D:\Nextframwork";
        var source = ModuleCodeGenerator.GenerateDomainProject(naming, root);

        Assert.DoesNotContain("<RootNamespace>", source);
        Assert.Contains("MetaForge.Core.Domain/MetaForge.Core.Domain.csproj", source);
        Assert.Contains("MetaForge.Shared/MetaForge.Shared.csproj", source);
    }

    [Fact]
    public void GenerateDependencyInjection_ContainsModuleRegistration()
    {
        var naming = ModuleNaming.Parse("Production");
        var source = ModuleCodeGenerator.GenerateDependencyInjection(naming);

        Assert.Contains("class ProductionModule : IMetaForgeModule", source);
        Assert.Contains("AddProductionModule", source);
        Assert.Contains("services.AddSingleton<IMetaForgeModule, ProductionModule>", source);
    }
}
