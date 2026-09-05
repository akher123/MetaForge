using Humanizer;
using MetaForge.Scaffold.Config;
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

        var config = MetaForgeConfigLoader.Load(root, options.MetaForgeConfigPath);
        var moduleName = ResolveModuleName(options, config);
        var profile = MetaForgeConfigLoader.ResolveModule(config, moduleName, root);
        MetaForgeConfigLoader.ApplyToOptions(options, profile, root);

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

        var entitySource = EntityCodeGenerator.Generate(table, profile, options.IncludeNavigations);
        var configSource = ConfigurationCodeGenerator.Generate(table, profile, options.IncludeNavigations);

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
                ModuleName = profile.Name,
                SchemaName = profile.SchemaName,
                EntityName = table.EntityName,
                TableName = table.TableName,
                TableSchemaName = table.SchemaName,
                DbContextName = options.DbContextName,
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
                if (!DbContextPatcher.TryPatch(dbContextPath, table.EntityName, plural, options.EntityNamespace, out var patchError))
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
            migrationOutput = await DotNetCliRunner.RunEfMigrationAddAsync(
                root,
                "MetaForge.slnx",
                options.InfrastructureProject,
                options.WebProject,
                migrationName,
                options.MigrationOutputDir,
                options.DbContextName,
                cancellationToken);
        }

        return new ScaffoldResult
        {
            ModuleName = profile.Name,
            SchemaName = profile.SchemaName,
            EntityName = table.EntityName,
            TableName = table.TableName,
            TableSchemaName = table.SchemaName,
            DbContextName = options.DbContextName,
            WrittenFiles = written,
            DbSetPatched = dbSetPatched,
            MigrationName = migrationName,
            MigrationOutput = migrationOutput,
            DryRun = false
        };
    }

    private static string ResolveModuleName(ScaffoldOptions options, MetaForgeConfig config)
    {
        if (!string.IsNullOrWhiteSpace(options.ModuleName))
            return options.ModuleName.Trim();

        var enabled = MetaForgeConfigLoader.GetEnabledModuleNames(config);
        return enabled.FirstOrDefault()
            ?? throw new InvalidOperationException("No modules enabled in metaforge.json. Set enabledModules or pass --module.");
    }

    private async Task<TableModel> ResolveTableModelAsync(ScaffoldOptions options, CancellationToken cancellationToken)
    {
        if (options.IsReverseFromTable)
        {
            var connection = ConnectionStringResolver.Resolve(options);
            var tableId = TableIdentifier.Parse(options.TableName!, options.SchemaName);
            return await _schemaReader.ReadTableAsync(
                connection,
                tableId.Schema,
                tableId.TableName,
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
}
