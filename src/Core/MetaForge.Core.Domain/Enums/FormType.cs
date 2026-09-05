namespace MetaForge.Domain.Enums;

/// <summary>
/// Admin form screen types mapped to entity configuration.
/// </summary>
public enum FormType
{
    Master,
    /// <summary>Single-entity form with fields grouped in tabs (ERP-style sections).</summary>
    Tabbed,
    MasterDetail,
    MasterDetailTabular,
    /// <summary>Hierarchical tree grid spanning multiple related entities (e.g. Country → Region → City).</summary>
    TreeViewMultiTable,
    Detail
}
