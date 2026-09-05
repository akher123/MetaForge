using MetaForge.Scaffold;
using MetaForge.Scaffold.Config;
using Xunit;

namespace MetaForge.Scaffold.Tests;

public class MetaForgeConfigLoaderTests
{
    [Fact]
    public void ResolveModule_Hrm_ProducesExpectedPaths()
    {
        var root = FindSolutionRoot();
        var config = MetaForgeConfigLoader.Load(root);
        var profile = MetaForgeConfigLoader.ResolveModule(config, "Hrm", root);

        Assert.Equal("Hrm", profile.Name);
        Assert.Equal("hrm", profile.SchemaName);
        Assert.Equal("MetaForge.Hrm.Domain.Entities", profile.EntityNamespace);
        Assert.Equal("src/Modules/Hrm/MetaForge.Hrm.Domain/Entities", profile.DomainOutputDir);
        Assert.Contains("MetaForge.Hrm.Infrastructure", profile.InfrastructureProjectPath);
        Assert.Equal("HrmDbContext", profile.DbContextName);
        Assert.Equal("src/Modules/Hrm/MetaForge.Hrm.Infrastructure/Persistence/HrmDbContext.cs", profile.DbContextPath);
        Assert.True(profile.PatchDbSet);
    }

    [Fact]
    public void ApplyToOptions_SetsModuleDbContextPath()
    {
        var root = FindSolutionRoot();
        var config = MetaForgeConfigLoader.Load(root);
        var profile = MetaForgeConfigLoader.ResolveModule(config, "Hrm", root);
        var options = new ScaffoldOptions();

        MetaForgeConfigLoader.ApplyToOptions(options, profile, root);

        Assert.Equal(profile.DbContextPath, options.DbContextPath);
        Assert.Equal("HrmDbContext", options.DbContextName);
        Assert.False(options.NoDbSetPatch);
    }

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
}
