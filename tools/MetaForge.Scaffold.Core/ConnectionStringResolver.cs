using Microsoft.Extensions.Configuration;

namespace MetaForge.Scaffold;

internal static class ConnectionStringResolver
{
    public static string Resolve(ScaffoldOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
            return options.ConnectionString;

        var env = Environment.GetEnvironmentVariable("METAFORGE_CONNECTION");
        if (!string.IsNullOrWhiteSpace(env))
            return env;

        var root = SolutionRootResolver.Resolve(options.SolutionRoot);
        var configPath = options.ConfigPath
            ?? Path.Combine(root, "src/MetaForge.Web/appsettings.json");

        if (!File.Exists(configPath))
            throw new InvalidOperationException(
                $"Connection string not provided and config not found at '{configPath}'. Use --connection or --config.");

        var configuration = new ConfigurationBuilder()
            .AddJsonFile(configPath, optional: false)
            .Build();

        var cs = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("DefaultConnection is missing from appsettings.");

        return cs;
    }
}
