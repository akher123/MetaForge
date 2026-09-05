using MetaForge.Modules.Abstractions;
using MetaForge.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MetaForge.Infrastructure.Persistence;

/// <summary>
/// Routes entity operations to the owning module DbContext or the core platform context.
/// </summary>
public sealed class ModuleDbContextResolver : IModuleDbContextResolver
{
    private readonly MetaForgeDbContext _coreContext;
    private readonly IReadOnlyList<IMetaForgeModule> _modules;
    private readonly IServiceProvider _serviceProvider;
    private readonly Lazy<IReadOnlyDictionary<string, IMetaForgeModule>> _entityPrefixMap;

    public ModuleDbContextResolver(
        MetaForgeDbContext coreContext,
        IEnumerable<IMetaForgeModule> modules,
        IServiceProvider serviceProvider)
    {
        _coreContext = coreContext;
        _modules = modules.ToList();
        _serviceProvider = serviceProvider;
        _entityPrefixMap = new Lazy<IReadOnlyDictionary<string, IMetaForgeModule>>(BuildEntityPrefixMap);
    }

    public DbContext CoreContext => _coreContext;

    public IReadOnlyList<IMetaForgeModule> EnabledModules => _modules;

    public DbContext ResolveForEntity(string entityName)
    {
        var entityType = GetAllFeatureEntityTypes()
            .FirstOrDefault(t => string.Equals(t.Name, entityName, StringComparison.OrdinalIgnoreCase));

        if (entityType == null)
        {
            var coreType = _coreContext.Model.GetEntityTypes()
                .FirstOrDefault(t => string.Equals(t.ClrType.Name, entityName, StringComparison.OrdinalIgnoreCase))
                ?.ClrType;

            if (coreType != null)
                return _coreContext;

            throw new NotFoundException($"Entity '{entityName}' was not found in any registered module.");
        }

        return ResolveForEntityType(entityType);
    }

    public DbContext ResolveForModule(string moduleName)
    {
        var module = _modules.FirstOrDefault(m =>
            string.Equals(m.Name, moduleName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(m.AreaName, moduleName, StringComparison.OrdinalIgnoreCase));

        if (module == null)
            throw new NotFoundException($"Module '{moduleName}' is not registered.");

        return (DbContext)_serviceProvider.GetRequiredService(module.DbContextType);
    }

    public IReadOnlyList<Type> GetAllFeatureEntityTypes()
    {
        var types = new List<Type>();

        foreach (var module in _modules)
        {
            var context = (DbContext)_serviceProvider.GetRequiredService(module.DbContextType);
            types.AddRange(context.Model.GetEntityTypes()
                .Select(t => t.ClrType)
                .Where(t => FeatureDiscoveryConstants.IsFeatureEntityNamespace(t.Namespace)));
        }

        // Legacy entities still registered on core context (if any remain during migration).
        types.AddRange(_coreContext.Model.GetEntityTypes()
            .Select(t => t.ClrType)
            .Where(t => FeatureDiscoveryConstants.IsFeatureEntityNamespace(t.Namespace)));

        return types
            .DistinctBy(t => t.FullName, StringComparer.Ordinal)
            .ToList();
    }

    private DbContext ResolveForEntityType(Type entityType)
    {
        var ns = entityType.Namespace ?? string.Empty;

        foreach (var (prefix, module) in _entityPrefixMap.Value.OrderByDescending(p => p.Key.Length))
        {
            if (ns.StartsWith(prefix, StringComparison.Ordinal))
                return (DbContext)_serviceProvider.GetRequiredService(module.DbContextType);
        }

        if (_coreContext.Model.FindEntityType(entityType) != null)
            return _coreContext;

        throw new NotFoundException($"No DbContext registered for entity '{entityType.Name}'.");
    }

    private IReadOnlyDictionary<string, IMetaForgeModule> BuildEntityPrefixMap()
    {
        var map = new Dictionary<string, IMetaForgeModule>(StringComparer.Ordinal);

        foreach (var module in _modules)
        {
            var prefix = $"MetaForge.{module.Name}.Domain";
            map[prefix] = module;
        }

        return map;
    }
}
