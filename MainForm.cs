using System.Globalization;
using System.Text.Json;

namespace CowPilot;

sealed class MainForm : Form
{
    private static readonly Color ChangedColor = Color.Yellow;
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
    private readonly ListBox _customTrimSegmentsList = new() { Height = 120, IntegralHeight = false, HorizontalScrollbar = true };
    private readonly ListBox _customTrimAnglesList = new() { Height = 90, IntegralHeight = false, HorizontalScrollbar = true };
    private readonly ComboBox _customOriginSelector = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 115 };
    private readonly CheckBox _showCustomCoordinates = new() { Text = "Show vertex coordinates", AutoSize = true };
    private readonly Panel _customCoordinatesPanel = new() { Dock = DockStyle.Top, AutoSize = true, Visible = false };
    private readonly NumericUpDown _customVertexX = InchBox();
    private readonly NumericUpDown _customVertexY = InchBox();
    private readonly NumericUpDown _customSegmentLength = PositiveInchBox(12);
    private readonly TextBox _customAngleOrPitch = new() { Width = 92 };
    private readonly NumericUpDown _customInteriorAngle = AngleBox(90);
    private readonly Label _customPitchAngle = new() { Text = "Angle: 0 deg", AutoSize = true };
    private readonly Label _customTrimAdded = new() { Text = "Custom Trim Added", AutoSize = true, ForeColor = ChangedColor, Font = BoldFont(9), Visible = false };
    private readonly Label _customTrimSummary = new() { Dock = DockStyle.Bottom, Height = 24, BorderStyle = BorderStyle.Fixed3D };
    private readonly Label _customTrimPiece = new() { AutoSize = true, Text = "Piece: none" };
    private readonly TextBox _customer = new();
    private readonly TextBox _phone = new();
    private readonly TextBox _color = new();
    private readonly TextBox _notes = new() { Multiline = true, ScrollBars = ScrollBars.Vertical };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 650 };
    private readonly DebugConsoleForm _console = new();
    private AppSettings _settings = SettingsStore.Load();
    private QuoteSet? _lastQuote;
    private string _centerOfBalanceClipboardText = "";
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

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildQuoteTab());
        tabs.TabPages.Add(BuildCustomTrimTab());
        MainMenuStrip = BuildMenu();
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.Controls.Add(MainMenuStrip, 0, 0);
        shell.Controls.Add(BuildToolBar(), 0, 1);
        shell.Controls.Add(tabs, 0, 2);
        shell.Controls.Add(_status, 0, 3);
        Controls.Add(shell);

        WireAutoCalc();
        _timer.Tick += (_, _) => { _timer.Stop(); Recalculate(); };
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
        ResetSavedSnapshot();
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("File");
        file.DropDownItems.Add("New", null, (_, _) => NewQuote());
        file.DropDownItems.Add("Load...", null, (_, _) => LoadQuote());
        file.DropDownItems.Add("Save as...", null, (_, _) => SaveAs());
        file.DropDownItems.Add("Quick Save", null, (_, _) => QuickSave());
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
        return menu;
    }

    private ToolStrip BuildToolBar()
    {
        var toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
        var save = new ToolStripButton
        {
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            Image = BuildSaveIcon(),
            Text = "Quick Save",
            ToolTipText = "Quick Save"
        };
        save.Click += (_, _) => QuickSave();
        toolbar.Items.Add(save);
        return toolbar;
    }

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
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var miscGrid = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
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
        grid.Controls.Add(miscGrid, 0, 0);
        grid.Controls.Add(BuildBootSelector(), 0, 1);
        return Group("Extras", grid);
    }

    private Control BuildBootSelector()
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, ColumnCount = 2 };
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
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
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
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 18));

        _customTrimPiecesList.SelectedIndexChanged += (_, _) =>
        {
            if (_syncingCustomTrimSidebar || _customTrimPiecesList.SelectedIndex < 0) return;
            _customTrim.SelectPiece(_customTrimPiecesList.SelectedIndex);
        };
        _customTrimVerticesList.SelectedIndexChanged += (_, _) =>
        {
            if (_syncingCustomTrimSidebar || _customTrimVerticesList.SelectedIndex < 0) return;
            var vertices = _customTrim.SelectedVertices;
            int index = _customTrimVerticesList.SelectedIndex;
            if (index >= vertices.Count) return;
            SetSidebarVertexValues(vertices[index]);
        };
        _customTrimSegmentsList.SelectedIndexChanged += (_, _) => LoadSelectedFace();
        _customTrimAnglesList.SelectedIndexChanged += (_, _) => LoadSelectedAngle();
        _customOriginSelector.SelectedIndexChanged += (_, _) =>
        {
            if (!_syncingCustomTrimSidebar && _customOriginSelector.SelectedIndex >= 0) _customTrim.SelectedOriginIndex = _customOriginSelector.SelectedIndex;
        };
        _customAngleOrPitch.TextChanged += (_, _) =>
        {
            UpdateAnglePreview();
            LiveUpdateSelectedFace();
        };
        _customSegmentLength.ValueChanged += (_, _) => LiveUpdateSelectedFace();
        _customInteriorAngle.ValueChanged += (_, _) => LiveUpdateSelectedAngle();
        _showCustomCoordinates.CheckedChanged += (_, _) => _customCoordinatesPanel.Visible = _showCustomCoordinates.Checked;
        _customVertexX.ValueChanged += (_, _) => UpdateSelectedVertexFromFields();
        _customVertexY.ValueChanged += (_, _) => UpdateSelectedVertexFromFields();

        var pieceGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
        pieceGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pieceGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _customTrimPiecesList.Dock = DockStyle.Fill;
        pieceGrid.Controls.Add(_customTrimPiecesList, 0, 0);
        pieceGrid.SetColumnSpan(_customTrimPiecesList, 2);
        pieceGrid.Controls.Add(new Label { Text = "Qty", AutoSize = true }, 0, 1);
        pieceGrid.Controls.Add(_customTrimQuantity, 1, 1);
        pieceGrid.Controls.Add(new Label { Text = "Origin", AutoSize = true }, 0, 2);
        var originPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
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
        pieceGrid.Controls.Add(originPanel, 1, 2);
        panel.Controls.Add(Group("Pieces", pieceGrid), 0, 0);

        var segmentGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 10 };
        segmentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        segmentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        segmentGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        segmentGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        segmentGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        segmentGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        segmentGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        segmentGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        segmentGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        segmentGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 28));
        segmentGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        segmentGrid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        segmentGrid.Controls.Add(new Label { Text = "Faces", AutoSize = true }, 0, 0);
        segmentGrid.SetColumnSpan(segmentGrid.GetControlFromPosition(0, 0)!, 2);
        _customTrimSegmentsList.Dock = DockStyle.Fill;
        segmentGrid.Controls.Add(_customTrimSegmentsList, 0, 1);
        segmentGrid.SetColumnSpan(_customTrimSegmentsList, 2);
        var faceActions = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        var addFace = new Button { Text = "Add face", AutoSize = true };
        addFace.Click += (_, _) => AddCustomFace();
        var colorSide = new Button { Text = "Color side", AutoSize = true, Image = BuildColorSideIcon(), TextImageRelation = TextImageRelation.ImageBeforeText };
        colorSide.Click += (_, _) => _customTrim.ToggleColorSide();
        faceActions.Controls.Add(addFace);
        faceActions.Controls.Add(colorSide);
        segmentGrid.Controls.Add(faceActions, 0, 2);
        segmentGrid.SetColumnSpan(faceActions, 2);
        segmentGrid.Controls.Add(new Label { Text = "Length", AutoSize = true }, 0, 3);
        segmentGrid.Controls.Add(_customSegmentLength, 1, 3);
        segmentGrid.Controls.Add(new Label { Text = "Angle/Pitch", AutoSize = true }, 0, 4);
        segmentGrid.Controls.Add(_customAngleOrPitch, 1, 4);
        segmentGrid.Controls.Add(_customPitchAngle, 0, 5);
        segmentGrid.SetColumnSpan(_customPitchAngle, 2);
        segmentGrid.Controls.Add(new Label { Text = "Angles", AutoSize = true }, 0, 6);
        segmentGrid.SetColumnSpan(segmentGrid.GetControlFromPosition(0, 6)!, 2);
        _customTrimAnglesList.Dock = DockStyle.Fill;
        segmentGrid.Controls.Add(_customTrimAnglesList, 0, 7);
        segmentGrid.SetColumnSpan(_customTrimAnglesList, 2);
        segmentGrid.Controls.Add(new Label { Text = "Selected angle", AutoSize = true }, 0, 8);
        segmentGrid.Controls.Add(_customInteriorAngle, 1, 8);
        var note = new Label
        {
            Text = "Faces edit live. Enter degrees, or pitch like 3/12.",
            AutoSize = false,
            Dock = DockStyle.Fill,
            Height = 40
        };
        segmentGrid.Controls.Add(note, 0, 9);
        segmentGrid.SetColumnSpan(note, 2);
        panel.Controls.Add(Group("Faces", segmentGrid), 0, 1);

        var coordGrid = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
        coordGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        coordGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        coordGrid.Controls.Add(_customTrimVerticesList, 0, 0);
        coordGrid.SetColumnSpan(_customTrimVerticesList, 2);
        coordGrid.Controls.Add(new Label { Text = "X", AutoSize = true }, 0, 1);
        coordGrid.Controls.Add(_customVertexX, 1, 1);
        coordGrid.Controls.Add(new Label { Text = "Y", AutoSize = true }, 0, 2);
        coordGrid.Controls.Add(_customVertexY, 1, 2);
        _customCoordinatesPanel.Controls.Add(coordGrid);
        var coordinateStack = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        coordinateStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        coordinateStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        coordinateStack.Controls.Add(_showCustomCoordinates, 0, 0);
        coordinateStack.Controls.Add(_customCoordinatesPanel, 0, 1);
        panel.Controls.Add(Group("Coordinates", coordinateStack), 0, 2);
        return panel;
    }

    private void SyncCustomTrimSidebar()
    {
        _syncingCustomTrimSidebar = true;
        try
        {
            int selectedPiece = _customTrim.SelectedPieceIndex;
            int selectedVertex = _customTrimVerticesList.SelectedIndex;
            int selectedSegment = _customTrimSegmentsList.SelectedIndex;
            int selectedAngle = _customTrimAnglesList.SelectedIndex;
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
                double angle = Math.Atan2(vertices[i].Y - vertices[i - 1].Y, vertices[i].X - vertices[i - 1].X) * 180.0 / Math.PI;
                _customTrimSegmentsList.Items.Add($"Face {i}: V{i}-V{i + 1}  {Num(length)}\" @ {Num(angle)} deg");
            }
            for (int i = 1; i < vertices.Count - 1; i++)
            {
                double angle = InteriorAngle(vertices[i - 1], vertices[i], vertices[i + 1]);
                _customTrimAnglesList.Items.Add($"V{i + 1}: {Num(angle)} deg between faces {i} and {i + 1}");
            }
            if (_customTrim.SelectedOriginIndex >= 0 && _customTrim.SelectedOriginIndex < _customOriginSelector.Items.Count)
            {
                _customOriginSelector.SelectedIndex = _customTrim.SelectedOriginIndex;
            }
            if (selectedVertex >= 0 && selectedVertex < _customTrimVerticesList.Items.Count) _customTrimVerticesList.SelectedIndex = selectedVertex;
            else if (_customTrimVerticesList.Items.Count > 0) _customTrimVerticesList.SelectedIndex = _customTrimVerticesList.Items.Count - 1;
            if (selectedSegment >= 0 && selectedSegment < _customTrimSegmentsList.Items.Count) _customTrimSegmentsList.SelectedIndex = selectedSegment;
            if (selectedAngle >= 0 && selectedAngle < _customTrimAnglesList.Items.Count) _customTrimAnglesList.SelectedIndex = selectedAngle;
            if (_customTrimVerticesList.SelectedIndex >= 0 && _customTrimVerticesList.SelectedIndex < vertices.Count)
            {
                SetSidebarVertexValues(vertices[_customTrimVerticesList.SelectedIndex]);
            }
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
            _customAngleOrPitch.Text = Num(SegmentAngle(from, to));
            UpdateAnglePreview();
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
            _customInteriorAngle.Value = ClampDecimal((decimal)InteriorAngle(vertices[vertexIndex - 1], vertices[vertexIndex], vertices[vertexIndex + 1]), _customInteriorAngle);
        }
        finally
        {
            _syncingCustomTrimSidebar = false;
        }
    }

    private void AddCustomFace()
    {
        if (!TryParseAngleOrPitch(_customAngleOrPitch.Text, out double angle))
        {
            SetStatus("Angle must be degrees or pitch like 3/12.", ErrorColor, Color.White);
            return;
        }
        _customTrim.AddBendSegment((double)_customSegmentLength.Value, angle);
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
        if (!TryParseAngleOrPitch(_customAngleOrPitch.Text, out double angle)) return;
        _customTrim.UpdateSegment(_customTrimSegmentsList.SelectedIndex, (double)_customSegmentLength.Value, angle);
    }

    private void LiveUpdateSelectedAngle()
    {
        if (_syncingCustomTrimSidebar || _customTrimAnglesList.SelectedIndex < 0) return;
        int vertexIndex = _customTrimAnglesList.SelectedIndex + 1;
        _customTrim.UpdateInteriorAngle(vertexIndex, (double)_customInteriorAngle.Value);
    }

    private void UpdateSelectedVertexFromFields()
    {
        if (_syncingCustomTrimSidebar || !_customCoordinatesPanel.Visible || _customTrimVerticesList.SelectedIndex < 0) return;
        _customTrim.UpdateVertex(_customTrimVerticesList.SelectedIndex, SidebarPoint());
    }

    private void UpdateAnglePreview()
    {
        _customPitchAngle.Text = TryParseAngleOrPitch(_customAngleOrPitch.Text, out double angle)
            ? $"Angle: {Num(angle)} deg"
            : "Angle: invalid";
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

    private static double InteriorAngle(PointF previous, PointF vertex, PointF next)
    {
        double ax = previous.X - vertex.X;
        double ay = previous.Y - vertex.Y;
        double bx = next.X - vertex.X;
        double by = next.Y - vertex.Y;
        double lengths = Math.Sqrt(ax * ax + ay * ay) * Math.Sqrt(bx * bx + by * by);
        if (lengths <= 0) return 0;
        return Math.Acos(Math.Clamp(((ax * bx) + (ay * by)) / lengths, -1, 1)) * 180.0 / Math.PI;
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
            LogDebug("Quote recalculated.");
        }
        catch (Exception ex)
        {
            _lastQuote = null;
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
        ApplyInput(new QuoteInput("", ScrewOption.OneInch, false, "0", "0", false, TrimSelection.Empty, MiscSelection.Empty, CustomTrimState.Empty));
        _customer.Clear(); _phone.Clear(); _color.Clear(); _notes.Clear();
        ClearQuoteOutputs();
        ResetSavedSnapshot();
    }

    private void ClearCustomTrim()
    {
        if (_customTrim.PieceCount == 0) return;
        DialogResult result = MessageBox.Show(this, "Clear the custom trim graph?", "Clear Custom Trim", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result != DialogResult.Yes) return;
        _customTrim.ClearPieces();
    }

    private bool SaveAs()
    {
        if (!EnsureQuote()) return false;
        using var dialog = new SaveFileDialog { Filter = "Text files (*.txt)|*.txt", FileName = "cow-pilot-estimate.txt" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return false;
        SaveTo(dialog.FileName);
        return true;
    }

    private bool QuickSave()
    {
        if (!EnsureQuote()) return false;
        string dir = string.IsNullOrWhiteSpace(_settings.General.QuickSaveFolder)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Cow Pilot Estimates")
            : _settings.General.QuickSaveFolder;
        Directory.CreateDirectory(dir);
        SaveTo(Path.Combine(dir, $"cow-pilot-estimate-{DateTime.Now:yyyyMMdd-HHmmss}.txt"));
        return true;
    }

    private void SaveTo(string path)
    {
        var doc = new QuoteDocument(AppVersion.SaveFormatVersion, AppVersion.Version, DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss"),
            _customer.Text, _phone.Text, _color.Text, _notes.Text, CurrentInput());
        File.WriteAllText(path, QuoteSaveLoad.CreateEstimateText(doc, _lastQuote!, _settings.Prices));
        ResetSavedSnapshot();
        SetStatus($"Saved: {path}", SuccessColor, Color.White);
        LogDebug("Saved quote: " + path);
    }

    private void LoadQuote()
    {
        using var dialog = new OpenFileDialog { Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var doc = QuoteSaveLoad.Load(File.ReadAllText(dialog.FileName));
            _customer.Text = doc.CustomerName;
            _phone.Text = doc.Phone;
            _color.Text = doc.Color;
            _notes.Text = doc.Notes;
            ApplyInput(doc.Input);
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
        if (dialog.Choice == UnsavedChoice.SaveAs) return SaveAs();
        if (dialog.Choice == UnsavedChoice.QuickSave) return QuickSave();
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
        _timer.Stop();
        _timer.Start();
    }

    private void ResetSavedSnapshot()
    {
        _timer.Stop();
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
        Color normal = control is TextBox or NumericUpDown ? SystemColors.Window : SystemColors.Control;
        control.BackColor = changed ? ChangedColor : normal;
        control.ForeColor = SystemColors.ControlText;
        if (control is CheckBox checkBox) checkBox.UseVisualStyleBackColor = !changed;
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

    private void LogDebug(string message) => _console.Log(message);

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
        _timer.Interval = _settings.General.AutoRecalculateDelayMs;
        _customTrim.ApplySettings(_settings);
        _customTrimSummary.Text = _customTrim.Summary();
        SyncCustomTrimSidebar();
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
            if (dialog.Choice == UnsavedChoice.SaveAs && !SaveAs())
            {
                e.Cancel = true;
                return;
            }
            if (dialog.Choice == UnsavedChoice.QuickSave && !QuickSave())
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
        foreach (var pair in _screwButtonMap) pair.Value.ForeColor = pair.Value.Checked ? Color.DarkGoldenrod : SystemColors.ControlText;
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

    private static NumericUpDown AngleBox(decimal value = 0) => new()
    {
        Minimum = 0.001m,
        Maximum = 180,
        DecimalPlaces = 3,
        Increment = 1,
        Value = value,
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
