using MetaForge.Domain.Business;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MetaForge.Modules.MasterData;

public static class MasterDataModule
{
    public static IServiceCollection AddMasterDataModule(this IServiceCollection services) => services;

    public static ModelBuilder ApplyMasterDataConfigurations(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MasterDataModule).Assembly);
        return modelBuilder;
    }
}
