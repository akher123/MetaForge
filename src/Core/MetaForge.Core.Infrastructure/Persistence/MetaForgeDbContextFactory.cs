using Microsoft.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore.Design;

using Microsoft.Extensions.Configuration;



namespace MetaForge.Infrastructure.Persistence;



/// <summary>

/// Design-time factory for EF Core CLI (migrations, database update).

/// </summary>

public sealed class MetaForgeDbContextFactory : IDesignTimeDbContextFactory<MetaForgeDbContext>

{

    public MetaForgeDbContext CreateDbContext(string[] args)

    {

        var connectionString = Environment.GetEnvironmentVariable("METAFORGE_CONNECTION")

            ?? DatabaseConfiguration.DefaultConnection;



        var optionsBuilder = new DbContextOptionsBuilder<MetaForgeDbContext>();

        var configuration = new ConfigurationBuilder()

            .AddInMemoryCollection(new Dictionary<string, string?>

            {

                ["ConnectionStrings:DefaultConnection"] = connectionString

            })

            .Build();



        DatabaseConfiguration.ConfigureDbContext(optionsBuilder, configuration);

        return new MetaForgeDbContext(optionsBuilder.Options);

    }

}


