namespace MetaForge.Shared.Constants;

/// <summary>
/// Application-wide constants.
/// </summary>
public static class AppConstants
{
    public const string MetadataCacheKeyPrefix = "admin:metadata:";
    public const string LookupCacheKeyPrefix = "admin:lookup:";
    public const int DefaultPageSize = 25;
    /// <summary>Default page size for autocomplete / paged lookup search.</summary>
    public const int DefaultLookupPageSize = 10;
    /// <summary>Hard cap for legacy full-list lookup responses.</summary>
    public const int MaxLookupListSize = 100;
    public const string PermissionClaimType = "permission";
}
