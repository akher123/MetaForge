namespace MetaForge.Domain.Metadata;

/// <summary>
/// Lookup dropdown configuration for dynamic forms.
/// </summary>
public class LookupConfiguration
{
    public int Id { get; set; }

    public string EntityName { get; set; } = string.Empty;

    public string ValueField { get; set; } = "Id";

    public string TextField { get; set; } = "Name";

    public string? FilterExpression { get; set; }

    public bool IsActive { get; set; } = true;
}
