using MetaForge.Scaffold;
using MetaForge.Scaffold.Module;
using Xunit;

namespace MetaForge.Scaffold.Tests;

public class ModulePathResolverTests
{
    [Fact]
    public void GetRelativeProjectReference_FromModulesFolder_ReachesCoreAndShared()
    {
        var root = @"D:\Nextframwork";
        var naming = ModuleNaming.Parse("Inventory");

        var domainDir = Path.Combine(root, naming.ModuleFolder, naming.DomainProject);
        var coreDomain = ModulePathResolver.ResolveFromSolutionRoot(root, ScaffoldConstants.CoreDomainProject);
        var shared = ModulePathResolver.ResolveFromSolutionRoot(root, ScaffoldConstants.SharedProject);

        var coreRef = ModulePathResolver.GetRelativeProjectReference(domainDir, coreDomain);
        var sharedRef = ModulePathResolver.GetRelativeProjectReference(domainDir, shared);

        Assert.Equal("../../../Core/MetaForge.Core.Domain/MetaForge.Core.Domain.csproj", coreRef);
        Assert.Equal("../../../MetaForge.Shared/MetaForge.Shared.csproj", sharedRef);
    }

    [Fact]
    public void GetRelativeProjectReference_FromLegacyHrmFolder_ReachesCore()
    {
        var root = @"D:\Nextframwork";
        var naming = ModuleNaming.FromConfig("Hrm", "src/Hrm");

        var domainDir = Path.Combine(root, naming.ModuleFolder, naming.DomainProject);
        var coreDomain = ModulePathResolver.ResolveFromSolutionRoot(root, ScaffoldConstants.CoreDomainProject);
        var coreRef = ModulePathResolver.GetRelativeProjectReference(domainDir, coreDomain);

        Assert.Equal("../../Core/MetaForge.Core.Domain/MetaForge.Core.Domain.csproj", coreRef);
    }

    [Fact]
    public void GetRelativeProjectReferenceFromModuleProject_UsesSolutionRoot()
    {
        var root = @"D:\Nextframwork";
        var naming = ModuleNaming.Parse("Inventory");
        var coreDomain = ModulePathResolver.ResolveFromSolutionRoot(root, ScaffoldConstants.CoreDomainProject);

        var coreRef = ModulePathResolver.GetRelativeProjectReferenceFromModuleProject(
            root, naming, naming.DomainProject, coreDomain);

        Assert.Equal("../../../Core/MetaForge.Core.Domain/MetaForge.Core.Domain.csproj", coreRef);
    }
}
