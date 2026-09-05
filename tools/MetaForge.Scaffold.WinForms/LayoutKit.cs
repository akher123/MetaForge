namespace MetaForge.Scaffold.WinForms;

internal static class LayoutKit
{
    /// <summary>Height of a card's title bar, never smaller than the title text needs.</summary>
    public static int CardTitleHeight => Math.Max(UiScale.Px(44), UiTheme.UiFontBold.Height + UiScale.Px(18));

    public static Panel CreateCard(string title, Control body, bool fillVertical = false)
    {
        var card = new Panel
        {
            Dock = fillVertical ? DockStyle.Fill : DockStyle.Top,
            BackColor = UiTheme.Surface,
            Margin = fillVertical ? Padding.Empty : UiScale.Px(0, 0, 0, 12),
            Padding = Padding.Empty
        };

        card.Paint += (_, e) =>
        {
            var r = card.ClientRectangle;
            r.Width -= 1;
            r.Height -= 1;
            using var pen = new Pen(UiTheme.Border);
            e.Graphics.DrawRectangle(pen, r);
        };

        var titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = CardTitleHeight,
            BackColor = UiTheme.CardTitleBg,
            Padding = UiScale.Px(16, 0, 16, 0)
        };
        titleBar.Controls.Add(new Label
        {
            Text = title,
            Font = UiTheme.UiFontBold,
            ForeColor = UiTheme.TextPrimary,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false
        });

        body.Dock = fillVertical ? DockStyle.Fill : DockStyle.Top;
        body.Padding = UiTheme.CardBodyPadding;
        if (!fillVertical && body is Panel autoPanel)
        {
            autoPanel.AutoSize = true;
            autoPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        }

        card.Controls.Add(body);
        card.Controls.Add(titleBar);
        return card;
    }

    public static Panel CreateBodyPanel(bool fill = false) =>
        new()
        {
            BackColor = UiTheme.Surface,
            Dock = fill ? DockStyle.Fill : DockStyle.Top,
            AutoSize = !fill,
            AutoSizeMode = fill ? AutoSizeMode.GrowOnly : AutoSizeMode.GrowAndShrink
        };

    public static TableLayoutPanel CreateFieldGrid(int logicalRows)
    {
        var grid = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = logicalRows * 2,
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Width = UiScale.Px(100),
            Margin = Padding.Empty
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        for (var i = 0; i < logicalRows * 2; i++)
            grid.RowStyles.Add(new RowStyle(i % 2 == 0 ? SizeType.AutoSize : SizeType.Absolute));
        return grid;
    }

    public static void AddCaptionRow(TableLayoutPanel grid, ref int row, string caption)
    {
        grid.Controls.Add(UiTheme.CreateCaption(caption), 0, row);
        grid.RowStyles[row] = new RowStyle(SizeType.AutoSize);
        row++;
    }

    public static void AddInputRow(TableLayoutPanel grid, ref int row, Control input, int logicalHeight = 34)
    {
        var height = UiScale.Px(logicalHeight);
        StretchInput(input, height);
        input.Margin = UiScale.Px(0, 0, 0, 8);
        grid.Controls.Add(input, 0, row);
        grid.RowStyles[row] = new RowStyle(SizeType.Absolute, height + input.Margin.Vertical);
        row++;
    }

    /// <summary>
    /// Input with a trailing button, laid out by a table so it stays aligned at any DPI or font size.
    /// </summary>
    public static Control CreateInputButtonRow(Control input, Button button, int logicalHeight = 34)
    {
        var height = UiScale.Px(logicalHeight);
        var isTall = input is TextBox { Multiline: true };

        var row = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            Dock = DockStyle.Fill,
            Height = height,
            Margin = UiScale.Px(0, 0, 0, 8),
            Padding = Padding.Empty,
            BackColor = UiTheme.Surface
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        StretchInput(input, height);
        input.Margin = UiScale.Px(0, 0, 10, 0);

        button.AutoSize = false;
        // A tall multiline input keeps a normal-sized button pinned to its top-right corner.
        button.Size = new Size(UiScale.Px(112), isTall ? UiTheme.ControlHeight : height);
        button.Margin = Padding.Empty;
        button.Anchor = isTall ? AnchorStyles.Top | AnchorStyles.Right : AnchorStyles.Right;

        row.Controls.Add(input, 0, 0);
        row.Controls.Add(button, 1, 0);
        return row;
    }

    /// <summary>
    /// Stretches an input across its cell. Single-line text boxes force their own height from the
    /// font, so they are centred vertically instead of being stretched into a taller row.
    /// </summary>
    private static void StretchInput(Control input, int height)
    {
        if (input is TextBox { Multiline: false })
        {
            input.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            return;
        }

        input.Height = height;
        input.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
    }

    public static Panel CreateInfoBox(string text)
    {
        var label = new Label
        {
            Text = text,
            Font = UiTheme.HelpFont,
            ForeColor = UiTheme.MutedText,
            AutoSize = true,
            Dock = DockStyle.Top,
            UseMnemonic = false,
            Padding = UiScale.Px(4, 2, 4, 2)
        };

        var box = new Panel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = UiTheme.HelpBg,
            Padding = UiScale.Px(14, 12, 14, 12),
            Margin = UiScale.Px(0, 10, 0, 0),
            Dock = DockStyle.Top
        };
        box.Paint += (_, e) =>
        {
            var r = box.ClientRectangle;
            r.Width -= 1;
            r.Height -= 1;
            using var pen = new Pen(UiTheme.Border);
            e.Graphics.DrawRectangle(pen, r);
        };
        box.Controls.Add(label);
        // Wrap rather than cut the help text off when the card is narrow.
        BindFullWidthLabel(label, box);
        return box;
    }

    /// <summary>Wraps a label so it uses the full width of the parent and wraps text.</summary>
    public static void BindFullWidthLabel(Label label, Control parent)
    {
        void UpdateWidth(object? _, EventArgs __)
        {
            var w = Math.Max(UiScale.Px(200), parent.ClientSize.Width - parent.Padding.Horizontal - UiScale.Px(8));
            label.MaximumSize = new Size(w, 0);
        }

        parent.Resize += UpdateWidth;
        UpdateWidth(null, EventArgs.Empty);
    }
}
