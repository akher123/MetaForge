using Microsoft.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore.Diagnostics;

using Microsoft.Extensions.Configuration;



namespace MetaForge.Infrastructure.Persistence;



/// <summary>

/// Configures EF Core for SQL Server.

/// </summary>

public static class DatabaseConfiguration

{

    public const string DefaultConnection =

        "Server=localhost;Database=MetaForgeDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";



    public static void ConfigureDbContext(DbContextOptionsBuilder options, IConfiguration configuration)

    {

        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? DefaultConnection;

        var migrationsAssembly = typeof(MetaForgeDbContext).Assembly.FullName;



        options.ConfigureWarnings(warnings =>

            warnings.Ignore(RelationalEventId.PendingModelChangesWarning));



        options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly));

    }

}


