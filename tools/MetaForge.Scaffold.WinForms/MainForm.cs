using MetaForge.Scaffold;
using Microsoft.Extensions.Configuration;

namespace MetaForge.Scaffold.WinForms;

public sealed class MainForm : Form
{
    private readonly ToolTip _tooltips = new();

    private readonly TextBox _txtSolutionRoot = new();
    private readonly TextBox _txtConnection = new() { Multiline = true, ScrollBars = ScrollBars.Vertical };
    private readonly TextBox _txtEntityName = new();
    private readonly TextBox _txtTableName = new();
    private readonly TextBox _txtColumns = new() { Multiline = true, ScrollBars = ScrollBars.Both, AcceptsReturn = true };
    private readonly TextBox _txtReverseTable = new();
    private readonly TextBox _txtEntityOverride = new();
    private readonly RichTextBox _txtOutput = new() { ReadOnly = true, BorderStyle = BorderStyle.None };
    private readonly TabControl _modeTabs = new();
    private readonly TabPage _tabGreenfield = new("New entity");
    private readonly TabPage _tabReverse = new("From database table");
    private readonly CheckBox _chkIncludeNav = new() { Text = "Include navigation properties (FKs)" };
    private readonly CheckBox _chkForce = new() { Text = "Overwrite existing files" };
    private readonly CheckBox _chkMigration = new() { Text = "Create EF migration after scaffold" };
    private readonly CheckBox _chkNoDbSet = new() { Text = "Do not add DbSet to DbContext" };
    private readonly Button _btnPreview = new() { Text = "Preview", Width = 120 };
    private readonly Button _btnRun = new() { Text = "Scaffold", Width = 140 };
    private readonly Button _btnClearOutput = new() { Text = "Clear output" };
    private readonly ToolStripStatusLabel _lblStatus = new() { Text = "Ready" };
    private readonly Label _lblOutputHint = new();
    private Action _syncStepScrollHeight = () => { };
    private Action _resetStepScroll = () => { };

    public MainForm()
    {
        // Every layout constant below is authored at 96 DPI, so the scale has to be known first.
        UiScale.SyncWith(this);

        Text = "MetaForge Entity Scaffold";
        Font = UiTheme.UiFont;
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = UiTheme.Background;
        MinimumSize = MinimumWindowSize();
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;

        ApplyControlStyles();
        BuildLayout();
        WireEvents();
        RegisterTooltips();
        TryDetectSolutionRoot();
        UpdateModeHint();
    }

    /// <summary>
    /// Never demand more room than the display actually offers, otherwise parts of the window
    /// end up off-screen on smaller or heavily scaled monitors.
    /// </summary>
    private static Size MinimumWindowSize()
    {
        var desired = UiScale.Px(980, 620);
        var workingArea = Screen.PrimaryScreen?.WorkingArea.Size ?? desired;
        return new Size(Math.Min(desired.Width, workingArea.Width), Math.Min(desired.Height, workingArea.Height));
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // Card heights are only final once the form has been laid out at its real size.
        _syncStepScrollHeight();

        // Activation focuses a field inside the tab control, which scrolls Step 2 into view. Undo
        // that once the queued activation work has run so the form always opens showing Step 1.
        BeginInvoke(() =>
        {
            ActiveControl = _txtSolutionRoot;
            _resetStepScroll();
        });
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        UiScale.SyncWith(this);
        MinimumSize = MinimumWindowSize();
        _syncStepScrollHeight();
    }

