using MetaForge.Application.Configuration;
using MetaForge.Infrastructure.Dynamic;
using MetaForge.Infrastructure.Repositories;
using MetaForge.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MetaForge.Infrastructure;

/// <summary>
/// Infrastructure layer dependency injection extensions.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<MetaForgeDbContext>(options =>
            DatabaseConfiguration.ConfigureDbContext(options, configuration));

        services.Configure<MetadataCacheOptions>(configuration.GetSection(MetadataCacheOptions.SectionName));
        services.AddMemoryCache();
        services.AddHttpContextAccessor();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IForgeFormRepository, ForgeFormRepository>();
        services.AddScoped<IForgeMenuRepository, ForgeMenuRepository>();
        services.AddScoped<IEntityTypeResolver, EntityTypeResolver>();
        services.AddScoped<IFormMetadataCache, FormMetadataCache>();

        services.AddScoped<IFormMetadataService, FormMetadataService>();
        services.AddScoped<IGenericCrudService, GenericCrudService>();
        services.AddScoped<IGridService, GridService>();
        services.AddScoped<IGridActionService, GridActionService>();
        services.AddScoped<ILookupService, LookupService>();
        services.AddScoped<IMasterDetailService, MasterDetailService>();
        services.AddScoped<IEntityMetadataDiscoveryService, EntityMetadataDiscoveryService>();
        services.AddScoped<INavigationService, NavigationService>();
        services.AddScoped<IDynamicValidationService, DynamicValidationService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IFormAuthorizationService, FormAuthorizationService>();
        services.AddScoped<IUserAuthorizationSnapshotProvider, UserAuthorizationSnapshotProvider>();
        services.AddScoped<ISecurityStampService, SecurityStampService>();
        services.AddScoped<IUserClaimsFactory, UserClaimsFactory>();

        services.AddScoped<ISecurityManagementService, SecurityManagementService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserPreferenceService, UserPreferenceService>();
        services.AddScoped<IFormConfigurationService, FormConfigurationService>();
        services.AddScoped<IMenuManagementService, MenuManagementService>();
        services.AddScoped<IMenuSyncService, MenuSyncService>();

        return services;
    }
}
