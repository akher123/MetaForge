using System.Reflection;
using MetaForge.Hrm.Infrastructure.Persistence;
using MetaForge.Modules.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MetaForge.Hrm.Infrastructure;

public sealed class HrmModule : IMetaForgeModule
{
    public string Name => "Hrm";

    public string AreaName => "Hrm";

    public string SchemaName => "hrm";

    public Type DbContextType => typeof(HrmDbContext);

    public Assembly InfrastructureAssembly => typeof(HrmModule).Assembly;

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
    }
}

public static class DependencyInjection
{
    private const string DefaultConnection =
        "Server=localhost;Database=MetaForgeDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    public static IServiceCollection AddHrmModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? DefaultConnection;
        var migrationsAssembly = typeof(HrmDbContext).Assembly.FullName;

        services.AddDbContext<HrmDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly)));

        services.AddSingleton<IMetaForgeModule, HrmModule>();
        return services;
    }
}
