using MetaForge.Domain.Business;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MetaForge.Modules.Hr;

public static class HrModule
{
    public static IServiceCollection AddHrModule(this IServiceCollection services) => services;

    public static ModelBuilder ApplyHrConfigurations(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HrModule).Assembly);
        return modelBuilder;
    }
}
