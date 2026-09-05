using MetaForge.Infrastructure.Persistence;
using MetaForge.Modules.Abstractions;
using MetaForge.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace MetaForge.UnitTests.Support;

internal sealed class TestModuleDbContextResolver : IModuleDbContextResolver
{
    public TestModuleDbContextResolver(MetaForgeDbContext coreContext)
    {
        CoreContext = coreContext;
    }

    public DbContext CoreContext { get; }

    public IReadOnlyList<IMetaForgeModule> EnabledModules => [];

    public DbContext ResolveForEntity(string entityName) => CoreContext;

    public DbContext ResolveForModule(string moduleName) => CoreContext;

    public IReadOnlyList<Type> GetAllFeatureEntityTypes() =>
        CoreContext.Model.GetEntityTypes()
            .Select(t => t.ClrType)
            .Where(t => FeatureDiscoveryConstants.IsFeatureEntityNamespace(t.Namespace))
            .ToList();
}

internal static class TestEntityTypeResolverFactory
{
    public static EntityTypeResolver Create(MetaForgeDbContext context) =>
        new(new TestModuleDbContextResolver(context));
}
