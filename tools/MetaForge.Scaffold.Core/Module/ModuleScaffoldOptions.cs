namespace MetaForge.Scaffold.Module;

using MetaForge.Scaffold;

public sealed class ModuleScaffoldOptions
{
    public string SolutionRoot { get; set; } = ".";

    public string ModuleName { get; set; } = string.Empty;

    public bool DryRun { get; set; }

    public bool Force { get; set; }

    public bool CreateWebArea { get; set; } = true;

    public bool CreateInitialMigration { get; set; }

    public string WebProject { get; set; } = ScaffoldConstants.WebProject;

    public string SolutionFile { get; set; } = "MetaForge.slnx";

    public string MetaForgeConfigPath { get; set; } = "metaforge.json";

    public string ModuleRegistrationPath { get; set; } = ScaffoldConstants.ModuleRegistrationFile;
}
