namespace MetaForge.Application.DTOs;

/// <summary>
/// Selectable property path for dynamic report configuration (supports navigation paths).
/// </summary>
public class ReportPropertyOptionDto
{
    public string Path { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string ClrType { get; set; } = string.Empty;

    public bool IsForeignKey { get; set; }
}
