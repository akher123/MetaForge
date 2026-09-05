namespace MetaForge.Scaffold.WinForms;

internal static class UiTheme
{
    public static readonly Color Background = Color.FromArgb(237, 241, 247);
    public static readonly Color Surface = Color.White;
    public static readonly Color HeaderBg = Color.FromArgb(22, 48, 82);
    public static readonly Color HeaderFg = Color.White;
    public static readonly Color CardTitleBg = Color.FromArgb(230, 238, 250);
    public static readonly Color Accent = Color.FromArgb(0, 120, 212);
    public static readonly Color AccentHover = Color.FromArgb(0, 95, 170);
    public static readonly Color TextPrimary = Color.FromArgb(24, 32, 44);
    public static readonly Color TextSecondary = Color.FromArgb(68, 78, 92);
    public static readonly Color MutedText = Color.FromArgb(95, 105, 120);
    public static readonly Color Border = Color.FromArgb(198, 208, 220);
    public static readonly Color HelpBg = Color.FromArgb(248, 250, 253);
    public static readonly Color CodeBg = Color.FromArgb(28, 32, 40);
    public static readonly Color CodeFg = Color.FromArgb(230, 236, 244);
    public static readonly Color Success = Color.FromArgb(16, 124, 65);

    public static readonly Font UiFont = new("Segoe UI", 10F);
    public static readonly Font UiFontBold = new("Segoe UI", 10.5F, FontStyle.Bold);
    public static readonly Font TitleFont = new("Segoe UI", 18F, FontStyle.Bold);
    public static readonly Font SubtitleFont = new("Segoe UI", 10.5F);
    public static readonly Font CodeFont = new("Consolas", 10.5F);
    public static readonly Font HelpFont = new("Segoe UI", 9.5F);

    public static Padding PagePadding => UiScale.Px(20, 16, 20, 16);
    public static Padding CardBodyPadding => UiScale.Px(16, 14, 16, 16);

    /// <summary>Height of a single-line input or secondary button.</summary>
    public static int ControlHeight => UiScale.Px(34);

    public static int PrimaryButtonHeight => UiScale.Px(42);

    public static void StylePrimaryButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = Accent;
        button.ForeColor = Color.White;
        button.Font = new Font(UiFont, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        button.Height = PrimaryButtonHeight;
        button.MinimumSize = new Size(UiScale.Px(120), PrimaryButtonHeight);
        button.MouseEnter += (_, _) => button.BackColor = AccentHover;
        button.MouseLeave += (_, _) => button.BackColor = Accent;
    }

    public static void StyleSecondaryButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.BorderSize = 1;
        button.BackColor = Surface;
        button.ForeColor = TextPrimary;
        button.Font = UiFont;
        button.Cursor = Cursors.Hand;
        button.Height = ControlHeight;
        button.MinimumSize = new Size(UiScale.Px(90), ControlHeight);
    }

    public static void StyleTextBox(TextBox textBox)
    {
        textBox.Font = UiFont;
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.BackColor = Surface;
        textBox.ForeColor = TextPrimary;
    }

    public static Label CreateCaption(string text) =>
        new()
        {
            Text = text,
            AutoSize = true,
            Font = UiFont,
            ForeColor = TextSecondary,
            Margin = UiScale.Px(0, 6, 0, 4),
            UseMnemonic = false,
            Padding = new Padding(0)
        };
}
