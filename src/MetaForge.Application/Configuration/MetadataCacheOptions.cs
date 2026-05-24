namespace MetaForge.Application.Configuration;

/// <summary>
/// In-memory cache settings for admin module/form metadata.
/// </summary>
public class MetadataCacheOptions
{
    public const string SectionName = "MetadataCache";

    /// <summary>Maximum time a metadata entry remains in cache.</summary>
    public int AbsoluteExpirationMinutes { get; set; } = 60;

    /// <summary>Sliding window; access resets expiry within the absolute limit.</summary>
    public int SlidingExpirationMinutes { get; set; } = 15;

    /// <summary>How long to cache negative lookups (module not found).</summary>
    public int NotFoundExpirationMinutes { get; set; } = 2;
}
