namespace MetaForge.Domain.Metadata;

/// <summary>
/// Signature line shown at the bottom of exported reports (PDF/Excel).
/// </summary>
public class ForgeReportSignature
{
    public int Id { get; set; }

    public int ReportId { get; set; }

    public string Label { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public ForgeReport Report { get; set; } = null!;
}
