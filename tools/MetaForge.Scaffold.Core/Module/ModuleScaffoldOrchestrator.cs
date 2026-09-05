using MetaForge.Scaffold;
using MetaForge.Scaffold.Patching;

namespace MetaForge.Scaffold.Module;

public sealed class ModuleScaffoldOrchestrator
{
    public async Task<ModuleScaffoldResult> RunAsync(ModuleScaffoldOptions options, CancellationToken cancellationToken = default)
    {
        var root = SolutionRootResolver.Resolve(options.SolutionRoot);
        options.SolutionRoot = root;

        var naming = ModuleNaming.Parse(options.ModuleName);
        ValidateModuleDoesNotExist(root, naming, options.Force);

        var plannedFiles = BuildPlannedFileList(root, naming, options);
        var previews = BuildSourcePreviews(naming, options);

        if (options.DryRun)
        {
            return new ModuleScaffoldResult
            {
                ModuleName = naming.Name,
                SchemaName = naming.SchemaName,
                PlannedFiles = plannedFiles,
                PatchedFiles =
                [
                    Path.Combine(root, options.MetaForgeConfigPath),
                    Path.Combine(root, options.SolutionFile),
                    Path.Combine(root, options.WebProject),
                    Path.Combine(root, options.ModuleRegistrationPath)
                ],
                DryRun = true,
                SourcePreviews = previews
            };
        }

        var written = new List<string>();
        await WriteModuleFilesAsync(root, naming, options, written, cancellationToken);

        var patched = new List<string>();
        PatchSolutionFiles(root, naming, options, patched);

        string? migrationName = null;
        string? migrationOutput = null;
        if (options.CreateInitialMigration)
        {
            migrationName = $"Initial{naming.Name}";
            migrationOutput = await DotNetCliRunner.RunEfMigrationAddAsync(
                root,
                options.SolutionFile,
                naming.InfrastructureProjectPath,
                options.WebProject,
                migrationName,
                "Persistence/Migrations",
                naming.DbContextName,
                cancellationToken);
        }

        return new ModuleScaffoldResult
        {
            ModuleName = naming.Name,
            SchemaName = naming.SchemaName,
            WrittenFiles = written,
            PatchedFiles = patched,
            MigrationName = migrationName,
            MigrationOutput = migrationOutput,
            DryRun = false
        };
    }

    private static void ValidateModuleDoesNotExist(string root, ModuleNaming naming, bool force)
    {
        if (force)
            return;

        var moduleRoot = Path.Combine(root, naming.ModuleFolder);
        if (Directory.Exists(moduleRoot))
            throw new InvalidOperationException(
                $"Module folder already exists: {moduleRoot}. Use --force to overwrite existing files where supported.");

        var configPath = Path.Combine(root, "metaforge.json");
        if (MetaForgeJsonPatcher.ModuleExists(configPath, naming.Name))
            throw new InvalidOperationException(
                $"Module '{naming.Name}' is already registered in metaforge.json.");
    }

    private static List<string> BuildPlannedFileList(string root, ModuleNaming naming, ModuleScaffoldOptions options)
    {
        var files = new List<string>
        {
            Path.Combine(root, naming.DomainProjectPath),
            Path.Combine(root, naming.ApplicationProjectPath),
            Path.Combine(root, naming.InfrastructureProjectPath),
            Path.Combine(root, naming.GlobalUsingsPath),
            Path.Combine(root, naming.DbContextPath),
            Path.Combine(root, naming.DependencyInjectionPath),
            Path.Combine(root, naming.EntitiesFolder),
            Path.Combine(root, naming.GeneratedConfigFolder),
            Path.Combine(root, naming.MigrationsFolder)
        };

        if (options.CreateWebArea)
        {
            files.Add(Path.Combine(root, naming.WebAreaControllerPath));
            files.Add(Path.Combine(root, naming.WebAreaIndexViewPath));
            files.Add(Path.Combine(root, naming.WebAreaViewStartPath));
        }

        return files;
    }

