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

    /// <summary>Claim type for the per-user security stamp used to invalidate stale sessions.</summary>
    public const string SecurityStampClaimType = "AspNet.Identity.SecurityStamp";

    public const string AuthorizationSnapshotCacheKeyPrefix = "auth:snapshot:";

    /// <summary>Root folder (relative to wwwroot) where uploaded files are stored.</summary>
    public const string UploadsFolderName = "uploads";

    /// <summary>Maximum allowed size for an uploaded file, in bytes (10 MB).</summary>
    public const long MaxUploadFileSizeBytes = 10 * 1024 * 1024;

    /// <summary>File extensions that are rendered as image thumbnails in the dynamic form.</summary>
    public static readonly IReadOnlyCollection<string> ImageFileExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".svg"
        };

    /// <summary>File extensions blocked from upload for security reasons.</summary>
    public static readonly IReadOnlyCollection<string> BlockedUploadExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".dll", ".bat", ".cmd", ".com", ".msi", ".sh", ".ps1",
            ".js", ".jar", ".vbs", ".scr", ".cpl", ".reg"
        };
}
