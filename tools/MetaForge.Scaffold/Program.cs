using MetaForge.Scaffold;
using System.CommandLine;

var rootOption = new Option<string?>("--root", "Solution root directory (default: auto-detect MetaForge.slnx)");
var tableOption = new Option<string?>("--table", "SQL Server table name (reverse scaffold)");
var nameOption = new Option<string?>("--name", "Entity/class name (greenfield or override singular name)");
var columnsOption = new Option<string?>("--columns", "Greenfield columns: Name:type[:size][!], comma-separated");
var connectionOption = new Option<string?>("--connection", "Database connection string");
var configOption = new Option<string?>("--config", "Path to appsettings.json for DefaultConnection");
var includeNavOption = new Option<bool>("--include-navigations", "Generate navigation properties and HasOne mappings");
var dryRunOption = new Option<bool>("--dry-run", "Preview generated code without writing files");
var noDbSetOption = new Option<bool>("--no-dbset-patch", "Skip MetaForgeDbContext DbSet insertion");
var migrationOption = new Option<bool>("--migration", "Run dotnet ef migrations add after scaffolding");
var forceOption = new Option<bool>("--force", "Overwrite existing entity/config files");

var entityCommand = new Command("entity", "Scaffold a business entity for MetaForge");
entityCommand.AddOption(rootOption);
entityCommand.AddOption(tableOption);
entityCommand.AddOption(nameOption);
entityCommand.AddOption(columnsOption);
entityCommand.AddOption(connectionOption);
entityCommand.AddOption(configOption);
entityCommand.AddOption(includeNavOption);
entityCommand.AddOption(dryRunOption);
entityCommand.AddOption(noDbSetOption);
entityCommand.AddOption(migrationOption);
entityCommand.AddOption(forceOption);

entityCommand.SetHandler(async context =>
{
    var options = new ScaffoldOptions
    {
        SolutionRoot = context.ParseResult.GetValueForOption(rootOption) ?? ".",
        TableName = context.ParseResult.GetValueForOption(tableOption),
        EntityName = context.ParseResult.GetValueForOption(nameOption),
        Columns = context.ParseResult.GetValueForOption(columnsOption),
        ConnectionString = context.ParseResult.GetValueForOption(connectionOption),
        ConfigPath = context.ParseResult.GetValueForOption(configOption),
        IncludeNavigations = context.ParseResult.GetValueForOption(includeNavOption),
        DryRun = context.ParseResult.GetValueForOption(dryRunOption),
        NoDbSetPatch = context.ParseResult.GetValueForOption(noDbSetOption),
        AddMigration = context.ParseResult.GetValueForOption(migrationOption),
        Force = context.ParseResult.GetValueForOption(forceOption)
    };

    try
    {
        var orchestrator = new ScaffoldOrchestrator();
        var result = await orchestrator.RunAsync(options);

        Console.Write(ScaffoldResultFormatter.Format(result));
        if (!result.DryRun && !options.AddMigration)
            Console.WriteLine("  Tip: use --migration to add an EF migration in the same step.");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Environment.ExitCode = 1;
    }
});

var scaffoldCommand = new Command("scaffold", "MetaForge scaffolding commands");
scaffoldCommand.AddCommand(entityCommand);

var root = new RootCommand("MetaForge CLI — scaffold entities and reduce boilerplate");
root.AddCommand(scaffoldCommand);

return await root.InvokeAsync(args);
