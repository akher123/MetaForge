namespace MetaForge.Scaffold.WinForms;

internal static class LayoutKit
{
    public static Panel CreateCard(string title, Control body, bool fillVertical = false)
    {
        var card = new Panel
        {
            Dock = fillVertical ? DockStyle.Fill : DockStyle.Top,
            BackColor = UiTheme.Surface,
            Margin = fillVertical ? Padding.Empty : new Padding(0, 0, 0, 12),
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
            Height = 44,
            BackColor = UiTheme.CardTitleBg,
            Padding = new Padding(16, 0, 16, 0)
        };
        titleBar.Controls.Add(new Label
        {
            Text = title,
            Font = UiTheme.UiFontBold,
            ForeColor = UiTheme.TextPrimary,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
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
            Width = 100,
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

    public static void AddInputRow(TableLayoutPanel grid, ref int row, Control input, int height = 34)
    {
        input.Height = height;
        input.Dock = DockStyle.Fill;
        input.Margin = new Padding(0, 0, 0, 8);
        grid.Controls.Add(input, 0, row);
        grid.RowStyles[row] = new RowStyle(SizeType.Absolute, height);
        row++;
    }

    public static Panel CreateInputButtonRow(Control input, Button button, int height = 34)
    {
        const int buttonWidth = 112;
        const int gap = 10;

        var row = new Panel
        {
            Height = height,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 8),
            MinimumSize = new Size(buttonWidth + gap + 120, height)
        };

        input.Margin = Padding.Empty;
        input.Height = height;
        input.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        button.Width = buttonWidth;
        button.Height = height;
        button.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        void LayoutRow()
        {
            var availableWidth = Math.Max(buttonWidth + gap, row.ClientSize.Width);
            button.Location = new Point(availableWidth - buttonWidth, 0);
            input.Location = new Point(0, 0);
            input.Width = Math.Max(0, availableWidth - buttonWidth - gap);
        }

        row.Controls.Add(input);
        row.Controls.Add(button);
        row.Resize += (_, _) => LayoutRow();
        row.HandleCreated += (_, _) => LayoutRow();
        LayoutRow();
        return row;
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
            Padding = new Padding(4, 2, 4, 2)
        };

        var box = new Panel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = UiTheme.HelpBg,
            Padding = new Padding(14, 12, 14, 12),
            Margin = new Padding(0, 10, 0, 0),
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
        return box;
    }

    /// <summary>Wraps a label so it uses the full width of the parent and wraps text.</summary>
    public static void BindFullWidthLabel(Label label, Control parent)
    {
        void UpdateWidth(object? _, EventArgs __)
        {
            var w = Math.Max(200, parent.ClientSize.Width - parent.Padding.Horizontal - 8);
            label.MaximumSize = new Size(w, 0);
        }

        parent.Resize += UpdateWidth;
        UpdateWidth(null, EventArgs.Empty);
    }
}
