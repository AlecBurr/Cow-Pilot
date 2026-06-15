using System.Globalization;
using System.Text.Json;

namespace CowPilot;

sealed class MainForm : Form
{
    private static readonly Color ChangedColor = Color.FromArgb(255, 193, 7);
    private static readonly Color SuccessColor = Color.FromArgb(46, 125, 50);
    private static readonly Color ErrorColor = Color.FromArgb(183, 28, 28);

    private readonly TextBox _measurements = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 10) };
    private readonly TextBox _formatted = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, Font = new Font("Consolas", 10) };
    private readonly TextBox _totalLf = ReadOnlyBox();
    private readonly TextBox _screwBags = new() { Text = "0", TextAlign = HorizontalAlignment.Right };
    private readonly TextBox _lapScrewBags = new() { Text = "0", TextAlign = HorizontalAlignment.Right };
    private readonly Label _suggestedScrews = new() { Text = "Suggested: 0", AutoSize = true };
    private readonly Label _suggestedLapScrews = new() { Text = "Suggested: 0", AutoSize = true };
    private readonly Label _status = new() { Dock = DockStyle.Fill, Height = 24, BorderStyle = BorderStyle.Fixed3D, Text = "Ready" };
    private readonly Label _centerOfBalance = new() { AutoSize = false, Height = 34, MinimumSize = new Size(240, 34), TextAlign = ContentAlignment.MiddleLeft, BorderStyle = BorderStyle.Fixed3D };
    private readonly CheckBox _useSuggested = new() { Text = "Use suggested screws", AutoSize = true };
    private readonly CheckBox _militaryDiscount = new() { Text = "Military discount", AutoSize = true };
    private readonly RadioButton[] _screwButtons;
    private readonly Dictionary<ScrewOption, RadioButton> _screwButtonMap = [];
    private readonly Dictionary<MetalOption, TextBox> _subtotalBoxes = [];
    private readonly Dictionary<MetalOption, TextBox> _grandTotalBoxes = [];
    private readonly Dictionary<string, NumericUpDown> _trimCounts = [];
    private readonly Dictionary<string, NumericUpDown> _trimExtras = [];
    private readonly NumericUpDown[] _miscCounts = [CountBox(), CountBox(), CountBox(), CountBox(), CountBox(), CountBox()];
    private readonly NumericUpDown[] _bootCounts = QuoteCalculator.BootNames.Select(_ => CountBox()).ToArray();
    private readonly CustomTrimControl _customTrim = new();
    private readonly NumericUpDown _customTrimQuantity = CountBox(1);
    private readonly Label _customTrimSummary = new() { Dock = DockStyle.Bottom, Height = 24, BorderStyle = BorderStyle.Fixed3D };
    private readonly Label _customTrimPiece = new() { AutoSize = true, Text = "Piece: none" };
    private readonly TextBox _customer = new();
    private readonly TextBox _phone = new();
    private readonly TextBox _color = new();
    private readonly TextBox _notes = new() { Multiline = true, ScrollBars = ScrollBars.Vertical };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 650 };
    private readonly DebugConsoleForm _console = new();
    private QuoteSet? _lastQuote;
    private string _savedSnapshot = "";
    private bool _isDirty;
    private bool _suppress;

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

        WireAutoCalc(this);
        _timer.Tick += (_, _) => { _timer.Stop(); Recalculate(); };
        _customTrim.TrimChanged += (_, _) =>
        {
            _customTrimSummary.Text = _customTrim.Summary();
            _customTrimPiece.Text = _customTrim.SelectedPieceText;
            _customTrimQuantity.Value = Math.Max(_customTrimQuantity.Minimum, Math.Min(_customTrimQuantity.Maximum, _customTrim.SelectedQuantity));
            MarkDirty();
        };
        _customTrimQuantity.ValueChanged += (_, _) => _customTrim.SelectedQuantity = (int)_customTrimQuantity.Value;
        _useSuggested.CheckedChanged += (_, _) => ApplySuggestedScrews();
        _customTrimSummary.Text = _customTrim.Summary();
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
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 3, Padding = new Padding(8) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 26));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 22));
        page.Controls.Add(root);

        root.Controls.Add(Group("Measurements", _measurements), 0, 0);
        root.SetRowSpan(_measurements.Parent!, 2);
        root.Controls.Add(Group("Formatted Output", _formatted), 1, 0);
        root.SetRowSpan(_formatted.Parent!, 2);
        root.Controls.Add(BuildScrewsPanel(), 2, 0);
        root.Controls.Add(BuildTrimPanel(), 2, 1);
        root.Controls.Add(BuildMiscPanel(), 3, 0);
        root.SetRowSpan(root.GetControlFromPosition(3, 0)!, 2);
        root.Controls.Add(BuildInfoPanel(), 0, 2);
        root.SetColumnSpan(root.GetControlFromPosition(0, 2)!, 2);
        root.Controls.Add(BuildCobPanel(), 2, 2);
        root.Controls.Add(BuildPricesPanel(), 3, 2);
        return page;
    }

    private Control BuildScrewsPanel()
    {
        return Group("Screws", Stack(_screwButtons.Cast<Control>().Concat([
            _useSuggested,
            Row("Total LF", _totalLf),
            Row("Screw bags", _screwBags, _suggestedScrews),
            Row("Lap bags", _lapScrewBags, _suggestedLapScrews)
        ]).ToArray()));
    }

    private Control BuildTrimPanel()
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3 };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        grid.Controls.Add(new Label { Text = "Trim", AutoSize = true }, 0, 0);
        grid.Controls.Add(new Label { Text = "Qty", AutoSize = true }, 1, 0);
        grid.Controls.Add(new Label { Text = "Extra in.", AutoSize = true }, 2, 0);

        string[] names = ["Ridges", "Eaves", "Gables", "Valleys", "Sidewalls", "Endwalls", "Transitions", "J-Trim"];
        for (int i = 0; i < names.Length; i++)
        {
            _trimCounts[names[i]] = CountBox();
            _trimExtras[names[i]] = CountBox();
            grid.Controls.Add(new Label { Text = names[i], AutoSize = true }, 0, i + 1);
            grid.Controls.Add(_trimCounts[names[i]], 1, i + 1);
            grid.Controls.Add(_trimExtras[names[i]], 2, i + 1);
        }
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
            if (!string.IsNullOrWhiteSpace(_centerOfBalance.Text)) Clipboard.SetText(_centerOfBalance.Text);
        };
        cobPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        cobPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        cobPanel.Controls.Add(copy, 0, 0);
        cobPanel.Controls.Add(_centerOfBalance, 1, 0);
        panel.Controls.Add(cobPanel, 0, 1);
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
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 4 };
        string[] misc = ["Outside Closures (4)", "Inside Closures (4)", "Butyl Tape (45')", "Caulk", "Vented Closures (1)", "Universal Closures (20')"];
        for (int i = 0; i < misc.Length; i++)
        {
            grid.Controls.Add(new Label { Text = misc[i], AutoSize = true }, 0, i);
            grid.Controls.Add(_miscCounts[i], 1, i);
        }
        for (int i = 0; i < QuoteCalculator.BootNames.Length; i++)
        {
            int row = i % 8;
            int col = i < 8 ? 2 : 0;
            if (i >= 8) row += misc.Length;
            grid.Controls.Add(new Label { Text = QuoteCalculator.BootNames[i], AutoSize = true }, col, row);
            grid.Controls.Add(_bootCounts[i], col + 1, row);
        }
        return Group("Extras", grid);
    }

    private TabPage BuildCustomTrimTab()
    {
        var page = new TabPage("Custom Trim");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        var newPiece = new Button { Text = "New Piece", AutoSize = true };
        var recenter = new Button { Text = "Re-center", AutoSize = true };
        var undo = new Button { Text = "Undo", AutoSize = true };
        var clear = new Button { Text = "Clear", AutoSize = true };
        var colorSide = new Button { Text = "Color Side", AutoSize = true };
        var zoomIn = new Button { Text = "+", AutoSize = true };
        var zoomOut = new Button { Text = "-", AutoSize = true };
        var snap = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
        snap.Items.AddRange(new object[] { "1/16\"", "1/8\"", "1/4\"", "1/2\"", "1\"", "1 ft" });
        snap.SelectedIndex = 1;
        snap.SelectedIndexChanged += (_, _) => _customTrim.SnapInches = snap.SelectedIndex switch { 0 => 1f / 16f, 1 => 1f / 8f, 2 => 0.25f, 3 => 0.5f, 4 => 1f, _ => 12f };
        newPiece.Click += (_, _) => _customTrim.BeginNewPiece();
        recenter.Click += (_, _) => _customTrim.Recenter();
        undo.Click += (_, _) => _customTrim.Undo();
        clear.Click += (_, _) => _customTrim.ClearPieces();
        colorSide.Click += (_, _) => _customTrim.ToggleColorSide();
        zoomIn.Click += (_, _) => _customTrim.Zoom(1.25f);
        zoomOut.Click += (_, _) => _customTrim.Zoom(0.8f);
        toolbar.Controls.AddRange([new Label { Text = "Snap", AutoSize = true }, snap, _customTrimPiece, new Label { Text = "Qty", AutoSize = true }, _customTrimQuantity, newPiece, recenter, undo, clear, colorSide, zoomOut, zoomIn]);
        root.Controls.Add(toolbar, 0, 0);
        root.Controls.Add(_customTrim, 0, 1);
        root.Controls.Add(_customTrimSummary, 0, 2);
        page.Controls.Add(root);
        return page;
    }

    private void Recalculate()
    {
        if (_suppress) return;
        try
        {
            var input = CurrentInput();
            _lastQuote = QuoteCalculator.Calculate(input);
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
            _centerOfBalance.Text = _lastQuote.GroupedPanels.Count == 0
                ? ""
                : $"Center of Balance: {Num(_lastQuote.CenterOfBalance)}\"";
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
        Extra("Ridges"), Extra("Gables"), Extra("Eaves"), Extra("Endwalls"), Extra("Sidewalls"), Extra("Valleys"), Extra("Transitions"), Extra("J-Trim"));

    private MiscSelection CurrentMisc() => new((int)_miscCounts[0].Value, (int)_miscCounts[1].Value, (int)_miscCounts[2].Value,
        (int)_miscCounts[3].Value, (int)_miscCounts[4].Value, (int)_miscCounts[5].Value, _bootCounts.Select(n => (int)n.Value).ToArray());

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
    }

    private void SetMisc(MiscSelection misc)
    {
        int[] values = [misc.OutsideClosures, misc.InsideClosures, misc.ButylTape, misc.Caulk, misc.VentedClosures, misc.UniversalClosures];
        for (int i = 0; i < values.Length; i++) _miscCounts[i].Value = values[i];
        for (int i = 0; i < _bootCounts.Length; i++) _bootCounts[i].Value = misc.BootCount(i);
    }

    private void NewQuote()
    {
        ApplyInput(new QuoteInput("", ScrewOption.OneInch, false, "", "", false, TrimSelection.Empty, MiscSelection.Empty, CustomTrimState.Empty));
        _customer.Clear(); _phone.Clear(); _color.Clear(); _notes.Clear();
        ResetSavedSnapshot();
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
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Cow Pilot Estimates");
        Directory.CreateDirectory(dir);
        SaveTo(Path.Combine(dir, $"cow-pilot-estimate-{DateTime.Now:yyyyMMdd-HHmmss}.txt"));
        return true;
    }

    private void SaveTo(string path)
    {
        var doc = new QuoteDocument(AppVersion.SaveFormatVersion, AppVersion.Version, DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss"),
            _customer.Text, _phone.Text, _color.Text, _notes.Text, CurrentInput());
        File.WriteAllText(path, QuoteSaveLoad.CreateEstimateText(doc, _lastQuote!));
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

    private void WireAutoCalc(Control root)
    {
        foreach (Control control in root.Controls)
        {
            if (control is TextBox textBox) textBox.TextChanged += (_, _) => MarkDirty();
            if (control is CheckBox checkBox) checkBox.CheckedChanged += (_, _) => MarkDirty();
            if (control is RadioButton radioButton) radioButton.CheckedChanged += (_, _) => { HighlightSelectedScrew(); MarkDirty(); };
            if (control is NumericUpDown numeric) numeric.ValueChanged += (_, _) => MarkDirty();
            WireAutoCalc(control);
        }
    }

    private void MarkDirty()
    {
        if (_suppress) return;
        _isDirty = CurrentSnapshot() != _savedSnapshot;
        UpdateChangedHighlights();
        _timer.Stop();
        _timer.Start();
    }

    private void ResetSavedSnapshot()
    {
        _timer.Stop();
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
        SetChanged(_measurements, _measurements.TextLength > 0);
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
        SetChanged(_customTrimQuantity, _customTrim.State.Pieces.Count > 0 && _customTrimQuantity.Value != 1);
        HighlightSelectedScrew();
    }

    private static void SetChanged(Control control, bool changed)
    {
        Color normal = control is TextBox or NumericUpDown ? SystemColors.Window : SystemColors.Control;
        control.BackColor = changed ? ChangedColor : normal;
        if (control is CheckBox checkBox) checkBox.UseVisualStyleBackColor = !changed;
    }

    private void SetStatus(string text, Color backColor, Color foreColor)
    {
        _status.Text = text;
        _status.BackColor = backColor;
        _status.ForeColor = foreColor;
    }

    private void ShowConsole()
    {
        if (_console.IsDisposed) return;
        _console.Show(this);
        _console.BringToFront();
    }

    private void LogDebug(string message) => _console.Log(message);

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

    private static FlowLayoutPanel Stack(params Control[] controls)
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoScroll = true, WrapContents = false };
        panel.Controls.AddRange(controls);
        return panel;
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
    private static Font BoldFont(float size) => new("Segoe UI", size, FontStyle.Bold);
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
