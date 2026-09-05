using MetaForge.Scaffold;
using System.CommandLine;

var rootOption = new Option<string?>("--root", "Solution root directory (default: auto-detect MetaForge.slnx)");
var moduleOption = new Option<string?>("--module", "Target module name from metaforge.json (e.g. Hrm, Accounting)");
var metaforgeConfigOption = new Option<string?>("--metaforge-config", "Path to metaforge.json (default: metaforge.json at solution root)");
var tableOption = new Option<string?>("--table", "SQL Server table name (uses module schema, or schema.table)");
var nameOption = new Option<string?>("--name", "Entity/class name (greenfield or override singular name)");
var columnsOption = new Option<string?>("--columns", "Greenfield columns: Name:type[:size][!], comma-separated");
var connectionOption = new Option<string?>("--connection", "Database connection string");
var configOption = new Option<string?>("--config", "Path to appsettings.json for DefaultConnection");
var includeNavOption = new Option<bool>("--include-navigations", "Generate navigation properties and HasOne mappings");
var dryRunOption = new Option<bool>("--dry-run", "Preview generated code without writing files");
var noDbSetOption = new Option<bool>("--no-dbset-patch", "Skip DbContext DbSet insertion");
var addDbSetOption = new Option<bool>("--add-dbset", "Add DbSet to module DbContext (default for modules)");
var migrationOption = new Option<bool>("--migration", "Run dotnet ef migrations add after scaffolding");
var forceOption = new Option<bool>("--force", "Overwrite existing entity/config files");

var entityCommand = new Command("entity", "Scaffold a business entity for a MetaForge module");
entityCommand.AddOption(rootOption);
entityCommand.AddOption(moduleOption);
entityCommand.AddOption(metaforgeConfigOption);
entityCommand.AddOption(tableOption);
entityCommand.AddOption(nameOption);
entityCommand.AddOption(columnsOption);
entityCommand.AddOption(connectionOption);
entityCommand.AddOption(configOption);
entityCommand.AddOption(includeNavOption);
entityCommand.AddOption(dryRunOption);
entityCommand.AddOption(noDbSetOption);
entityCommand.AddOption(addDbSetOption);
entityCommand.AddOption(migrationOption);
entityCommand.AddOption(forceOption);

entityCommand.SetHandler(async context =>
{
    var noDbSet = context.ParseResult.GetValueForOption(noDbSetOption);
    var addDbSet = context.ParseResult.GetValueForOption(addDbSetOption);

    var options = new ScaffoldOptions
    {
        SolutionRoot = context.ParseResult.GetValueForOption(rootOption) ?? ".",
        ModuleName = context.ParseResult.GetValueForOption(moduleOption),
        MetaForgeConfigPath = context.ParseResult.GetValueForOption(metaforgeConfigOption),
        TableName = context.ParseResult.GetValueForOption(tableOption),
        EntityName = context.ParseResult.GetValueForOption(nameOption),
        Columns = context.ParseResult.GetValueForOption(columnsOption),
        ConnectionString = context.ParseResult.GetValueForOption(connectionOption),
        ConfigPath = context.ParseResult.GetValueForOption(configOption),
        IncludeNavigations = context.ParseResult.GetValueForOption(includeNavOption),
        DryRun = context.ParseResult.GetValueForOption(dryRunOption),
        AddMigration = context.ParseResult.GetValueForOption(migrationOption),
        Force = context.ParseResult.GetValueForOption(forceOption)
    };

    if (addDbSet)
    {
        options.NoDbSetPatch = false;
        options.NoDbSetPatchExplicit = true;
    }
    else if (noDbSet)
    {
        options.NoDbSetPatch = true;
        options.NoDbSetPatchExplicit = true;
    }

    try
    {
        var orchestrator = new ScaffoldOrchestrator();
        var result = await orchestrator.RunAsync(options);

        Console.Write(ScaffoldResultFormatter.Format(result));
        if (!result.DryRun && !options.AddMigration)
            Console.WriteLine("  Tip: use --migration to add an EF migration for the module DbContext.");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Environment.ExitCode = 1;
    }
});

var moduleNameOption = new Option<string>("--name", "Module name in PascalCase (e.g. Production, Accounting)") { IsRequired = true };
var noWebAreaOption = new Option<bool>("--no-web-area", "Skip MVC area scaffold under MetaForge.Web/Areas");
var moduleMigrationOption = new Option<bool>("--migration", "Create initial EF migration for the module DbContext");
var moduleForceOption = new Option<bool>("--force", "Allow creating when module folder already exists");
var moduleDryRunOption = new Option<bool>("--dry-run", "Preview generated module without writing files");

var moduleCommand = new Command("module", "Scaffold a new business module (Domain, Application, Infrastructure, DbContext)");
moduleCommand.AddOption(rootOption);
moduleCommand.AddOption(moduleNameOption);
moduleCommand.AddOption(noWebAreaOption);
moduleCommand.AddOption(moduleMigrationOption);
moduleCommand.AddOption(moduleForceOption);
moduleCommand.AddOption(moduleDryRunOption);

moduleCommand.SetHandler(async context =>
{
    var options = new MetaForge.Scaffold.Module.ModuleScaffoldOptions
    {
        SolutionRoot = context.ParseResult.GetValueForOption(rootOption) ?? ".",
        ModuleName = context.ParseResult.GetValueForOption(moduleNameOption)!,
        CreateWebArea = !context.ParseResult.GetValueForOption(noWebAreaOption),
        CreateInitialMigration = context.ParseResult.GetValueForOption(moduleMigrationOption),
        Force = context.ParseResult.GetValueForOption(moduleForceOption),
        DryRun = context.ParseResult.GetValueForOption(moduleDryRunOption)
    };

    try
    {
        var orchestrator = new MetaForge.Scaffold.Module.ModuleScaffoldOrchestrator();
        var result = await orchestrator.RunAsync(options);
        Console.Write(MetaForge.Scaffold.Module.ModuleScaffoldResultFormatter.Format(result));
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Environment.ExitCode = 1;
    }
});

var scaffoldCommand = new Command("scaffold", "MetaForge scaffolding commands");
scaffoldCommand.AddCommand(entityCommand);
scaffoldCommand.AddCommand(moduleCommand);

var root = new RootCommand("MetaForge CLI — scaffold entities into modular Clean Architecture projects");
root.AddCommand(scaffoldCommand);

return await root.InvokeAsync(args);