    private static Dictionary<string, string> BuildSourcePreviews(ModuleNaming naming, ModuleScaffoldOptions options)
    {
        var previews = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [naming.DbContextPath] = ModuleCodeGenerator.GenerateDbContext(naming),
            [naming.DependencyInjectionPath] = ModuleCodeGenerator.GenerateDependencyInjection(naming)
        };

        if (options.CreateWebArea)
            previews[naming.WebAreaControllerPath] = ModuleCodeGenerator.GenerateHomeController(naming);

        return previews;
    }

    private static async Task WriteModuleFilesAsync(
        string root,
        ModuleNaming naming,
        ModuleScaffoldOptions options,
        List<string> written,
        CancellationToken cancellationToken)
    {
        await WriteFileAsync(root, naming.DomainProjectPath, ModuleCodeGenerator.GenerateDomainProject(naming, root), written, cancellationToken);
        await WriteFileAsync(root, naming.ApplicationProjectPath, ModuleCodeGenerator.GenerateApplicationProject(naming, root), written, cancellationToken);
        await WriteFileAsync(root, naming.InfrastructureProjectPath, ModuleCodeGenerator.GenerateInfrastructureProject(naming, root), written, cancellationToken);
        await WriteFileAsync(root, naming.GlobalUsingsPath, ModuleCodeGenerator.GenerateGlobalUsings(), written, cancellationToken);
        await WriteFileAsync(root, naming.DbContextPath, ModuleCodeGenerator.GenerateDbContext(naming), written, cancellationToken);
        await WriteFileAsync(root, naming.DependencyInjectionPath, ModuleCodeGenerator.GenerateDependencyInjection(naming), written, cancellationToken);

        EnsureDirectory(root, naming.EntitiesFolder, written);
        EnsureDirectory(root, naming.GeneratedConfigFolder, written);
        EnsureDirectory(root, naming.MigrationsFolder, written);

        if (options.CreateWebArea)
        {
            await WriteFileAsync(root, naming.WebAreaControllerPath, ModuleCodeGenerator.GenerateHomeController(naming), written, cancellationToken);
            await WriteFileAsync(root, naming.WebAreaIndexViewPath, ModuleCodeGenerator.GenerateIndexView(naming), written, cancellationToken);
            await WriteFileAsync(root, naming.WebAreaViewStartPath, ModuleCodeGenerator.GenerateViewStart(), written, cancellationToken);
        }
    }

    private static void PatchSolutionFiles(string root, ModuleNaming naming, ModuleScaffoldOptions options, List<string> patched)
    {
        var configPath = Path.Combine(root, options.MetaForgeConfigPath);
        if (!MetaForgeJsonPatcher.TryAddModule(configPath, naming, out var configError))
            throw new InvalidOperationException(configError ?? "Failed to update metaforge.json.");
        patched.Add(configPath);

        var slnxPath = Path.Combine(root, options.SolutionFile);
        if (!SolutionFilePatcher.TryAddModule(slnxPath, naming, out var slnError))
            throw new InvalidOperationException(slnError ?? "Failed to update MetaForge.slnx.");
        patched.Add(slnxPath);

        var webProjectPath = Path.Combine(root, options.WebProject);
        if (!WebProjectReferencePatcher.TryAddModuleReference(webProjectPath, naming, root, out var webError))
            throw new InvalidOperationException(webError ?? "Failed to update MetaForge.Web.csproj.");
        patched.Add(webProjectPath);

        var registrationPath = Path.Combine(root, options.ModuleRegistrationPath);
        if (!ModuleRegistrationPatcher.TryRegisterModule(registrationPath, naming, out var regError))
            throw new InvalidOperationException(regError ?? "Failed to update MetaForgeModuleRegistration.cs.");
        patched.Add(registrationPath);
    }

    private static async Task WriteFileAsync(
        string root,
        string relativePath,
        string content,
        List<string> written,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, content, cancellationToken);
        written.Add(fullPath);
    }

    private static void EnsureDirectory(string root, string relativePath, List<string> written)
    {
        var fullPath = Path.Combine(root, relativePath);
        Directory.CreateDirectory(fullPath);
        written.Add(fullPath);
    }
}
