namespace MetaForge.Shared.Culture;

/// <summary>
/// Per-request effective date format preferences (set by culture middleware).
/// </summary>
public static class DisplayFormatContext
{
    private static readonly AsyncLocal<DisplayFormatPreferences?> Current = new();

    public static DisplayFormatPreferences? Preferences
    {
        get => Current.Value;
        set => Current.Value = value;
    }
}

public sealed class DisplayFormatPreferences
{
    public required string DateFormat { get; init; }

    public required string DateTimeFormat { get; init; }
}
