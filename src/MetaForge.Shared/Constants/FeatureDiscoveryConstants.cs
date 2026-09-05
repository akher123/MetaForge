namespace MetaForge.Shared.Constants;

/// <summary>
/// Namespace rules that separate MetaForge framework entities from discoverable feature (business) entities.
/// </summary>
public static class FeatureDiscoveryConstants
{
    /// <summary>
    /// Root namespace for application feature entities scanned by Form Builder discovery.
    /// New entities should live under this namespace (optionally in sub-namespaces such as .Sales or .Education).
    /// </summary>
    public const string FeatureEntityNamespacePrefix = "MetaForge.Domain.Features";

    /// <summary>
    /// Legacy namespace retained for existing entities and EF migrations. Discovery accepts both prefixes.
    /// </summary>
    public const string LegacyFeatureEntityNamespacePrefix = "MetaForge.Domain.Business";

    /// <summary>
    /// Module-based entities (e.g. MetaForge.Hrm.Domain.Entities).
    /// </summary>
    public const string ModuleEntityNamespaceMarker = ".Domain.";

    public static bool IsFeatureEntityNamespace(string? namespaceName) =>
        !string.IsNullOrEmpty(namespaceName)
        && (namespaceName.StartsWith(FeatureEntityNamespacePrefix, StringComparison.Ordinal)
            || namespaceName.StartsWith(LegacyFeatureEntityNamespacePrefix, StringComparison.Ordinal)
            || IsModuleFeatureEntityNamespace(namespaceName));

    public static bool IsModuleFeatureEntityNamespace(string namespaceName) =>
        namespaceName.StartsWith("MetaForge.", StringComparison.Ordinal)
        && namespaceName.Contains(ModuleEntityNamespaceMarker, StringComparison.Ordinal)
        && !namespaceName.StartsWith("MetaForge.Domain.", StringComparison.Ordinal)
        && !namespaceName.StartsWith("MetaForge.Core.", StringComparison.Ordinal)
        && !namespaceName.StartsWith("MetaForge.Application.", StringComparison.Ordinal)
        && !namespaceName.StartsWith("MetaForge.Infrastructure.", StringComparison.Ordinal);

    public static readonly string[] FrameworkEntityNamespacePrefixes =
    [
        "MetaForge.Domain.Metadata",
        "MetaForge.Domain.Security",
        "MetaForge.Domain.Audit"
    ];

    public static bool IsFrameworkEntityNamespace(string? namespaceName) =>
        !string.IsNullOrEmpty(namespaceName)
        && FrameworkEntityNamespacePrefixes.Any(p => namespaceName.StartsWith(p, StringComparison.Ordinal));
}
