namespace MetaForge.Application.DTOs;

/// <summary>
/// Title, header, footer, and signature layout for PDF/Excel export and on-screen report display.
/// </summary>
public class ReportExportLayoutDto
{
    public string Title { get; set; } = string.Empty;

    public bool ShowTitleUnderline { get; set; } = true;

    public bool ShowSignatureBlock { get; set; }

    public string? HeaderLeft { get; set; }

    public string? HeaderCenter { get; set; }

    public string? HeaderRight { get; set; }

    public string? FooterLeft { get; set; }

    public string? FooterCenter { get; set; }

    public string? FooterRight { get; set; }

    public bool ShowPageNumbers { get; set; } = true;

    public bool ShowGeneratedTimestamp { get; set; } = true;

    public List<ReportSignatureLineDto> Signatures { get; set; } = [];
}

public class ReportSignatureLineDto
{
    public int Id { get; set; }

    public string Label { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }
}
