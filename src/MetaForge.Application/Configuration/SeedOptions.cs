namespace MetaForge.Application.Configuration;

/// <summary>
/// Controls initial database seeding and startup data upgrades.
/// </summary>
public class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>
    /// When true, seeds sample business entities, demo forms, sample reports,
    /// and runs demo-specific metadata upgrades on startup.
    /// Set false in production deployments.
    /// </summary>
    public bool IncludeDemoData { get; set; }
}
