using System.Xml.Linq;
using NetArchTest.Rules;

namespace MetaForge.UnitTests.Architecture;

/// <summary>
/// Enforces modular-monolith layer boundaries documented in docs/architecture/framework-vs-features.md.
/// </summary>
public sealed class LayerDependencyTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void Shared_ProjectFile_ShouldNotReference_OtherProjects()
    {
        var references = GetProjectReferences("src/MetaForge.Shared/MetaForge.Shared.csproj");
        Assert.Empty(references);
    }

    [Fact]
    public void ModulesAbstractions_ProjectFile_ShouldOnlyReference_MinimalDependencies()
    {
        var references = GetProjectReferences("src/MetaForge.Modules.Abstractions/MetaForge.Modules.Abstractions.csproj");
        Assert.Empty(references);
    }

    [Fact]
    public void Web_ProjectFile_ShouldNotReference_DomainProjects()
    {
        var references = GetProjectReferences("src/MetaForge.Web/MetaForge.Web.csproj");

        Assert.DoesNotContain(references, r =>
            r.Contains("Core.Domain", StringComparison.OrdinalIgnoreCase)
            || r.Contains("Hrm.Domain", StringComparison.OrdinalIgnoreCase)
            || r.Contains("Production.Domain", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Web_ProjectFile_ShouldReference_InfrastructureProjects()
    {
        var references = GetProjectReferences("src/MetaForge.Web/MetaForge.Web.csproj");

        Assert.Contains(references, r => r.Contains("Core.Infrastructure", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(references, r => r.Contains("Hrm.Infrastructure", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(references, r => r.Contains("Production.Infrastructure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ModulesAbstractions_ShouldNotReference_ModuleOrCoreDomainAssemblies()
    {
        var result = Types.InAssembly(typeof(MetaForge.Modules.Abstractions.IMetaForgeModule).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "MetaForge.Domain",
                "MetaForge.Hrm.Domain",
                "MetaForge.Production.Domain",
                "MetaForge.Application",
                "MetaForge.Infrastructure",
                "MetaForge.Web")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    [Fact]
    public void CoreInfrastructure_ShouldNotReference_ModuleInfrastructureAssemblies()
    {
        var result = Types.InAssembly(typeof(MetaForge.Infrastructure.DependencyInjection).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "MetaForge.Hrm.Infrastructure",
                "MetaForge.Production.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    [Fact]
    public void FrameworkControllers_ShouldNotReference_DomainAssemblies()
    {
        var result = Types.InAssembly(typeof(MetaForge.Web.Controllers.ModuleController).Assembly)
            .That()
            .ResideInNamespace("MetaForge.Web.Controllers")
            .Or()
            .ResideInNamespace("MetaForge.Web.Controllers.Api")
            .ShouldNot()
            .HaveDependencyOnAny(
                "MetaForge.Domain",
                "MetaForge.Hrm.Domain",
                "MetaForge.Production.Domain")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    private static IReadOnlyList<string> GetProjectReferences(string relativeProjectPath)
    {
        var projectPath = Path.Combine(RepoRoot, relativeProjectPath);
        var document = XDocument.Load(projectPath);

        return document.Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
    }

    private static string FormatFailures(TestResult result) =>
        result.IsSuccessful
            ? string.Empty
            : string.Join(Environment.NewLine, result.FailingTypeNames ?? []);
}
