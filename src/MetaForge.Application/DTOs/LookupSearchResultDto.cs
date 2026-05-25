namespace MetaForge.Application.DTOs;

/// <summary>
/// Paginated lookup search result for autocomplete controls.
/// </summary>
public class LookupSearchResultDto
{
    public IReadOnlyList<LookupItemDto> Items { get; set; } = [];

    public bool HasMore { get; set; }
}
