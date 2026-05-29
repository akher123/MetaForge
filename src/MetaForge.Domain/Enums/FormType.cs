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
    Detail
}
