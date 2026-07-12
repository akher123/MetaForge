using MetaForge.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace MetaForge.Infrastructure.Audit;

/// <summary>
/// Audit module dependency injection extensions.
/// </summary>
public static class AuditDependencyInjection
{
    public static IServiceCollection AddAuditServices(this IServiceCollection services)
    {
        services.AddSingleton<IAuditQueue, AuditQueue>();
        services.AddScoped<IAuditLogStore, EfAuditLogStore>();
        services.AddScoped<IAuditUserProvider, HttpContextAuditUserProvider>();
        services.AddScoped<IAuditService, QueuedAuditService>();
        services.AddHostedService<AuditBackgroundService>();

        return services;
    }
}
