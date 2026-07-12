using MetaForge.Application.Configuration;
using MetaForge.Infrastructure.Dynamic;
using MetaForge.Infrastructure.Email;
using MetaForge.Infrastructure.Email.Providers;
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

        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));
        services.Configure<MetadataCacheOptions>(configuration.GetSection(MetadataCacheOptions.SectionName));
        services.Configure<ExportOptions>(configuration.GetSection(ExportOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<SecurityOptions>(configuration.GetSection(SecurityOptions.SectionName));
        services.AddMemoryCache();
        services.AddHttpContextAccessor();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IForgeFormRepository, ForgeFormRepository>();
        services.AddScoped<IForgeMenuRepository, ForgeMenuRepository>();
        services.AddScoped<IForgeReportRepository, ForgeReportRepository>();
        services.AddScoped<IEntityTypeResolver, EntityTypeResolver>();
        services.AddScoped<IFormMetadataCache, FormMetadataCache>();

        services.AddScoped<IFormMetadataService, FormMetadataService>();
        services.AddScoped<IGenericCrudService, GenericCrudService>();
        services.AddScoped<IGridService, GridService>();
        services.AddScoped<IGridActionService, GridActionService>();
        services.AddScoped<ILookupService, LookupService>();
        services.AddScoped<IMappingAssociationService, MappingAssociationService>();
        services.AddScoped<IMasterDetailService, MasterDetailService>();
        services.AddScoped<ITreeGridService, TreeGridService>();
        services.AddScoped<IEntityMetadataDiscoveryService, EntityMetadataDiscoveryService>();
        services.AddScoped<INavigationService, NavigationService>();
        services.AddScoped<IDynamicValidationService, DynamicValidationService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        services.AddScoped<IFormAuthorizationService, FormAuthorizationService>();
        services.AddScoped<IUserAuthorizationSnapshotProvider, UserAuthorizationSnapshotProvider>();
        services.AddScoped<ISecurityStampService, SecurityStampService>();
        services.AddScoped<IUserClaimsFactory, UserClaimsFactory>();

        services.AddScoped<ISecurityManagementService, SecurityManagementService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddScoped<IUserPreferenceService, UserPreferenceService>();
        services.AddScoped<IFormConfigurationService, FormConfigurationService>();
        services.AddScoped<IFormHealthCheckService, FormHealthCheckService>();
        services.AddScoped<IReportConfigurationService, ReportConfigurationService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IMenuManagementService, MenuManagementService>();
        services.AddScoped<IMenuSyncService, MenuSyncService>();

        services.AddScoped<IEmailConfigurationService, EmailConfigurationService>();
        services.AddScoped<IEmailDispatchService, EmailDispatchService>();
        services.AddScoped<IEmailMessageService, EmailMessageService>();
        services.AddScoped<IEmailTriggerService, EmailTriggerService>();
        services.AddScoped<IEmailTemplateRenderer, EmailTemplateRenderer>();
        services.AddScoped<IRetryPolicyEvaluator, RetryPolicyEvaluator>();
        services.AddScoped<IEmailCredentialResolver, EmailCredentialResolver>();
        services.AddScoped<IEmailChannelSender, EmailChannelSender>();
        services.AddScoped<IEmailProvider, SmtpEmailProvider>();
        services.AddScoped<IEmailProvider, SendGridEmailProvider>();
        services.AddScoped<IEmailProviderFactory, EmailProviderFactory>();
        services.AddSingleton<IEmailQueue, EmailQueue>();
        services.AddHostedService<EmailBackgroundService>();

        return services;
    }
}
