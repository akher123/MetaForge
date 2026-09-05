using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MetaForge.Modules.Abstractions;

/// <summary>
/// Contract for a pluggable business module (Hrm, Accounting, Inventory, …).
/// </summary>
public interface IMetaForgeModule
{
    string Name { get; }

    string AreaName { get; }

    string SchemaName { get; }

    Type DbContextType { get; }

    Assembly InfrastructureAssembly { get; }

    void RegisterServices(IServiceCollection services, IConfiguration configuration);
}

/// <summary>
/// Resolves the EF Core DbContext that owns a given entity or module.
/// </summary>
public interface IModuleDbContextResolver
{
    DbContext ResolveForEntity(string entityName);

    DbContext ResolveForModule(string moduleName);

    DbContext CoreContext { get; }

    IReadOnlyList<IMetaForgeModule> EnabledModules { get; }

    IReadOnlyList<Type> GetAllFeatureEntityTypes();
}
