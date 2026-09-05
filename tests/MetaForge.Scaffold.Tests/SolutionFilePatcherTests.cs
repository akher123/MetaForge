using MetaForge.Scaffold.Module;
using MetaForge.Scaffold.Patching;
using Xunit;

namespace MetaForge.Scaffold.Tests;

public class SolutionFilePatcherTests
{
    private const string SampleSlnx =
        """
        <Solution>
          <Folder Name="/src/Modules/Hrm/">
            <Project Path="src/Hrm/MetaForge.Hrm.Domain/MetaForge.Hrm.Domain.csproj" />
          </Folder>
          <Folder Name="/src/Hosts/">
            <Project Path="src/MetaForge.Web/MetaForge.Web.csproj" />
          </Folder>
        </Solution>
        """;

    [Fact]
    public void TryAddModule_InsertsUnderModulesBeforeHosts()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "MetaForgeScaffoldTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var slnxPath = Path.Combine(tempDir, "MetaForge.slnx");
        File.WriteAllText(slnxPath, SampleSlnx);

        var naming = ModuleNaming.Parse("Inventory");

        var success = SolutionFilePatcher.TryAddModule(slnxPath, naming, out var error);

        Assert.True(success, error);
        var updated = File.ReadAllText(slnxPath);
        Assert.Contains("<Folder Name=\"/src/Modules/Inventory/\">", updated);
        Assert.Contains("src/Modules/Inventory/MetaForge.Inventory.Domain/MetaForge.Inventory.Domain.csproj", updated);
        Assert.True(updated.IndexOf("/src/Modules/Inventory/", StringComparison.Ordinal)
            < updated.IndexOf("/src/Hosts/", StringComparison.Ordinal));
    }

    [Fact]
    public void TryAddModule_WhenFolderExists_ReturnsError()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "MetaForgeScaffoldTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var slnxPath = Path.Combine(tempDir, "MetaForge.slnx");
        File.WriteAllText(slnxPath, SampleSlnx);

        var naming = ModuleNaming.Parse("Hrm");

        var success = SolutionFilePatcher.TryAddModule(slnxPath, naming, out var error);

        Assert.False(success);
        Assert.Contains("already exists", error, StringComparison.OrdinalIgnoreCase);
    }
}
