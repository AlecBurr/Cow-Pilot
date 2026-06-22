using System.Globalization;
using System.Text.Json;

namespace CowPilot;

sealed class MainForm : Form
{
    private const string QuoteFileExtension = ".cowpilot";
    private static readonly Color ChangedBoxColor = Color.LightBlue;
    private static readonly Color ChangedTextColor = Color.FromArgb(128, 24, 24);
    private static readonly Color SuccessColor = Color.FromArgb(46, 125, 50);
    private static readonly Color ErrorColor = Color.FromArgb(183, 28, 28);
    private static readonly Color CalculatingColor = Color.DimGray;

    private readonly TextBox _measurements = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 10) };
    private readonly TextBox _formatted = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, Font = new Font("Consolas", 10) };
    private readonly TextBox _totalLf = ReadOnlyBox();
    private readonly TextBox _screwBags = new() { Text = "0", TextAlign = HorizontalAlignment.Right };
    private readonly TextBox _lapScrewBags = new() { Text = "0", TextAlign = HorizontalAlignment.Right };
    private readonly Label _suggestedScrews = new() { Text = "Suggested: 0", AutoSize = true };
    private readonly Label _suggestedLapScrews = new() { Text = "Suggested: 0", AutoSize = true };
    private readonly Label _status = new() { Dock = DockStyle.Fill, Height = 24, BorderStyle = BorderStyle.Fixed3D, Text = "Ready", BackColor = CalculatingColor, ForeColor = Color.White };
    private readonly Label _centerOfBalance = new() { AutoSize = false, Height = 34, MinimumSize = new Size(240, 34), TextAlign = ContentAlignment.MiddleLeft, BorderStyle = BorderStyle.Fixed3D };
    private readonly Label _weight = new() { AutoSize = false, Height = 34, MinimumSize = new Size(240, 34), TextAlign = ContentAlignment.MiddleLeft, BorderStyle = BorderStyle.Fixed3D };
    private readonly CheckBox _useSuggested = new() { Text = "Use suggested screws", AutoSize = true };
    private readonly CheckBox _militaryDiscount = new() { Text = "Military discount", AutoSize = true };
    private readonly RadioButton[] _screwButtons;
    private readonly Dictionary<ScrewOption, RadioButton> _screwButtonMap = [];
    private readonly Dictionary<MetalOption, TextBox> _subtotalBoxes = [];
    private readonly Dictionary<MetalOption, TextBox> _grandTotalBoxes = [];
    private readonly Dictionary<string, NumericUpDown> _trimCounts = [];
    private readonly Dictionary<string, NumericUpDown> _trimExtras = [];
    private readonly NumericUpDown[] _miscCounts = [CountBox(), CountBox(), CountBox(), CountBox(), CountBox(), CountBox(), CountBox(), CountBox(), CountBox(), CountBox()];
    private readonly NumericUpDown[] _bootCounts = QuoteCalculator.BootCatalog.Select(_ => CountBox()).ToArray();
    private readonly CustomTrimControl _customTrim = new();
    private readonly NumericUpDown _customTrimQuantity = CountBox(1);
    private readonly ListBox _customTrimPiecesList = new() { Height = 110, IntegralHeight = false, HorizontalScrollbar = true };
    private readonly ListBox _customTrimVerticesList = new() { Height = 130, IntegralHeight = false };
    private readonly ListBox _customTrimSegmentsList = new() { Height = 145, IntegralHeight = false, HorizontalScrollbar = true };
    private readonly ListBox _customTrimAnglesList = new() { Height = 120, IntegralHeight = false, HorizontalScrollbar = true };
    private readonly ComboBox _customOriginSelector = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 115 };
    private readonly CheckBox _showCustomCoordinates = new() { Text = "Show vertex coordinates", AutoSize = true };
    private readonly Panel _customCoordinatesPanel = new() { Dock = DockStyle.Top, AutoSize = true, Visible = false };
    private readonly NumericUpDown _customVertexX = InchBox();
    private readonly NumericUpDown _customVertexY = InchBox();
    private readonly NumericUpDown _customSegmentLength = PositiveInchBox(12);
    private readonly TextBox _customInteriorAngle = new() { Width = 92 };
    private readonly Label _customTrimAdded = new() { Text = "Custom Trim Added", AutoSize = true, ForeColor = ChangedTextColor, Font = BoldFont(9), Visible = false };
    private readonly Label _customTrimSummary = new() { Dock = DockStyle.Bottom, Height = 24, BorderStyle = BorderStyle.Fixed3D };
    private readonly Label _customTrimPiece = new() { AutoSize = true, Text = "Piece: none" };
    private readonly PictureBox _mascot = new() { Width = 82, Height = 82, SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(8, 0, 8, 0) };
    private readonly System.Windows.Forms.Timer _mascotTimer = new();
    private Image? _idleMascot;
    private Image? _explodeMascot;
    private readonly TextBox _customer = new();
    private readonly TextBox _phone = new();
    private readonly TextBox _color = new();
    private readonly TextBox _notes = new() { Multiline = true, ScrollBars = ScrollBars.Vertical };
    private readonly DebugConsoleForm _console = new();
    private AppSettings _settings = SettingsStore.Load();
    private QuoteSet? _lastQuote;
    private string _centerOfBalanceClipboardText = "";
    private string _currentSavePath = "";
    private string _savedSnapshot = "";
    private bool _isDirty;
    private bool _suppress;
    private bool _syncingCustomTrimSidebar;

    public MainForm()
    {
        Text = $"{AppVersion.Name} version {AppVersion.Version}";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? Icon;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1100, 700);

        _screwButtons =
        [
            ScrewButton(ScrewOption.OneInch),
            ScrewButton(ScrewOption.OneAndHalfInch),
            ScrewButton(ScrewOption.TwoInch),
            ScrewButton(ScrewOption.Tubing)
        ];
        _screwButtons[0].Checked = true;
        LoadMascotImages();

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildQuoteTab());
        tabs.TabPages.Add(BuildCustomTrimTab());
        MainMenuStrip = BuildMenu();
        PrepareCustomTrimListBoxes();
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.Controls.Add(BuildTopBar(), 0, 0);
        shell.Controls.Add(tabs, 0, 1);
        shell.Controls.Add(_status, 0, 2);
        Controls.Add(shell);
        Controls.Add(_mascot);
        PositionMascot();
        _mascot.BringToFront();

        WireAutoCalc();
        _console.VerboseCalculationRequested += (_, _) => PrintCalculationTrace();
        _customTrim.TrimChanged += (_, _) =>
        {
            RefreshCustomTrimUi();
            MarkDirty();
        };
        _customTrim.SelectionChanged += (_, _) => RefreshCustomTrimUi();
        _customTrimQuantity.ValueChanged += (_, _) =>
        {
            if (!_suppress) _customTrim.SelectedQuantity = (int)_customTrimQuantity.Value;
        };
        _useSuggested.CheckedChanged += (_, _) => ApplySuggestedScrews();
        ApplyRuntimeSettings();
        _customTrimSummary.Text = _customTrim.Summary();
        SyncCustomTrimSidebar();
        Recalculate();
        ResetSavedSnapshot();
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("File");
        file.DropDownItems.Add("New", null, (_, _) => NewQuote());
        file.DropDownItems.Add("Load...", null, (_, _) => LoadQuote());
        file.DropDownItems.Add("Save", null, (_, _) => SaveQuote());
        file.DropDownItems.Add("Save as...", null, (_, _) => SaveAs());
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("Exit", null, (_, _) => Close());
        menu.Items.Add(file);

        var edit = new ToolStripMenuItem("Edit");
        edit.DropDownItems.Add("Cut", null, (_, _) => SendKeys.SendWait("^x"));
        edit.DropDownItems.Add("Copy", null, (_, _) => SendKeys.SendWait("^c"));
        edit.DropDownItems.Add("Paste", null, (_, _) => SendKeys.SendWait("^v"));
        edit.DropDownItems.Add("Select All", null, (_, _) => SendKeys.SendWait("^a"));
        menu.Items.Add(edit);

        var view = new ToolStripMenuItem("View");
        view.DropDownItems.Add("Console", null, (_, _) => ShowConsole());
        menu.Items.Add(view);

        var options = new ToolStripMenuItem("Options");
        options.DropDownItems.Add("Settings", null, (_, _) => ShowSettings());
        menu.Items.Add(options);

        var help = new ToolStripMenuItem("Help") { Alignment = ToolStripItemAlignment.Right };
        help.DropDownItems.Add("Calculator Guide", null, (_, _) => ShowHelpGuide());
        menu.Items.Add(help);
        return menu;
    }

    private Control BuildTopBar()
    {
        var top = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty, Padding = Padding.Empty };
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var menu = MainMenuStrip ?? throw new InvalidOperationException("Main menu is not initialized.");
        menu.Dock = DockStyle.Fill;
        menu.Padding = new Padding(menu.Padding.Left, menu.Padding.Top, _mascot.Width + 20, menu.Padding.Bottom);
        top.Controls.Add(menu, 0, 0);
        top.Controls.Add(BuildToolBar(), 0, 1);
        return top;
    }

    private ToolStrip BuildToolBar()
    {
        var toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Fill };
        toolbar.Items.Add(ToolbarButton(BuildNewIcon(), "New", (_, _) => NewQuote()));
        toolbar.Items.Add(ToolbarButton(BuildOpenIcon(), "Load quote", (_, _) => LoadQuote()));
        var save = new ToolStripButton
        {
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            Image = BuildSaveIcon(),
            Text = "Save",
            ToolTipText = "Save"
        };
        save.Click += (_, _) => SaveQuote();
        toolbar.Items.Add(save);
        return toolbar;
    }

    private void LoadMascotImages()
    {
        string baseDir = AppContext.BaseDirectory;
        string idlePath = Path.Combine(baseDir, "Assets", "idle lossy.gif");
        string explodePath = Path.Combine(baseDir, "Assets", "explode lossy.gif");
        if (!File.Exists(idlePath)) idlePath = Path.Combine(Application.StartupPath, "Assets", "idle lossy.gif");
        if (!File.Exists(explodePath)) explodePath = Path.Combine(Application.StartupPath, "Assets", "explode lossy.gif");
        if (File.Exists(idlePath)) _idleMascot = Image.FromFile(idlePath);
        if (File.Exists(explodePath)) _explodeMascot = Image.FromFile(explodePath);
        _mascot.Visible = _idleMascot != null;
        _mascot.Image = _idleMascot;
        _mascotTimer.Tick += (_, _) =>
        {
            _mascotTimer.Stop();
            _mascot.Image = _idleMascot;
        };
    }

    private void PlayMascotExplosion()
    {
        if (_explodeMascot == null || _idleMascot == null) return;
        _mascotTimer.Stop();
        _mascot.Image = _explodeMascot;
        _mascotTimer.Interval = Math.Max(500, GifDurationMs(_explodeMascot));
        _mascotTimer.Start();
    }

    private void PositionMascot()
    {
        _mascot.Location = new Point(Math.Max(0, ClientSize.Width - _mascot.Width - 12), 4);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        PositionMascot();
    }

    private static int GifDurationMs(Image image)
    {
        const int frameDelayProperty = 0x5100;
        if (!image.PropertyIdList.Contains(frameDelayProperty)) return 1500;
        byte[] values = image.GetPropertyItem(frameDelayProperty)?.Value ?? [];
        int total = 0;
        for (int i = 0; i + 3 < values.Length; i += 4)
        {
            total += Math.Max(2, BitConverter.ToInt32(values, i)) * 10;
        }
        return total <= 0 ? 1500 : total;
    }

    private void PrepareCustomTrimListBoxes()
    {
        foreach (var list in new[] { _customTrimPiecesList, _customTrimSegmentsList, _customTrimAnglesList })
        {
            list.DrawMode = DrawMode.OwnerDrawFixed;
            list.ItemHeight = 22;
            list.DrawItem += DrawCustomTrimListItem;
        }
    }

    private void DrawCustomTrimListItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ListBox list || e.Index < 0) return;
        int pieceIndex = list == _customTrimPiecesList ? e.Index : _customTrim.SelectedPieceIndex;
        bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        Color back = selected ? _customTrim.SelectedDisplayColor : _customTrim.PieceDisplayColor(pieceIndex);
        using var background = new SolidBrush(back);
        e.Graphics.FillRectangle(background, e.Bounds);

        int x = e.Bounds.Left + 4;
        if (list == _customTrimSegmentsList)
        {
            using var pen = new Pen(Contrast(back), 2);
            int y = e.Bounds.Top + e.Bounds.Height / 2;
            e.Graphics.DrawLine(pen, x, y + 4, x + 14, y - 4);
            x += 20;
        }

        TextRenderer.DrawText(e.Graphics, list.Items[e.Index].ToString(), list.Font,
            new Rectangle(x, e.Bounds.Top, e.Bounds.Width - (x - e.Bounds.Left), e.Bounds.Height),
            Contrast(back), TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        e.DrawFocusRectangle();
    }

    private static Color Contrast(Color color) => color.GetBrightness() < 0.45 ? Color.White : Color.Black;

    private TabPage BuildQuoteTab()
    {
        var page = new TabPage("Calculator");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 3, Padding = new Padding(8) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 22));
        page.Controls.Add(root);

        root.Controls.Add(Group("Measurements", _measurements), 0, 0);
        root.SetRowSpan(_measurements.Parent!, 2);
        root.Controls.Add(Group("Formatted Output", _formatted), 1, 0);
        root.SetRowSpan(_formatted.Parent!, 2);
        root.Controls.Add(BuildScrewsPanel(), 2, 0);
        root.Controls.Add(BuildTrimPanel(), 3, 0);
        root.Controls.Add(BuildMiscPanel(), 4, 0);
        root.SetRowSpan(root.GetControlFromPosition(4, 0)!, 2);
        root.Controls.Add(BuildInfoPanel(), 0, 2);
        root.SetColumnSpan(root.GetControlFromPosition(0, 2)!, 2);
        root.Controls.Add(BuildCobPanel(), 2, 2);
        root.SetColumnSpan(root.GetControlFromPosition(2, 2)!, 2);
        root.Controls.Add(BuildPricesPanel(), 4, 2);
        return page;
    }

    private Control BuildScrewsPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8 };
        for (int i = 0; i < 8; i++) panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        int row = 0;
        foreach (var button in _screwButtons) panel.Controls.Add(button, 0, row++);
        panel.Controls.Add(_useSuggested, 0, row++);
        panel.Controls.Add(Row("Total LF", _totalLf), 0, row++);
        panel.Controls.Add(Row("Screw bags", _screwBags, _suggestedScrews), 0, row++);
        panel.Controls.Add(Row("Lap bags", _lapScrewBags, _suggestedLapScrews), 0, row);
        return Group("Screws", panel);
    }

    private Control BuildTrimPanel()
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3 };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        grid.Controls.Add(new Label(), 0, 0);
        grid.Controls.Add(new Label { Text = "Qty", AutoSize = true }, 1, 0);
        grid.Controls.Add(new Label { Text = "Extra in.", AutoSize = true }, 2, 0);

        string[] names = ["Ridges", "Deluxe Corners", "Eaves", "Gables", "Valleys", "Sidewalls", "Endwalls", "Transitions", "J-Trim"];
        for (int i = 0; i < names.Length; i++)
        {
            _trimCounts[names[i]] = CountBox();
            _trimExtras[names[i]] = CountBox();
            grid.Controls.Add(new Label { Text = names[i], AutoSize = true }, 0, i + 1);
            grid.Controls.Add(_trimCounts[names[i]], 1, i + 1);
            grid.Controls.Add(_trimExtras[names[i]], 2, i + 1);
        }
        grid.Controls.Add(_customTrimAdded, 0, names.Length + 1);
        grid.SetColumnSpan(_customTrimAdded, 3);
        return Group("Standard Trim", grid);
    }

    private Control BuildPricesPanel()
    {
        var prices = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 5, Padding = new Padding(4) };
        prices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        prices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        prices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        for (int i = 0; i < 5; i++) prices.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
        prices.Controls.Add(new Label(), 0, 0);
        prices.Controls.Add(new Label { Text = "Subtotal", AutoSize = true, Font = BoldFont(10) }, 1, 0);
        prices.Controls.Add(new Label { Text = "Grand Total", AutoSize = true, Font = BoldFont(10) }, 2, 0);
        int row = 1;
        foreach (MetalOption metal in Enum.GetValues<MetalOption>())
        {
            _subtotalBoxes[metal] = PriceBox();
            _grandTotalBoxes[metal] = PriceBox();
            prices.Controls.Add(new Label { Text = QuoteCalculator.MetalLabel(metal), AutoSize = true, Anchor = AnchorStyles.Left, Font = BoldFont(10) }, 0, row);
            prices.Controls.Add(_subtotalBoxes[metal], 1, row);
            prices.Controls.Add(_grandTotalBoxes[metal], 2, row);
            row++;
        }
        return Group("Prices", prices);
    }

    private Control BuildCobPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(4) };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        panel.Controls.Add(_militaryDiscount, 0, 0);

        var cobPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        var copy = new Button { Text = "Copy COB", AutoSize = true };
        copy.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_centerOfBalanceClipboardText)) Clipboard.SetText(_centerOfBalanceClipboardText);
        };
        cobPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        cobPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        cobPanel.Controls.Add(copy, 0, 0);
        cobPanel.Controls.Add(_centerOfBalance, 1, 0);
        panel.Controls.Add(cobPanel, 0, 1);
        panel.Controls.Add(_weight, 0, 2);
        return Group("Balance", panel);
    }

    private Control BuildInfoPanel()
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 3 };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        grid.Controls.Add(new Label { Text = "Customer", AutoSize = true }, 0, 0);
        grid.Controls.Add(_customer, 1, 0);
        grid.Controls.Add(new Label { Text = "Phone", AutoSize = true }, 2, 0);
        grid.Controls.Add(_phone, 3, 0);
        grid.Controls.Add(new Label { Text = "Color", AutoSize = true }, 0, 1);
        grid.Controls.Add(_color, 1, 1);
        grid.Controls.Add(new Label { Text = "Notes", AutoSize = true }, 0, 2);
        _notes.MinimumSize = new Size(0, 70);
        grid.Controls.Add(_notes, 1, 2);
        grid.SetColumnSpan(_notes, 3);
        return Group("Quote Info", grid);
    }

    private Control BuildMiscPanel()
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));

        var miscGrid = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
        miscGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        miscGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        string[] misc =
        [
            "Outside Closures (4)", "Inside Closures (4)", "Butyl Tape (45')", "Caulk",
            "Vented Closures (1)", "Universal Closures (20')", "Red Snips", "Green Snips",
            "Blue Snips", "Turbo Shear"
        ];
        for (int i = 0; i < misc.Length; i++)
        {
            miscGrid.Controls.Add(new Label { Text = misc[i], AutoSize = true }, 0, i);
            miscGrid.Controls.Add(_miscCounts[i], 1, i);
        }
        grid.Controls.Add(Group("Extras", miscGrid), 0, 0);
        grid.Controls.Add(BuildBootSelector(), 1, 0);
        return grid;
    }

    private Control BuildBootSelector()
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.Controls.Add(new Label { Text = "Boot Type", AutoSize = true, Font = BoldFont(9) }, 0, 0);
        grid.Controls.Add(new Label { Text = "Qty", AutoSize = true, Font = BoldFont(9) }, 1, 0);
        for (int i = 0; i < QuoteCalculator.BootCatalog.Length; i++)
        {
            grid.Controls.Add(new Label { Text = QuoteCalculator.BootCatalog[i].Name, AutoSize = true }, 0, i + 1);
            grid.Controls.Add(_bootCounts[i], 1, i + 1);
        }
        return Group("Boots", grid);
    }

    private TabPage BuildCustomTrimTab()
    {
        var page = new TabPage("Custom Trim");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 2 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Fill };
        var snap = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
        snap.Items.AddRange(new object[] { "1/16\"", "1/8\"", "1/4\"", "1/2\"", "1\"", "1 ft" });
        snap.SelectedIndex = 4;
        snap.SelectedIndexChanged += (_, _) => _customTrim.SnapInches = snap.SelectedIndex switch { 0 => 1f / 16f, 1 => 1f / 8f, 2 => 0.25f, 3 => 0.5f, 4 => 1f, _ => 12f };
        var rotationSnap = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 85 };
        rotationSnap.Items.AddRange(new object[] { "Off", "1 deg", "5 deg", "10 deg", "15 deg", "22.5 deg", "45 deg", "90 deg" });
        rotationSnap.SelectedIndex = 4;
        rotationSnap.SelectedIndexChanged += (_, _) => _customTrim.RotationSnapDegrees = rotationSnap.SelectedIndex switch
        {
            0 => 0,
            1 => 1,
            2 => 5,
            3 => 10,
            4 => 15,
            5 => 22.5f,
            6 => 45,
            _ => 90
        };
        toolbar.Items.Add(new ToolStripLabel("Snap"));
        toolbar.Items.Add(new ToolStripControlHost(snap));
        toolbar.Items.Add(new ToolStripLabel("Rotation snap"));
        toolbar.Items.Add(new ToolStripControlHost(rotationSnap));
        toolbar.Items.Add(ToolbarButton(BuildRecenterIcon(), "Re-center", (_, _) => _customTrim.Recenter()));
        toolbar.Items.Add(ToolbarButton(BuildClearIcon(), "Clear custom trim", (_, _) => ClearCustomTrim()));
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(new ToolStripControlHost(_customTrimPiece));
        root.Controls.Add(toolbar, 0, 0);
        root.SetColumnSpan(toolbar, 2);
        root.Controls.Add(_customTrim, 0, 1);
        root.Controls.Add(BuildCustomTrimSidebar(), 1, 1);
        root.Controls.Add(_customTrimSummary, 0, 2);
        root.SetColumnSpan(_customTrimSummary, 2);
        page.Controls.Add(root);
        return page;
    }

    private Control BuildCustomTrimSidebar()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(6) };
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 36));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 36));

        _customTrimPiecesList.SelectedIndexChanged += (_, _) =>
        {
            if (_syncingCustomTrimSidebar || _customTrimPiecesList.SelectedIndex < 0) return;
            _customTrim.SelectPiece(_customTrimPiecesList.SelectedIndex);
        };
        _customTrimSegmentsList.SelectedIndexChanged += (_, _) =>
        {
            if (_syncingCustomTrimSidebar || _customTrimSegmentsList.SelectedIndex < 0) return;
            _customTrim.SelectSegment(_customTrimSegmentsList.SelectedIndex);
            LoadSelectedFace();
        };
        _customTrimAnglesList.SelectedIndexChanged += (_, _) =>
        {
            if (_syncingCustomTrimSidebar || _customTrimAnglesList.SelectedIndex < 0) return;
            _customTrim.SelectAngleVertex(_customTrimAnglesList.SelectedIndex + 1);
            LoadSelectedAngle();
        };
        _customOriginSelector.SelectedIndexChanged += (_, _) =>
        {
            if (!_syncingCustomTrimSidebar && _customOriginSelector.SelectedIndex >= 0) _customTrim.SelectedOriginIndex = _customOriginSelector.SelectedIndex;
        };
        _customSegmentLength.ValueChanged += (_, _) => LiveUpdateSelectedFace();
        _customInteriorAngle.TextChanged += (_, _) => LiveUpdateSelectedAngle();
        _showCustomCoordinates.CheckedChanged += (_, _) => _customCoordinatesPanel.Visible = _showCustomCoordinates.Checked;
        _customVertexX.ValueChanged += (_, _) => UpdateSelectedVertexFromFields();
        _customVertexY.ValueChanged += (_, _) => UpdateSelectedVertexFromFields();

        var pieceGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        pieceGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        pieceGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _customTrimPiecesList.Dock = DockStyle.Fill;
        pieceGrid.Controls.Add(_customTrimPiecesList, 0, 0);
        var originPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        originPanel.Controls.Add(new Label { Text = "Qty", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft });
        originPanel.Controls.Add(_customTrimQuantity);
        originPanel.Controls.Add(new Label { Text = "Origin", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft });
        var setOrigin = new Button { Text = "Set origin", AutoSize = true };
        setOrigin.Click += (_, _) =>
        {
            if (_customTrim.SelectedPieceIndex < 0)
            {
                SetStatus("Select a custom trim piece before setting origin.", ErrorColor, Color.White);
                return;
            }
            _customTrim.BeginOriginPick();
            SetStatus("Click a vertex to set the selected piece origin.", CalculatingColor, Color.White);
        };
        originPanel.Controls.Add(_customOriginSelector);
        originPanel.Controls.Add(setOrigin);
        pieceGrid.Controls.Add(originPanel, 0, 1);
        panel.Controls.Add(Group("Pieces", pieceGrid), 0, 0);

        var segmentGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4 };
        segmentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        segmentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        segmentGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        segmentGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        segmentGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        segmentGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _customTrimSegmentsList.Dock = DockStyle.Fill;
        segmentGrid.Controls.Add(_customTrimSegmentsList, 0, 0);
        segmentGrid.SetColumnSpan(_customTrimSegmentsList, 2);
        var faceActions = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        var addFace = new Button { Text = "Add face", AutoSize = true };
        addFace.Click += (_, _) => AddCustomFace();
        var colorSide = new Button { Text = "Color side", AutoSize = true, Image = BuildColorSideIcon(), TextImageRelation = TextImageRelation.ImageBeforeText };
        colorSide.Click += (_, _) => _customTrim.ToggleColorSide();
        faceActions.Controls.Add(addFace);
        faceActions.Controls.Add(colorSide);
        segmentGrid.Controls.Add(faceActions, 0, 1);
        segmentGrid.SetColumnSpan(faceActions, 2);
        segmentGrid.Controls.Add(new Label { Text = "Length", AutoSize = true }, 0, 2);
        segmentGrid.Controls.Add(_customSegmentLength, 1, 2);
        panel.Controls.Add(Group("Faces", segmentGrid), 0, 1);

        var angleGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4 };
        angleGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        angleGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        angleGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        angleGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        angleGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        angleGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _customTrimAnglesList.Dock = DockStyle.Fill;
        angleGrid.Controls.Add(_customTrimAnglesList, 0, 0);
        angleGrid.SetColumnSpan(_customTrimAnglesList, 2);
        angleGrid.Controls.Add(new Label { Text = "Selected angle", AutoSize = true }, 0, 1);
        angleGrid.Controls.Add(_customInteriorAngle, 1, 1);
        var note = new Label
        {
            Text = "Enter degrees, or pitch like 3/12.",
            AutoSize = false,
            Dock = DockStyle.Fill,
            Height = 40
        };
        angleGrid.Controls.Add(note, 0, 2);
        angleGrid.SetColumnSpan(note, 2);
        panel.Controls.Add(Group("Angles", angleGrid), 0, 2);
        return panel;
    }

    private void SyncCustomTrimSidebar()
    {
        _syncingCustomTrimSidebar = true;
        try
        {
            int selectedPiece = _customTrim.SelectedPieceIndex;
            int selectedSegment = _customTrim.SelectedSegmentIndex;
            int selectedAngle = _customTrim.SelectedAngleVertexIndex > 0 ? _customTrim.SelectedAngleVertexIndex - 1 : -1;
            _customTrimPiecesList.Items.Clear();
            for (int i = 0; i < _customTrim.PieceCount; i++)
            {
                var piece = _customTrim.Pieces[i];
                _customTrimPiecesList.Items.Add($"Piece {i + 1}: {Num(QuoteCalculator.CustomTrimLength(piece))}\"  Qty {piece.Quantity}");
            }
            if (selectedPiece >= 0 && selectedPiece < _customTrimPiecesList.Items.Count) _customTrimPiecesList.SelectedIndex = selectedPiece;

            _customTrimVerticesList.Items.Clear();
            _customTrimSegmentsList.Items.Clear();
            _customTrimAnglesList.Items.Clear();
            _customOriginSelector.Items.Clear();
            var vertices = _customTrim.SelectedVertices;
            for (int i = 0; i < vertices.Count; i++)
            {
                string origin = i == _customTrim.SelectedOriginIndex ? " origin" : "";
                _customTrimVerticesList.Items.Add($"{i + 1}: X {Num(vertices[i].X)}\"  Y {Num(vertices[i].Y)}\"{origin}");
                _customOriginSelector.Items.Add($"Vertex {i + 1}");
            }
            for (int i = 1; i < vertices.Count; i++)
            {
                double length = Math.Sqrt(Math.Pow(vertices[i].X - vertices[i - 1].X, 2) + Math.Pow(vertices[i].Y - vertices[i - 1].Y, 2));
                _customTrimSegmentsList.Items.Add($"{Num(length)}\"");
            }
            for (int i = 1; i < vertices.Count - 1; i++)
            {
                double angle = BenderAngle(vertices[i - 1], vertices[i], vertices[i + 1]);
                _customTrimAnglesList.Items.Add($"V{i + 1}: {Num(angle)} deg");
            }
            if (_customTrim.SelectedOriginIndex >= 0 && _customTrim.SelectedOriginIndex < _customOriginSelector.Items.Count)
            {
                _customOriginSelector.SelectedIndex = _customTrim.SelectedOriginIndex;
            }
            if (selectedSegment >= 0 && selectedSegment < _customTrimSegmentsList.Items.Count) _customTrimSegmentsList.SelectedIndex = selectedSegment;
            if (selectedAngle >= 0 && selectedAngle < _customTrimAnglesList.Items.Count) _customTrimAnglesList.SelectedIndex = selectedAngle;
        }
        finally
        {
            _syncingCustomTrimSidebar = false;
        }
    }

    private void SetSidebarVertexValues(PointF point)
    {
        _syncingCustomTrimSidebar = true;
        try
        {
            _customVertexX.Value = ClampDecimal((decimal)point.X, _customVertexX);
            _customVertexY.Value = ClampDecimal((decimal)point.Y, _customVertexY);
        }
        finally
        {
            _syncingCustomTrimSidebar = false;
        }
    }

    private PointF SidebarPoint() => new((float)_customVertexX.Value, (float)_customVertexY.Value);

    private void LoadSelectedFace()
    {
        if (_syncingCustomTrimSidebar || _customTrimSegmentsList.SelectedIndex < 0) return;
        var vertices = _customTrim.SelectedVertices;
        int endIndex = _customTrimSegmentsList.SelectedIndex + 1;
        if (endIndex >= vertices.Count) return;
        PointF from = vertices[endIndex - 1];
        PointF to = vertices[endIndex];
        _syncingCustomTrimSidebar = true;
        try
        {
            _customSegmentLength.Value = ClampDecimal((decimal)SegmentLength(from, to), _customSegmentLength);
        }
        finally
        {
            _syncingCustomTrimSidebar = false;
        }
    }

    private void LoadSelectedAngle()
    {
        if (_syncingCustomTrimSidebar || _customTrimAnglesList.SelectedIndex < 0) return;
        var vertices = _customTrim.SelectedVertices;
        int vertexIndex = _customTrimAnglesList.SelectedIndex + 1;
        if (vertexIndex <= 0 || vertexIndex >= vertices.Count - 1) return;
        _syncingCustomTrimSidebar = true;
        try
        {
            _customInteriorAngle.Text = Num(BenderAngle(vertices[vertexIndex - 1], vertices[vertexIndex], vertices[vertexIndex + 1]));
        }
        finally
        {
            _syncingCustomTrimSidebar = false;
        }
    }

    private void AddCustomFace()
    {
        _customTrim.AddBendSegment((double)_customSegmentLength.Value, 0);
        RefreshCustomTrimUi();
        if (_customTrimSegmentsList.Items.Count > 0)
        {
            _customTrimSegmentsList.SelectedIndex = _customTrimSegmentsList.Items.Count - 1;
            LoadSelectedFace();
        }
    }

    private void LiveUpdateSelectedFace()
    {
        if (_syncingCustomTrimSidebar || _customTrimSegmentsList.SelectedIndex < 0) return;
        var vertices = _customTrim.SelectedVertices;
        int segment = _customTrimSegmentsList.SelectedIndex;
        if (segment < 0 || segment + 1 >= vertices.Count) return;
        double angle = SegmentAngle(vertices[segment], vertices[segment + 1]);
        _customTrim.UpdateSegment(_customTrimSegmentsList.SelectedIndex, (double)_customSegmentLength.Value, angle);
    }

    private void LiveUpdateSelectedAngle()
    {
        if (_syncingCustomTrimSidebar || _customTrimAnglesList.SelectedIndex < 0) return;
        if (!TryParseAngleOrPitch(_customInteriorAngle.Text, out double benderAngle)) return;
        double normalized = NormalizeDegrees(benderAngle);
        if (!_customInteriorAngle.Text.Contains('/') && Math.Abs(normalized - benderAngle) > 0.001)
        {
            _syncingCustomTrimSidebar = true;
            _customInteriorAngle.Text = Num(normalized);
            _customInteriorAngle.SelectionStart = _customInteriorAngle.TextLength;
            _syncingCustomTrimSidebar = false;
        }
        int vertexIndex = _customTrimAnglesList.SelectedIndex + 1;
        _customTrim.UpdateInteriorAngle(vertexIndex, normalized - 180);
    }

    private void UpdateSelectedVertexFromFields()
    {
        if (_syncingCustomTrimSidebar || !_customCoordinatesPanel.Visible || _customTrimVerticesList.SelectedIndex < 0) return;
        _customTrim.UpdateVertex(_customTrimVerticesList.SelectedIndex, SidebarPoint());
    }

    private static bool TryParseAngleOrPitch(string text, out double angle)
    {
        angle = 0;
        if (text.Contains('/'))
        {
            if (!TryParsePitch(text, out double rise, out double run)) return false;
            angle = Math.Atan2(rise, run) * 180.0 / Math.PI;
            return true;
        }
        return double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out angle);
    }

    private static bool TryParsePitch(string text, out double rise, out double run)
    {
        rise = 0;
        run = 0;
        string[] parts = text.Trim().Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return false;
        return double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out rise)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out run)
            && run != 0;
    }

    private static double SegmentLength(PointF from, PointF to) => Math.Sqrt(Math.Pow(to.X - from.X, 2) + Math.Pow(to.Y - from.Y, 2));

    private static double SegmentAngle(PointF from, PointF to) => Math.Atan2(to.Y - from.Y, to.X - from.X) * 180.0 / Math.PI;

    private static double BenderAngle(PointF previous, PointF vertex, PointF next)
    {
        double sweep = SignedInteriorSweep(previous, vertex, next);
        return Math.Abs(Math.Abs(sweep) - 180) < 0.001 ? 0 : NormalizeDegrees(180 + sweep);
    }

    private static double SignedInteriorSweep(PointF previous, PointF vertex, PointF next)
    {
        double previousAngle = Math.Atan2(previous.Y - vertex.Y, previous.X - vertex.X) * 180.0 / Math.PI;
        double nextAngle = Math.Atan2(next.Y - vertex.Y, next.X - vertex.X) * 180.0 / Math.PI;
        double sweep = nextAngle - previousAngle;
        while (sweep > 180) sweep -= 360;
        while (sweep < -180) sweep += 360;
        return sweep;
    }

    private static double NormalizeDegrees(double angle)
    {
        while (angle > 180) angle -= 360;
        while (angle < -180) angle += 360;
        return angle;
    }

    private void RefreshCustomTrimUi()
    {
        _customTrimSummary.Text = _customTrim.Summary();
        _customTrimPiece.Text = _customTrim.SelectedPieceText;
        bool oldSuppress = _suppress;
        _suppress = true;
        try
        {
            _customTrimQuantity.Value = Math.Max(_customTrimQuantity.Minimum, Math.Min(_customTrimQuantity.Maximum, _customTrim.SelectedQuantity));
        }
        finally
        {
            _suppress = oldSuppress;
        }
        UpdateCustomTrimAddedIndicator();
        SyncCustomTrimSidebar();
    }

    private void Recalculate()
    {
        if (_suppress) return;
        SetStatus("Calculating...", CalculatingColor, Color.White);
        try
        {
            var input = CurrentInput();
            _lastQuote = QuoteCalculator.Calculate(input, _settings.Prices);
            _formatted.Text = QuoteCalculator.SortedOutputText(_lastQuote.GroupedPanels);
            _totalLf.Text = Num(_lastQuote.TotalLengthInFeet);
            _suggestedScrews.Text = $"Suggested: {Num(_lastQuote.SuggestedScrewBags)}";
            _suggestedLapScrews.Text = $"Suggested: {Num(_lastQuote.SuggestedLapScrewBags)}";
            if (_useSuggested.Checked)
            {
                _suppress = true;
                _screwBags.Text = Num(_lastQuote.SuggestedScrewBags);
                _lapScrewBags.Text = Num(_lastQuote.SuggestedLapScrewBags);
                _suppress = false;
            }
            foreach (var result in _lastQuote.Quotes.Values)
            {
                _subtotalBoxes[result.Metal].Text = Money(result.Subtotal);
                _grandTotalBoxes[result.Metal].Text = Money(result.GrandTotal);
            }
            if (_lastQuote.GroupedPanels.Count == 0)
            {
                _centerOfBalanceClipboardText = "";
                _centerOfBalance.Text = "";
                _weight.Text = "";
            }
            else
            {
                _centerOfBalanceClipboardText = $"{Num(_lastQuote.CenterOfBalance)}\"";
                _centerOfBalance.Text = $"Center of Balance: {_centerOfBalanceClipboardText}";
                _weight.Text = $"Weight: 29ga {Num(_lastQuote.Quotes[MetalOption.Galv29].TotalWeight)} lb | 26ga {Num(_lastQuote.Quotes[MetalOption.Galv26].TotalWeight)} lb";
            }
            SetStatus("Quote recalculated.", SuccessColor, Color.White);
            HighlightSelectedScrew();
        }
        catch (Exception ex)
        {
            _lastQuote = null;
            ClearQuoteOutputs();
            SetStatus(ex.Message, ErrorColor, Color.White);
            LogDebug("Calculation error: " + ex.Message);
        }
    }

    private QuoteInput CurrentInput() => new(
        _measurements.Text,
        SelectedScrew(),
        _useSuggested.Checked,
        _screwBags.Text,
        _lapScrewBags.Text,
        _militaryDiscount.Checked,
        CurrentTrim(),
        CurrentMisc(),
        _customTrim.State);

    private TrimSelection CurrentTrim() => new(
        Count("Ridges"), Count("Gables"), Count("Eaves"), Count("Endwalls"), Count("Sidewalls"), Count("Valleys"), Count("Transitions"), Count("J-Trim"),
        Count("Deluxe Corners"),
        Extra("Ridges"), Extra("Gables"), Extra("Eaves"), Extra("Endwalls"), Extra("Sidewalls"), Extra("Valleys"), Extra("Transitions"), Extra("J-Trim"),
        Extra("Deluxe Corners"));

    private MiscSelection CurrentMisc() => new((int)_miscCounts[0].Value, (int)_miscCounts[1].Value, (int)_miscCounts[2].Value,
        (int)_miscCounts[3].Value, (int)_miscCounts[4].Value, (int)_miscCounts[5].Value, (int)_miscCounts[6].Value,
        (int)_miscCounts[7].Value, (int)_miscCounts[8].Value, (int)_miscCounts[9].Value, _bootCounts.Select(n => (int)n.Value).ToArray());

    private void ApplyInput(QuoteInput input)
    {
        _suppress = true;
        try
        {
            _measurements.Text = input.MeasurementsText;
            _screwButtonMap[input.Screw].Checked = true;
            _useSuggested.Checked = input.UseSuggestedScrews;
            _screwBags.Text = input.ScrewBagsText;
            _lapScrewBags.Text = input.LapScrewBagsText;
            _militaryDiscount.Checked = input.MilitaryDiscount;
            SetTrim(input.Trim);
            SetMisc(input.Misc);
            _customTrim.LoadState(input.CustomTrim);
        }
        finally { _suppress = false; }
        MarkDirty();
    }

    private void SetTrim(TrimSelection trim)
    {
        SetTrimValue("Ridges", trim.Ridges, trim.RidgesExtraInches);
        SetTrimValue("Gables", trim.Gables, trim.GablesExtraInches);
        SetTrimValue("Eaves", trim.Eaves, trim.EavesExtraInches);
        SetTrimValue("Endwalls", trim.Endwalls, trim.EndwallsExtraInches);
        SetTrimValue("Sidewalls", trim.Sidewalls, trim.SidewallsExtraInches);
        SetTrimValue("Valleys", trim.Valleys, trim.ValleysExtraInches);
        SetTrimValue("Transitions", trim.Transitions, trim.TransitionsExtraInches);
        SetTrimValue("J-Trim", trim.JTrim, trim.JTrimExtraInches);
        SetTrimValue("Deluxe Corners", trim.DeluxeCorners, trim.DeluxeCornersExtraInches);
    }

    private void SetMisc(MiscSelection misc)
    {
        int[] values =
        [
            misc.OutsideClosures, misc.InsideClosures, misc.ButylTape, misc.Caulk, misc.VentedClosures,
            misc.UniversalClosures, misc.RedSnips, misc.GreenSnips, misc.BlueSnips, misc.TurboShear
        ];
        for (int i = 0; i < values.Length; i++) _miscCounts[i].Value = values[i];
        for (int i = 0; i < _bootCounts.Length; i++) _bootCounts[i].Value = misc.BootCount(i);
    }

    private void NewQuote()
    {
        if (!ConfirmDiscardWork()) return;
        _currentSavePath = "";
        ApplyInput(new QuoteInput("", ScrewOption.OneInch, false, "0", "0", false, TrimSelection.Empty, MiscSelection.Empty, CustomTrimState.Empty));
        _customer.Clear(); _phone.Clear(); _color.Clear(); _notes.Clear();
        Recalculate();
        ResetSavedSnapshot();
        PlayMascotExplosion();
    }

    private void ClearCustomTrim()
    {
        if (_customTrim.PieceCount == 0) return;
        DialogResult result = MessageBox.Show(this, "Clear the custom trim graph?", "Clear Custom Trim", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result != DialogResult.Yes) return;
        _customTrim.ClearPieces();
        PlayMascotExplosion();
    }

    private bool SaveAs()
    {
        if (!EnsureQuote()) return false;
        using var dialog = new SaveFileDialog
        {
            Filter = "Cow Pilot quote (*.cowpilot)|*.cowpilot|Text files (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt = "cowpilot",
            AddExtension = true,
            FileName = DefaultQuoteFileName()
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return false;
        SaveTo(dialog.FileName);
        return true;
    }

    private bool SaveQuote()
    {
        if (!EnsureQuote()) return false;
        if (string.IsNullOrWhiteSpace(_currentSavePath)) return SaveAs();
        SaveTo(_currentSavePath);
        return true;
    }

    private void SaveTo(string path)
    {
        var doc = new QuoteDocument(AppVersion.SaveFormatVersion, AppVersion.Version, DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss tt", CultureInfo.InvariantCulture),
            _customer.Text, _phone.Text, _color.Text, _notes.Text, CurrentInput());
        File.WriteAllText(path, QuoteSaveLoad.CreateEstimateText(doc, _lastQuote!, _settings.Prices));
        _currentSavePath = path;
        ResetSavedSnapshot();
        SetStatus($"Saved: {path}", SuccessColor, Color.White);
        LogDebug("Saved quote: " + path);
    }

    private void LoadQuote()
    {
        using var dialog = new OpenFileDialog { Filter = "Cow Pilot quote (*.cowpilot)|*.cowpilot|Text files (*.txt)|*.txt|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var doc = QuoteSaveLoad.Load(File.ReadAllText(dialog.FileName));
            _customer.Text = doc.CustomerName;
            _phone.Text = doc.Phone;
            _color.Text = doc.Color;
            _notes.Text = doc.Notes;
            ApplyInput(doc.Input);
            _currentSavePath = dialog.FileName;
            Recalculate();
            ResetSavedSnapshot();
            LogDebug("Loaded quote: " + dialog.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Load Quote", MessageBoxButtons.OK, MessageBoxIcon.Error);
            LogDebug("Load error: " + ex.Message);
        }
    }

    private bool EnsureQuote()
    {
        Recalculate();
        if (_lastQuote != null) return true;
        MessageBox.Show(this, _status.Text, "Cannot Save Quote", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }

    private string DefaultQuoteFileName()
    {
        string name = _customer.Text.Trim();
        if (name.Length == 0) name = $"cow-pilot-estimate-{DateTime.Now:yyyyMMdd-hhmmss-tt}";
        foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '-');
        name = name.Trim();
        if (name.Length == 0) name = "cow-pilot-estimate";
        if (name.Length > 80) name = name[..80].Trim();
        return Path.ChangeExtension(name, QuoteFileExtension);
    }

    private void ClearQuoteOutputs()
    {
        _lastQuote = null;
        _formatted.Clear();
        _totalLf.Clear();
        _suggestedScrews.Text = "Suggested: 0";
        _suggestedLapScrews.Text = "Suggested: 0";
        foreach (var box in _subtotalBoxes.Values.Concat(_grandTotalBoxes.Values)) box.Clear();
        _centerOfBalanceClipboardText = "";
        _centerOfBalance.Text = "";
        _weight.Text = "";
        SetStatus("Ready", CalculatingColor, Color.White);
    }

    private bool ConfirmDiscardWork(bool forcePrompt = false)
    {
        if (!forcePrompt && !_isDirty) return true;
        using var dialog = new UnsavedChangesDialog();
        dialog.ShowDialog(this);
        if (dialog.Choice == UnsavedChoice.Cancel) return false;
        if (dialog.Choice == UnsavedChoice.Yes) return SaveQuote();
        return true;
    }

    private void ApplySuggestedScrews()
    {
        if (_useSuggested.Checked && _lastQuote != null)
        {
            _screwBags.Text = Num(_lastQuote.SuggestedScrewBags);
            _lapScrewBags.Text = Num(_lastQuote.SuggestedLapScrewBags);
        }
        else if (!_useSuggested.Checked)
        {
            _screwBags.Text = "0";
            _lapScrewBags.Text = "0";
        }
        MarkDirty();
    }

    private void WireAutoCalc()
    {
        foreach (var textBox in new[] { _measurements, _screwBags, _lapScrewBags, _customer, _phone, _color, _notes })
        {
            textBox.TextChanged += (_, _) => MarkDirty();
        }
        _militaryDiscount.CheckedChanged += (_, _) => MarkDirty();
        foreach (var radioButton in _screwButtons)
        {
            radioButton.CheckedChanged += (_, _) => { HighlightSelectedScrew(); MarkDirty(); };
        }
        foreach (var numeric in _trimCounts.Values.Concat(_trimExtras.Values).Concat(_miscCounts).Concat(_bootCounts))
        {
            numeric.ValueChanged += (_, _) => MarkDirty();
        }
    }

    private void MarkDirty()
    {
        if (_suppress) return;
        UpdateTrimExtraAvailability();
        _isDirty = CurrentSnapshot() != _savedSnapshot;
        UpdateChangedHighlights();
        SetStatus("Calculating...", CalculatingColor, Color.White);
        Recalculate();
    }

    private void ResetSavedSnapshot()
    {
        UpdateTrimExtraAvailability();
        _savedSnapshot = CurrentSnapshot();
        _isDirty = false;
        UpdateChangedHighlights();
    }

    private string CurrentSnapshot()
    {
        var doc = new QuoteDocument(AppVersion.SaveFormatVersion, AppVersion.Version, "",
            _customer.Text, _phone.Text, _color.Text, _notes.Text, CurrentInput());
        return JsonSerializer.Serialize(doc);
    }

    private void UpdateChangedHighlights()
    {
        ResetInputHighlight(_measurements);
        ResetInputHighlight(_formatted);
        SetChanged(_screwBags, !_useSuggested.Checked && _screwBags.Text.Trim() != "0" && _screwBags.Text.Trim().Length > 0);
        SetChanged(_lapScrewBags, !_useSuggested.Checked && _lapScrewBags.Text.Trim() != "0" && _lapScrewBags.Text.Trim().Length > 0);
        SetChanged(_customer, _customer.TextLength > 0);
        SetChanged(_phone, _phone.TextLength > 0);
        SetChanged(_color, _color.TextLength > 0);
        SetChanged(_notes, _notes.TextLength > 0);
        SetChanged(_useSuggested, _useSuggested.Checked);
        SetChanged(_militaryDiscount, _militaryDiscount.Checked);
        foreach (var input in _trimCounts.Values) SetChanged(input, input.Value != 0);
        foreach (var input in _trimExtras.Values) SetChanged(input, input.Value != 0);
        foreach (var input in _miscCounts) SetChanged(input, input.Value != 0);
        foreach (var input in _bootCounts) SetChanged(input, input.Value != 0);
        ResetInputHighlight(_customTrimQuantity);
        UpdateCustomTrimAddedIndicator();
        HighlightSelectedScrew();
    }

    private static void SetChanged(Control control, bool changed)
    {
        if (control is TextBox or NumericUpDown)
        {
            control.BackColor = changed ? ChangedBoxColor : SystemColors.Window;
            control.ForeColor = SystemColors.ControlText;
            return;
        }

        control.BackColor = SystemColors.Control;
        control.ForeColor = changed ? ChangedTextColor : SystemColors.ControlText;
        if (control is CheckBox checkBox) checkBox.UseVisualStyleBackColor = true;
    }

    private static void ResetInputHighlight(Control control)
    {
        control.BackColor = control is TextBox or NumericUpDown ? SystemColors.Window : SystemColors.Control;
        control.ForeColor = control is TextBox ? SystemColors.WindowText : SystemColors.ControlText;
    }

    private void UpdateCustomTrimAddedIndicator()
    {
        _customTrimAdded.Visible = _customTrim.State.Pieces.Count > 0;
    }

    private void UpdateTrimExtraAvailability()
    {
        bool oldSuppress = _suppress;
        _suppress = true;
        try
        {
            foreach (var pair in _trimExtras)
            {
                bool enabled = _trimCounts[pair.Key].Value > 0;
                pair.Value.Enabled = enabled;
                if (!enabled && pair.Value.Value != 0) pair.Value.Value = 0;
            }
        }
        finally { _suppress = oldSuppress; }
    }

    private void SetStatus(string text, Color backColor, Color foreColor)
    {
        if (_status.Text == text && _status.BackColor == backColor && _status.ForeColor == foreColor) return;
        _status.SuspendLayout();
        _status.Text = text;
        _status.BackColor = backColor;
        _status.ForeColor = foreColor;
        _status.ResumeLayout();
    }

    private void ShowConsole()
    {
        if (_console.IsDisposed) return;
        _console.Show(this);
        _console.BringToFront();
    }

    private void ShowHelpGuide()
    {
        using var guide = new HelpGuideForm();
        guide.ShowDialog(this);
    }

    private void LogDebug(string message) => _console.Log(message);

    private void PrintCalculationTrace()
    {
        try
        {
            Recalculate();
            foreach (string line in QuoteCalculator.CalculationTrace(CurrentInput(), _settings.Prices)) LogDebug(line);
        }
        catch (Exception ex)
        {
            LogDebug("Calculation trace error: " + ex.Message);
        }
    }

    private void ShowSettings()
    {
        using var dialog = new SettingsForm(_settings);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _settings = dialog.Settings;
        ApplyRuntimeSettings();
        Recalculate();
        SetStatus("Settings saved.", SuccessColor, Color.White);
        LogDebug("Settings saved: " + SettingsStore.SettingsPath);
    }

    private void ApplyRuntimeSettings()
    {
        _settings.Normalize();
        _customTrim.ApplySettings(_settings);
        _customTrimSummary.Text = _customTrim.Summary();
        SyncCustomTrimSidebar();
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        UpdateCheckResult? update = await UpdateChecker.CheckLatestAsync();
        if (update is not { IsNewer: true }) return;
        string message = $"Cow Pilot {update.LatestVersion} is available.\r\n\r\nCurrent version: {AppVersion.Version}.\r\nDownload the newest zip from the Cow Pilot GitHub repo.";
        if (!string.IsNullOrWhiteSpace(update.DownloadUrl)) message += $"\r\n\r\n{update.DownloadUrl}";
        MessageBox.Show(this, message, "Cow Pilot Update Available", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_isDirty)
        {
            using var dialog = new UnsavedChangesDialog();
            dialog.ShowDialog(this);
            if (dialog.Choice == UnsavedChoice.Cancel)
            {
                e.Cancel = true;
                return;
            }
            if (dialog.Choice == UnsavedChoice.Yes && !SaveQuote())
            {
                e.Cancel = true;
                return;
            }
        }
        base.OnFormClosing(e);
    }

    private ScrewOption SelectedScrew() => _screwButtonMap.First(kvp => kvp.Value.Checked).Key;
    private int Count(string name) => (int)_trimCounts[name].Value;
    private double Extra(string name) => (double)_trimExtras[name].Value;
    private void SetTrimValue(string name, int count, double extra) { _trimCounts[name].Value = count; _trimExtras[name].Value = (decimal)extra; }

    private RadioButton ScrewButton(ScrewOption option)
    {
        var button = new RadioButton { Text = QuoteCalculator.ScrewLabel(option), AutoSize = true };
        _screwButtonMap[option] = button;
        return button;
    }

    private void HighlightSelectedScrew()
    {
        foreach (var pair in _screwButtonMap) pair.Value.ForeColor = pair.Value.Checked ? ChangedTextColor : SystemColors.ControlText;
    }

    private static GroupBox Group(string title, Control child)
    {
        var group = new GroupBox { Text = title, Dock = DockStyle.Fill, Padding = new Padding(8) };
        child.Dock = DockStyle.Fill;
        group.Controls.Add(child);
        return group;
    }

    private static Control Row(string label, Control input, Control? extra = null)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        panel.Controls.Add(new Label { Text = label, Width = 90, TextAlign = ContentAlignment.MiddleRight });
        input.Width = 70;
        panel.Controls.Add(input);
        if (extra != null) panel.Controls.Add(extra);
        return panel;
    }

    private static TextBox ReadOnlyBox() => new() { ReadOnly = true, TextAlign = HorizontalAlignment.Right };
    private static TextBox PriceBox() => new()
    {
        ReadOnly = true,
        TextAlign = HorizontalAlignment.Right,
        Dock = DockStyle.Fill,
        Font = BoldFont(12),
        MinimumSize = new Size(125, 32)
    };

    private static NumericUpDown CountBox(decimal minimum = 0) => new() { Minimum = minimum, Maximum = 100000, Width = 58, TextAlign = HorizontalAlignment.Right };
    private static NumericUpDown InchBox(decimal value = 0) => new()
    {
        Minimum = -100000,
        Maximum = 100000,
        DecimalPlaces = 3,
        Increment = 0.125m,
        Value = value,
        Width = 78,
        TextAlign = HorizontalAlignment.Right
    };

    private static NumericUpDown PositiveInchBox(decimal value = 0) => new()
    {
        Minimum = 0.001m,
        Maximum = 100000,
        DecimalPlaces = 3,
        Increment = 0.125m,
        Value = Math.Max(0.001m, value),
        Width = 78,
        TextAlign = HorizontalAlignment.Right
    };

    private static decimal ClampDecimal(decimal value, NumericUpDown box) => Math.Min(box.Maximum, Math.Max(box.Minimum, value));
    private static Font BoldFont(float size) => new("Segoe UI", size, FontStyle.Bold);
    private static ToolStripButton ToolbarButton(Image image, string tooltip, EventHandler click)
    {
        var button = new ToolStripButton
        {
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            Image = image,
            Text = tooltip,
            ToolTipText = tooltip
        };
        button.Click += click;
        return button;
    }

    private static Image BuildRecenterIcon()
    {
        var image = new Bitmap(16, 16);
        using var g = Graphics.FromImage(image);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        using var pen = new Pen(Color.FromArgb(25, 118, 210), 2);
        g.DrawEllipse(pen, 3, 3, 10, 10);
        g.DrawLine(pen, 8, 1, 8, 5);
        g.DrawLine(pen, 8, 11, 8, 15);
        g.DrawLine(pen, 1, 8, 5, 8);
        g.DrawLine(pen, 11, 8, 15, 8);
        g.FillEllipse(Brushes.Black, 7, 7, 2, 2);
        return image;
    }

    private static Image BuildClearIcon()
    {
        var image = new Bitmap(16, 16);
        using var g = Graphics.FromImage(image);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        using var pen = new Pen(Color.FromArgb(211, 47, 47), 3) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        g.DrawLine(pen, 4, 4, 12, 12);
        g.DrawLine(pen, 12, 4, 4, 12);
        return image;
    }

    private static Image BuildColorSideIcon()
    {
        var image = new Bitmap(16, 16);
        using var g = Graphics.FromImage(image);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        using var line = new Pen(Color.Black, 2);
        g.DrawLine(line, 3, 12, 13, 4);
        using var arrow = new Pen(Color.DeepSkyBlue, 2) { EndCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor };
        g.DrawLine(arrow, 4, 3, 8, 8);
        return image;
    }

    private static Image BuildNewIcon()
    {
        var image = new Bitmap(16, 16);
        using var g = Graphics.FromImage(image);
        g.Clear(Color.Transparent);
        using var paper = new SolidBrush(Color.White);
        using var fold = new SolidBrush(Color.FromArgb(220, 230, 245));
        g.FillRectangle(paper, 4, 2, 9, 12);
        g.FillPolygon(fold, new[] { new Point(10, 2), new Point(13, 5), new Point(10, 5) });
        g.DrawRectangle(Pens.DimGray, 4, 2, 9, 12);
        using var plus = new Pen(Color.FromArgb(46, 125, 50), 2);
        g.DrawLine(plus, 2, 11, 8, 11);
        g.DrawLine(plus, 5, 8, 5, 14);
        return image;
    }

    private static Image BuildOpenIcon()
    {
        var image = new Bitmap(16, 16);
        using var g = Graphics.FromImage(image);
        g.Clear(Color.Transparent);
        using var back = new SolidBrush(Color.FromArgb(255, 213, 79));
        using var front = new SolidBrush(Color.FromArgb(255, 193, 7));
        g.FillRectangle(back, 2, 5, 12, 8);
        g.FillRectangle(front, 2, 7, 12, 6);
        g.FillRectangle(back, 3, 3, 5, 3);
        g.DrawRectangle(Pens.DarkGoldenrod, 2, 5, 12, 8);
        return image;
    }

    private static Image BuildSaveIcon()
    {
        var image = new Bitmap(16, 16);
        using var g = Graphics.FromImage(image);
        g.Clear(Color.Transparent);
        using var body = new SolidBrush(Color.FromArgb(25, 118, 210));
        using var label = new SolidBrush(Color.White);
        using var dark = new SolidBrush(Color.FromArgb(13, 71, 161));
        g.FillRectangle(body, 2, 1, 12, 14);
        g.FillRectangle(label, 4, 3, 7, 4);
        g.FillRectangle(dark, 5, 10, 7, 4);
        g.DrawRectangle(Pens.Black, 2, 1, 12, 14);
        return image;
    }
    private static string Num(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string Money(double value) => value.ToString("C", CultureInfo.GetCultureInfo("en-US"));
}
