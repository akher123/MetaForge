namespace MetaForge.Scaffold.WinForms;

/// <summary>
/// Converts the layout constants used by this UI (all authored at 96 DPI) into device pixels.
/// Fonts are declared in points and therefore already grow on scaled displays, so every fixed
/// height, width, padding and margin has to grow with them or the content gets clipped.
/// </summary>
internal static class UiScale
{
    private const float BaselineDpi = 96f;

    public static float Factor { get; private set; } = 1f;

    public static void SyncWith(Control control) => Factor = Math.Max(1f, control.DeviceDpi / BaselineDpi);

    public static int Px(int logical) => (int)Math.Round(logical * Factor, MidpointRounding.AwayFromZero);

    public static Size Px(int width, int height) => new(Px(width), Px(height));

    public static Padding Px(int left, int top, int right, int bottom) =>
        new(Px(left), Px(top), Px(right), Px(bottom));
}
