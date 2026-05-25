using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MetaForge.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core CLI (migrations, database update).
/// </summary>
public sealed class MetaForgeDbContextFactory : IDesignTimeDbContextFactory<MetaForgeDbContext>
{
    private const string DefaultConnectionString =
        "Server=localhost;Database=MetaForgeDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    public MetaForgeDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("METAFORGE_CONNECTION")
            ?? DefaultConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<MetaForgeDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sql =>
            sql.MigrationsAssembly(typeof(MetaForgeDbContext).Assembly.FullName));

        return new MetaForgeDbContext(optionsBuilder.Options);
    }
}