    private void ApplyControlStyles()
    {
        foreach (var box in new[] { _txtSolutionRoot, _txtConnection, _txtEntityName, _txtTableName, _txtColumns, _txtReverseTable, _txtEntityOverride })
            UiTheme.StyleTextBox(box);

        _txtSolutionRoot.PlaceholderText = @"D:\Nextframwork";
        _txtConnection.PlaceholderText = "Paste SQL Server connection string";
        _txtEntityName.PlaceholderText = "Warehouse";
        _txtTableName.PlaceholderText = "Warehouses (optional)";
        _txtColumns.PlaceholderText = "Code:string:50!, Name:string:200!, IsActive:bool!";
        _txtReverseTable.PlaceholderText = "Warehouses";
        _txtEntityOverride.PlaceholderText = "Warehouse (optional)";

        _txtColumns.Font = UiTheme.CodeFont;
        _txtOutput.Font = UiTheme.CodeFont;
        _txtOutput.BackColor = UiTheme.CodeBg;
        _txtOutput.ForeColor = UiTheme.CodeFg;

        UiTheme.StyleSecondaryButton(_btnPreview);
        UiTheme.StylePrimaryButton(_btnRun);
        UiTheme.StyleSecondaryButton(_btnClearOutput);

        _modeTabs.Font = UiTheme.UiFont;
        // Tabs sized from their own text, so a narrow card does not push them behind scroll arrows.
        _modeTabs.SizeMode = TabSizeMode.Normal;
        _modeTabs.Padding = new Point(UiScale.Px(18), UiScale.Px(6));
        _modeTabs.BackColor = UiTheme.Surface;

        StyleCheckBox(_chkIncludeNav);
        StyleCheckBox(_chkForce);
        StyleCheckBox(_chkMigration);
        StyleCheckBox(_chkNoDbSet);
    }

    private static void StyleCheckBox(CheckBox checkBox)
    {
        checkBox.Font = UiTheme.UiFont;
        checkBox.ForeColor = UiTheme.TextPrimary;
        checkBox.AutoSize = true;
        checkBox.Margin = UiScale.Px(0, 8, 0, 0);
    }

    private void BuildLayout()
    {
        SuspendLayout();

        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = UiTheme.Background,
            Padding = Padding.Empty
        };
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var header = BuildHeader();
        header.Dock = DockStyle.Fill;
        var workspace = BuildWorkspace();
        workspace.Dock = DockStyle.Fill;
        var status = BuildStatusStrip();
        status.Dock = DockStyle.Fill;

        shell.Controls.Add(header, 0, 0);
        shell.Controls.Add(workspace, 0, 1);
        shell.Controls.Add(status, 0, 2);

