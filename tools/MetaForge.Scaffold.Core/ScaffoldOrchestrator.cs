using Humanizer;
using MetaForge.Scaffold.Generation;
using MetaForge.Scaffold.Models;
using MetaForge.Scaffold.Patching;
using MetaForge.Scaffold.Schema;

namespace MetaForge.Scaffold;

public sealed class ScaffoldOrchestrator
{
    private readonly SqlServerSchemaReader _schemaReader = new();

    public async Task<ScaffoldResult> RunAsync(ScaffoldOptions options, CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);
        var root = SolutionRootResolver.Resolve(options.SolutionRoot);
        options.SolutionRoot = root;

        var table = await ResolveTableModelAsync(options, cancellationToken);

        var domainDir = Path.Combine(root, options.DomainOutputDir);
        var configDir = Path.Combine(root, options.ConfigOutputDir);
        var dbContextPath = Path.Combine(root, options.DbContextPath);

        var entityPath = Path.Combine(domainDir, $"{table.EntityName}.cs");
        var configurationPath = Path.Combine(configDir, $"{table.EntityName}Configuration.cs");

        if (!options.Force)
        {
            EnsureNotExists(entityPath);
            EnsureNotExists(configurationPath);
        }

        var entitySource = EntityCodeGenerator.Generate(table, options.IncludeNavigations);
        var configSource = ConfigurationCodeGenerator.Generate(table, options.IncludeNavigations);

        var written = new List<string>();
        var dbSetPatched = false;

        if (options.DryRun)
        {
            var planned = new List<string> { entityPath, configurationPath };
            var willPatch = !options.NoDbSetPatch && !DbContextPatcher.DbSetExists(dbContextPath, table.EntityName);
            if (willPatch)
                planned.Add(dbContextPath);

            return new ScaffoldResult
            {
                EntityName = table.EntityName,
                TableName = table.TableName,
                PlannedFiles = planned,
                WillPatchDbSet = willPatch,
                DryRun = true,
                EntitySourcePreview = entitySource,
                ConfigurationSourcePreview = configSource
            };
        }

        Directory.CreateDirectory(domainDir);
        Directory.CreateDirectory(configDir);

        await File.WriteAllTextAsync(entityPath, entitySource, cancellationToken);
        written.Add(entityPath);

        await File.WriteAllTextAsync(configurationPath, configSource, cancellationToken);
        written.Add(configurationPath);

        if (!options.NoDbSetPatch)
        {
            if (DbContextPatcher.DbSetExists(dbContextPath, table.EntityName))
            {
                if (!options.Force)
                    throw new InvalidOperationException($"DbSet<{table.EntityName}> already exists. Use --force to continue.");
            }
            else
            {
                var plural = table.EntityName.Pluralize();
                if (!DbContextPatcher.TryPatch(dbContextPath, table.EntityName, plural, out var patchError))
                    throw new InvalidOperationException(patchError ?? "Failed to patch DbContext.");

                written.Add(dbContextPath);
                dbSetPatched = true;
            }
        }

        string? migrationName = null;
        string? migrationOutput = null;
        if (options.AddMigration)
        {
            migrationName = $"Scaffold_{table.EntityName}";
            migrationOutput = await RunEfMigrationAddAsync(root, options, migrationName, cancellationToken);
        }

        return new ScaffoldResult
        {
            EntityName = table.EntityName,
            TableName = table.TableName,
            WrittenFiles = written,
            DbSetPatched = dbSetPatched,
            MigrationName = migrationName,
            MigrationOutput = migrationOutput,
            DryRun = false
        };
    }

    private async Task<TableModel> ResolveTableModelAsync(ScaffoldOptions options, CancellationToken cancellationToken)
    {
        if (options.IsReverseFromTable)
        {
            var connection = ConnectionStringResolver.Resolve(options);
            return await _schemaReader.ReadTableAsync(
                connection,
                options.TableName!,
                options.EntityName,
                cancellationToken);
        }

        var entityName = options.EntityName!;
        var tableName = options.TableName ?? ColumnSpecParser.DefaultTableName(entityName);
        return ColumnSpecParser.Parse(entityName, tableName, options.Columns!);
    }

    private static void ValidateOptions(ScaffoldOptions options)
    {
        if (options.IsReverseFromTable && options.IsGreenfield)
            throw new InvalidOperationException("Specify either --table or --name with --columns, not both.");

        if (!options.IsReverseFromTable && !options.IsGreenfield)
            throw new InvalidOperationException(
                "Provide --table <TableName> (reverse scaffold) or --name <Entity> --columns <spec> (greenfield).");

        if (options.IsGreenfield && string.IsNullOrWhiteSpace(options.Columns))
            throw new InvalidOperationException("Greenfield scaffold requires --columns.");
    }

    private static void EnsureNotExists(string path)
    {
        if (File.Exists(path))
            throw new InvalidOperationException($"File already exists: {path}. Use --force to overwrite.");
    }

    private static async Task<string> RunEfMigrationAddAsync(
        string root,
        ScaffoldOptions options,
        string migrationName,
        CancellationToken cancellationToken)
    {
        var infra = Path.Combine(root, options.InfrastructureProject);
        var web = Path.Combine(root, options.WebProject);

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"ef migrations add {migrationName} --project \"{infra}\" --startup-project \"{web}\"",
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start dotnet ef.");

        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var output = string.Join(Environment.NewLine, new[] { stdout, stderr }.Where(s => !string.IsNullOrWhiteSpace(s)));

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"dotnet ef migrations add failed with exit code {process.ExitCode}.{Environment.NewLine}{output}");

        return output;
    }
}
