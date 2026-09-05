using MetaForge.Scaffold.Module;
using Xunit;

namespace MetaForge.Scaffold.Tests;

public class ModuleScaffoldOrchestratorTests
{
    [Fact]
    public async Task RunAsync_DryRun_ListsPlannedFilesWithoutWriting()
    {
        var root = FindSolutionRoot();
        var options = new ModuleScaffoldOptions
        {
            SolutionRoot = root,
            ModuleName = "TestModuleDryRun",
            DryRun = true,
            CreateWebArea = true
        };

        var result = await new ModuleScaffoldOrchestrator().RunAsync(options);

        Assert.Equal("TestModuleDryRun", result.ModuleName);
        Assert.Equal("testmoduledryrun", result.SchemaName);
        Assert.True(result.DryRun);
        Assert.NotEmpty(result.PlannedFiles);
        Assert.Contains(result.PlannedFiles, p => p.Contains("TestModuleDryRunDbContext.cs", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.PlannedFiles, p => p.Contains("src\\Modules\\TestModuleDryRun", StringComparison.OrdinalIgnoreCase)
            || p.Contains("src/Modules/TestModuleDryRun", StringComparison.OrdinalIgnoreCase));
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