        Controls.Add(shell);
        ResumeLayout(true);
    }

    private Control BuildWorkspace()
    {
        var workspace = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = UiTheme.PagePadding,
            BackColor = UiTheme.Background
        };
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48f));
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52f));

        workspace.Controls.Add(BuildLeftColumn(), 0, 0);
        workspace.Controls.Add(BuildOutputCard(), 1, 0);
        return workspace;
    }

    private Control BuildLeftColumn()
    {
        var steps = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = UiTheme.Background,
            Margin = Padding.Empty
        };
        steps.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        steps.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        steps.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        steps.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));

        // Both step cards fill an explicitly sized row rather than auto-sizing: a card's preferred
        // size is measured unwrapped, which is too short once its content wraps onto more lines.
        var (project, projectFields) = BuildProjectCard();
        project.Dock = DockStyle.Fill;
        project.Margin = UiScale.Px(0, 0, 0, 12);

        var mode = BuildModeCard();
        mode.Dock = DockStyle.Fill;
        mode.Margin = UiScale.Px(0, 0, 0, 12);

        var (options, optionFlow) = BuildOptionsCard();
        options.Dock = DockStyle.Fill;
        options.Margin = Padding.Empty;

        steps.Controls.Add(project, 0, 0);
        steps.Controls.Add(mode, 0, 1);
        steps.Controls.Add(options, 0, 2);

        // Where the display is too short for all three cards, scroll them instead of clipping
        // whatever runs off the bottom. AutoScrollMinSize (not MinimumSize) is what grows the
        // host's display rectangle, which is the area a docked child is laid out in.
        var scrollHost = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = UiTheme.Background,
            Margin = Padding.Empty
        };
        scrollHost.Controls.Add(steps);
        _resetStepScroll = () => scrollHost.AutoScrollPosition = Point.Empty;

        _syncStepScrollHeight = () =>
        {
            var projectRow = RequiredCardHeight(projectFields) + project.Margin.Vertical;
            var optionsRow = RequiredCardHeight(optionFlow) + options.Margin.Vertical;

            if (Math.Abs(steps.RowStyles[0].Height - projectRow) > 0.5f)
                steps.RowStyles[0] = new RowStyle(SizeType.Absolute, projectRow);
            if (Math.Abs(steps.RowStyles[2].Height - optionsRow) > 0.5f)
                steps.RowStyles[2] = new RowStyle(SizeType.Absolute, optionsRow);

            var required = projectRow + mode.Margin.Vertical + MinimumModeCardHeight + optionsRow;
            if (scrollHost.AutoScrollMinSize.Height != required)
                scrollHost.AutoScrollMinSize = new Size(0, required);
        };

        projectFields.Resize += (_, _) => _syncStepScrollHeight();
        optionFlow.Resize += (_, _) => _syncStepScrollHeight();
        scrollHost.Resize += (_, _) => _syncStepScrollHeight();

        // Preview/Scaffold stays pinned below the scroll area so the primary actions are always reachable.
        var actions = BuildActionsBar();
        actions.Dock = DockStyle.Fill;
        actions.Margin = UiScale.Px(0, 12, 0, 0);

        var column = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = UiTheme.Background,
            Margin = UiScale.Px(0, 0, 10, 0)
        };
        column.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        column.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        column.RowStyles.Add(new RowStyle(SizeType.Absolute, ActionsBarHeight + actions.Margin.Vertical));
        column.Controls.Add(scrollHost, 0, 0);
        column.Controls.Add(actions, 0, 1);
        return column;
    }

    /// <summary>
    /// Height an auto-sizing card needs. Measured from its content, whose docked layout keeps its
    /// full height, rather than from the card itself, which the surrounding table may have squeezed.
    /// </summary>
    private static int RequiredCardHeight(Control content) =>
        LayoutKit.CardTitleHeight
        + (content.Parent?.Padding.Vertical ?? 0)
        + content.Height
        + content.Margin.Vertical;

    /// <summary>Room for the Step 2 card title, tab strip and the tallest tab page content.</summary>
    private int MinimumModeCardHeight
    {
        get
        {
            var content = 0;
            foreach (TabPage tab in _modeTabs.TabPages)
                foreach (Control child in tab.Controls)
                    content = Math.Max(content, child.PreferredSize.Height);

            // ItemSize reports 0 until the tab control has a handle, so keep a font-derived floor.
            var tabStrip = Math.Max(_modeTabs.ItemSize.Height, UiTheme.UiFont.Height + UiScale.Px(16));

            return LayoutKit.CardTitleHeight
                   + UiTheme.CardBodyPadding.Vertical
                   + tabStrip
                   + content
                   + UiScale.Px(18);
        }
    }

    private static int ActionsBarHeight => UiTheme.PrimaryButtonHeight + UiScale.Px(22);

    private Control BuildHeader()
    {
        // Sized by its own text so the subtitle cannot be cropped by a fixed header height.
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = UiTheme.HeaderBg,
            Padding = UiScale.Px(24, 12, 24, 12)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var titles = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = Padding.Empty
        };
        titles.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        titles.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        titles.Controls.Add(new Label
        {
            Text = "MetaForge Entity Scaffold",
            Font = UiTheme.TitleFont,
            ForeColor = UiTheme.HeaderFg,
            AutoSize = true,
            Margin = Padding.Empty,
            UseMnemonic = false
        }, 0, 0);
        titles.Controls.Add(new Label
        {
            Text = "Generate entities · EF configuration · Form Builder screens",
            Font = UiTheme.SubtitleFont,
            ForeColor = Color.FromArgb(195, 210, 230),
            AutoSize = true,
            Margin = UiScale.Px(0, 2, 0, 0),
            UseMnemonic = false
        }, 0, 1);

        layout.Controls.Add(titles, 0, 0);
        layout.Controls.Add(new Label
        {
            Text = "1 Project  →  2 Entity  →  3 Options  →  Preview / Scaffold",
            Font = UiTheme.HelpFont,
            ForeColor = Color.FromArgb(170, 190, 215),
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            TextAlign = ContentAlignment.MiddleRight,
            UseMnemonic = false
        }, 1, 0);

        panel.Controls.Add(layout);
        return panel;
    }

    private (Panel Card, Control Content) BuildProjectCard()
    {
        // The body fills the card, whose row is sized from this grid's measured height.
        var body = LayoutKit.CreateBodyPanel(fill: true);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 4,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Width = UiScale.Px(100)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        body.Resize += (_, _) =>
            grid.Width = Math.Max(UiScale.Px(200), body.ClientSize.Width - body.Padding.Horizontal);

        var lblSolution = UiTheme.CreateCaption("Solution folder");
        lblSolution.Margin = UiScale.Px(0, 6, 0, 10);
        lblSolution.Font = new Font(UiTheme.UiFont, FontStyle.Bold);
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.Controls.Add(lblSolution, 0, 0);

        var browse = new Button { Text = "Browse…" };
        UiTheme.StyleSecondaryButton(browse);
        browse.Click += (_, _) => BrowseSolutionRoot();
        AddFixedRow(grid, LayoutKit.CreateInputButtonRow(_txtSolutionRoot, browse), 1);

        var lblConnection = UiTheme.CreateCaption("SQL Server connection");
        lblConnection.Margin = UiScale.Px(0, 10, 0, 6);
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.Controls.Add(lblConnection, 0, 2);

        var loadConn = new Button { Text = "Load" };
        UiTheme.StyleSecondaryButton(loadConn);
        loadConn.Click += (_, _) => LoadConnectionFromAppSettings();
        AddFixedRow(grid, LayoutKit.CreateInputButtonRow(_txtConnection, loadConn, 64), 3);

        body.Controls.Add(grid);
        return (LayoutKit.CreateCard("Step 1 — Project & database", body, fillVertical: true), grid);
    }

    /// <summary>Adds a row sized to exactly what the control asked for, margins included.</summary>
    private static void AddFixedRow(TableLayoutPanel grid, Control control, int row)
    {
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, control.Height + control.Margin.Vertical));
        grid.Controls.Add(control, 0, row);
    }

    private Panel BuildModeCard()
    {
        _tabGreenfield.Controls.Add(BuildGreenfieldTab());
        _tabReverse.Controls.Add(BuildReverseTab());
        _modeTabs.TabPages.Add(_tabGreenfield);
        _modeTabs.TabPages.Add(_tabReverse);
        _modeTabs.Dock = DockStyle.Fill;
        _modeTabs.SelectedIndexChanged += (_, _) => UpdateModeHint();

        var body = LayoutKit.CreateBodyPanel(fill: true);
        body.Controls.Add(_modeTabs);
        return LayoutKit.CreateCard("Step 2 — Entity definition", body, fillVertical: true);
    }

    private Control BuildGreenfieldTab()
    {
        var page = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = UiTheme.Surface,
            Padding = UiScale.Px(4, 6, 4, 6)
        };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 5,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        top.RowStyles.Add(new RowStyle(SizeType.Absolute, UiTheme.ControlHeight));
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        top.RowStyles.Add(new RowStyle(SizeType.Absolute, UiTheme.ControlHeight));
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        top.Controls.Add(UiTheme.CreateCaption("Entity class name"), 0, 0);
        _txtEntityName.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        top.Controls.Add(_txtEntityName, 0, 1);
        top.Controls.Add(UiTheme.CreateCaption("Table name (optional)"), 0, 2);
        _txtTableName.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        top.Controls.Add(_txtTableName, 0, 3);

        var columnsHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        columnsHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        columnsHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        columnsHeader.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        columnsHeader.Controls.Add(UiTheme.CreateCaption("Column definitions"), 0, 0);
        var sampleBtn = new Button { Text = "Load example", AutoSize = true };
        UiTheme.StyleSecondaryButton(sampleBtn);
        sampleBtn.Click += (_, _) => _txtColumns.Text = "Code:string:50!, Name:string:200!, IsActive:bool!";
        columnsHeader.Controls.Add(sampleBtn, 1, 0);
        top.Controls.Add(columnsHeader, 0, 4);

        _txtColumns.Dock = DockStyle.Fill;
        _txtColumns.MinimumSize = new Size(0, UiScale.Px(80));
        _txtColumns.Margin = UiScale.Px(0, 6, 0, 0);

        page.Controls.Add(top, 0, 0);
        page.Controls.Add(_txtColumns, 0, 1);
        page.Controls.Add(LayoutKit.CreateInfoBox(
            "Format: Name:type[:size][!]  (! = required)\r\n" +
            "Code:string:50! · Name:string:200 · IsActive:bool! · Amount:decimal:18,2 · CountryId:int"), 0, 2);

        return page;
    }

    private Control BuildReverseTab()
    {
        var page = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = UiTheme.Surface,
            Padding = UiScale.Px(4, 6, 4, 6)
        };
        page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 4,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, UiTheme.ControlHeight));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, UiTheme.ControlHeight));

        grid.Controls.Add(UiTheme.CreateCaption("Database table name"), 0, 0);
        _txtReverseTable.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        grid.Controls.Add(_txtReverseTable, 0, 1);
        grid.Controls.Add(UiTheme.CreateCaption("Entity class name (optional)"), 0, 2);
        _txtEntityOverride.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        grid.Controls.Add(_txtEntityOverride, 0, 3);

        page.Controls.Add(grid, 0, 0);
        page.Controls.Add(LayoutKit.CreateInfoBox(
            "Reads SQL Server table → entity, EF config, DbSet.\r\n" +
            "Requires int Id primary key. Load connection in Step 1."), 0, 1);

        return page;
    }

    private (Panel Card, Control Content) BuildOptionsCard()
    {
        var body = LayoutKit.CreateBodyPanel(fill: true);

        // Flows into two columns where there is room and one where there is not, so a narrow card
        // makes the options taller instead of cutting their labels off.
        var options = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = UiTheme.Surface
        };

        foreach (var chk in new[] { _chkIncludeNav, _chkMigration, _chkForce, _chkNoDbSet })
        {
            chk.Font = UiTheme.UiFont;
            chk.ForeColor = UiTheme.TextPrimary;
            chk.AutoSize = true;
            chk.Margin = UiScale.Px(4, 6, 20, 6);
            options.Controls.Add(chk);
        }

        body.Controls.Add(options);
        return (LayoutKit.CreateCard("Step 3 — Options", body, fillVertical: true), options);
    }

    private Panel BuildActionsBar()
    {
        var bar = new Panel
        {
            BackColor = UiTheme.Surface,
            Padding = UiScale.Px(16, 10, 16, 10),
            Margin = Padding.Empty
        };
        bar.Paint += (_, e) =>
        {
            var r = bar.ClientRectangle;
            r.Width -= 1;
            r.Height -= 1;
            using var pen = new Pen(UiTheme.Border);
            e.Graphics.DrawRectangle(pen, r);
        };

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        _btnPreview.Dock = DockStyle.Fill;
        _btnPreview.Margin = UiScale.Px(0, 0, 8, 0);
        _btnRun.Dock = DockStyle.Fill;
        _btnRun.Margin = UiScale.Px(8, 0, 0, 0);

        layout.Controls.Add(_btnPreview, 0, 0);
        layout.Controls.Add(_btnRun, 1, 0);
        bar.Controls.Add(layout);
        return bar;
    }

    private Control BuildOutputCard()
    {
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = UiTheme.CardBodyPadding
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, UiTheme.ControlHeight + UiScale.Px(6)));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        _lblOutputHint.Font = UiTheme.HelpFont;
        _lblOutputHint.ForeColor = UiTheme.MutedText;
        _lblOutputHint.AutoSize = true;
        _lblOutputHint.Dock = DockStyle.Fill;
        _lblOutputHint.UseMnemonic = false;
        LayoutKit.BindFullWidthLabel(_lblOutputHint, body);

        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiScale.Px(120)));
        toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _btnClearOutput.Dock = DockStyle.Fill;
        _btnClearOutput.Click += (_, _) =>
        {
            _txtOutput.Clear();
            _lblStatus.Text = "Output cleared.";
        };
        toolbar.Controls.Add(_btnClearOutput, 1, 0);

        var outputFrame = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.Border, Padding = new Padding(1) };
        _txtOutput.Dock = DockStyle.Fill;
        outputFrame.Controls.Add(_txtOutput);

        body.Controls.Add(_lblOutputHint, 0, 0);
        body.Controls.Add(toolbar, 0, 1);
        body.Controls.Add(outputFrame, 0, 2);

        var card = LayoutKit.CreateCard("Preview & output", body, fillVertical: true);
        card.Dock = DockStyle.Fill;
        card.Margin = UiScale.Px(10, 0, 0, 0);
        return card;
    }

    private StatusStrip BuildStatusStrip()
    {
        var strip = new StatusStrip
        {
            BackColor = UiTheme.Surface,
            SizingGrip = true,
            Padding = UiScale.Px(8, 2, 8, 2)
        };
        _lblStatus.Font = UiTheme.UiFont;
        strip.Items.Add(new ToolStripStatusLabel { Spring = true });
        strip.Items.Add(_lblStatus);
        return strip;
    }

    private void WireEvents()
    {
        _btnPreview.Click += async (_, _) => await RunScaffoldAsync(dryRun: true);
        _btnRun.Click += async (_, _) => await RunScaffoldAsync(dryRun: false);
    }

    private void RegisterTooltips()
    {
        _tooltips.SetToolTip(_txtSolutionRoot, "Folder containing MetaForge.slnx");
        _tooltips.SetToolTip(_txtConnection, "Required for reverse scaffold from database");
        _tooltips.SetToolTip(_txtEntityName, "C# class name, e.g. Warehouse");
        _tooltips.SetToolTip(_txtTableName, "Leave empty to auto-pluralize (Warehouse → Warehouses)");
        _tooltips.SetToolTip(_txtColumns, "Comma-separated: Name:type[:size][!]");
        _tooltips.SetToolTip(_txtReverseTable, "Table name in SQL Server");
        _tooltips.SetToolTip(_btnPreview, "Preview generated code without writing files");
        _tooltips.SetToolTip(_btnRun, "Write entity files and update DbContext");
        _tooltips.SetToolTip(_chkMigration, "Runs dotnet ef migrations add");
    }

    private void UpdateModeHint()
    {
        _lblOutputHint.Text = _modeTabs.SelectedTab == _tabReverse
            ? "Reverse mode: load connection string, enter table name, then Preview or Scaffold."
            : "Greenfield mode: define entity and columns, then Preview or Scaffold.";
    }

    private bool IsGreenfieldMode => _modeTabs.SelectedTab == _tabGreenfield;

    private void TryDetectSolutionRoot()
    {
        try
        {
            _txtSolutionRoot.Text = SolutionRootResolver.Resolve(".");
            _lblStatus.Text = "Solution folder detected.";
        }
        catch
        {
            _txtSolutionRoot.Text = Directory.GetCurrentDirectory();
            _lblStatus.Text = "Set solution folder.";
        }
    }

    private void BrowseSolutionRoot()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select folder containing MetaForge.slnx",
            SelectedPath = _txtSolutionRoot.Text,
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _txtSolutionRoot.Text = dialog.SelectedPath;
            _lblStatus.Text = "Solution folder updated.";
        }
    }

    private void LoadConnectionFromAppSettings()
    {
        try
        {
            var root = _txtSolutionRoot.Text.Trim();
            var configPath = Path.Combine(root, "src/MetaForge.Web/appsettings.json");
            if (!File.Exists(configPath))
            {
                MessageBox.Show($"appsettings.json not found:{Environment.NewLine}{configPath}", "Connection",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var configuration = new ConfigurationBuilder().AddJsonFile(configPath).Build();
            var cs = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(cs))
            {
                MessageBox.Show("DefaultConnection is empty.", "Connection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _txtConnection.Text = cs;
            _lblStatus.Text = "Connection loaded.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Connection", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private ScaffoldOptions? BuildOptions(bool dryRun)
    {
        if (string.IsNullOrWhiteSpace(_txtSolutionRoot.Text))
        {
            MessageBox.Show("Please set the solution folder.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        var options = new ScaffoldOptions
        {
            SolutionRoot = _txtSolutionRoot.Text.Trim(),
            ConnectionString = string.IsNullOrWhiteSpace(_txtConnection.Text) ? null : _txtConnection.Text.Trim(),
            IncludeNavigations = _chkIncludeNav.Checked,
            Force = _chkForce.Checked,
            AddMigration = _chkMigration.Checked,
            NoDbSetPatch = _chkNoDbSet.Checked,
            DryRun = dryRun
        };

        if (IsGreenfieldMode)
        {
            if (string.IsNullOrWhiteSpace(_txtEntityName.Text))
            {
                MessageBox.Show("Enter entity class name.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _modeTabs.SelectedTab = _tabGreenfield;
                _txtEntityName.Focus();
                return null;
            }

            if (string.IsNullOrWhiteSpace(_txtColumns.Text))
            {
                MessageBox.Show("Enter column definitions.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _modeTabs.SelectedTab = _tabGreenfield;
                _txtColumns.Focus();
                return null;
            }

            options.EntityName = _txtEntityName.Text.Trim();
            options.TableName = string.IsNullOrWhiteSpace(_txtTableName.Text) ? null : _txtTableName.Text.Trim();
            options.Columns = NormalizeColumns(_txtColumns.Text);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(_txtConnection.Text))
            {
                MessageBox.Show("Connection string is required for reverse scaffold.", Text, MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                _txtConnection.Focus();
                return null;
            }

            if (string.IsNullOrWhiteSpace(_txtReverseTable.Text))
            {
                MessageBox.Show("Enter database table name.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _modeTabs.SelectedTab = _tabReverse;
                _txtReverseTable.Focus();
                return null;
            }

            options.TableName = _txtReverseTable.Text.Trim();
            options.EntityName = string.IsNullOrWhiteSpace(_txtEntityOverride.Text) ? null : _txtEntityOverride.Text.Trim();
        }

        return options;
    }

    private static string NormalizeColumns(string text) =>
        string.Join(",", text.Split([',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private async Task RunScaffoldAsync(bool dryRun)
    {
        var options = BuildOptions(dryRun);
        if (options == null)
            return;

        SetBusy(true);
        _txtOutput.Clear();
        _lblStatus.Text = dryRun ? "Generating preview…" : "Writing files…";

        try
        {
            var result = await new ScaffoldOrchestrator().RunAsync(options);
            _txtOutput.Text = ScaffoldResultFormatter.Format(result);
            _lblStatus.ForeColor = UiTheme.Success;
            _lblStatus.Text = dryRun
                ? $"Preview: {result.EntityName} / {result.TableName}"
                : $"Done: {result.EntityName} ({result.WrittenFiles.Count} files)";

            if (!dryRun)
            {
                MessageBox.Show(
                    $"Scaffolded {result.EntityName}.{Environment.NewLine}{Environment.NewLine}Next: build, run app, Form Builder → Auto-Build.",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            _txtOutput.Text = ex.ToString();
            _lblStatus.ForeColor = Color.DarkRed;
            _lblStatus.Text = "Failed — see output.";
            MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _btnPreview.Enabled = !busy;
        _btnRun.Enabled = !busy;
        _modeTabs.Enabled = !busy;
        UseWaitCursor = busy;
        if (!busy)
            _lblStatus.ForeColor = SystemColors.ControlText;
    }
}
