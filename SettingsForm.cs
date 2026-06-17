namespace CowPilot;

sealed class SettingsForm : Form
{
    private readonly ListBox _sections = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly Panel _content = new() { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(10) };
    private AppSettings _settings;

    public AppSettings Settings => _settings;

    public SettingsForm(AppSettings settings)
    {
        _settings = SettingsStore.Clone(settings);
        Text = "Settings";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(850, 620);
        Size = new Size(980, 720);
        ShowIcon = false;
        ShowInTaskbar = false;

        _sections.Items.AddRange(["General", "Prices", "Preferences"]);
        _sections.SelectedIndexChanged += (_, _) => RenderSelectedSection();
        _sections.SelectedIndex = 0;

        var main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(10) };
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        main.Controls.Add(_sections, 0, 0);
        main.Controls.Add(_content, 1, 0);
        main.Controls.Add(BuildButtons(), 0, 1);
        main.SetColumnSpan(main.GetControlFromPosition(0, 1)!, 2);
        Controls.Add(main);
    }

    private Control BuildButtons()
    {
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        var save = new Button { Text = "Save", AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        var defaults = new Button { Text = "Restore Defaults", AutoSize = true };
        save.Click += (_, _) => { _settings.Normalize(); SettingsStore.Save(_settings); };
        defaults.Click += (_, _) =>
        {
            _settings = SettingsStore.DefaultSettings();
            RenderSelectedSection();
        };
        buttons.Controls.AddRange([save, cancel, defaults]);
        AcceptButton = save;
        CancelButton = cancel;
        return buttons;
    }

    private void RenderSelectedSection()
    {
        _content.SuspendLayout();
        _content.Controls.Clear();
        Control page = _sections.SelectedItem?.ToString() switch
        {
            "Prices" => BuildPricesPage(),
            "Preferences" => BuildPreferencesPage(),
            _ => BuildGeneralPage()
        };
        page.Dock = DockStyle.Top;
        _content.Controls.Add(page);
        _content.ResumeLayout();
    }

    private Control BuildGeneralPage()
    {
        var page = Page();
        var general = Grid();
        AddRow(general, "Quote files", new Label { Text = "Use File > Save or Save As. Quotes save as .cowpilot files.", AutoSize = true });
        AddGroup(page, "General", general);
        return page;
    }

    private Control BuildPricesPage()
    {
        var page = Page();
        AddGroup(page, "Tax And Discounts", TaxGrid());
        AddGroup(page, "Panel Metal", MetalGrid());
        AddGroup(page, "Screws", ScrewGrid());
        AddGroup(page, "Standard Trim", NamedPriceGrid(_settings.Prices.StandardTrim));
        AddGroup(page, "Standard Trim Extras", StandardTrimExtrasGrid());
        AddGroup(page, "Custom Trim", CustomTrimGrid());
        AddGroup(page, "Misc", NamedPriceGrid(_settings.Prices.Misc));
        AddGroup(page, "Boots", NamedPriceGrid(_settings.Prices.Boots));
        return page;
    }

    private Control BuildPreferencesPage()
    {
        var page = Page();
        var grid = Grid();
        AddRow(grid, "Show custom trim grid", Check(_settings.Preferences.ShowCustomTrimGrid, value => _settings.Preferences.ShowCustomTrimGrid = value));
        AddRow(grid, "Random color per trim piece", Check(_settings.Preferences.UseRandomCustomTrimPieceColors, value => _settings.Preferences.UseRandomCustomTrimPieceColors = value));
        AddRow(grid, "Show angle arcs", Check(_settings.Preferences.ShowCustomTrimAngleArcs, value => _settings.Preferences.ShowCustomTrimAngleArcs = value));
        AddRow(grid, "Show selected piece marquee", Check(_settings.Preferences.ShowCustomTrimMarquee, value => _settings.Preferences.ShowCustomTrimMarquee = value));
        AddRow(grid, "Graph background", ColorButton(_settings.Preferences.CustomTrimBackgroundArgb, value => _settings.Preferences.CustomTrimBackgroundArgb = value));
        AddRow(grid, "Minor grid color", ColorButton(_settings.Preferences.CustomTrimMinorGridArgb, value => _settings.Preferences.CustomTrimMinorGridArgb = value));
        AddRow(grid, "Major grid color", ColorButton(_settings.Preferences.CustomTrimMajorGridArgb, value => _settings.Preferences.CustomTrimMajorGridArgb = value));
        AddRow(grid, "Line color", ColorButton(_settings.Preferences.CustomTrimLineArgb, value => _settings.Preferences.CustomTrimLineArgb = value));
        AddRow(grid, "Selected line color", ColorButton(_settings.Preferences.CustomTrimSelectedLineArgb, value => _settings.Preferences.CustomTrimSelectedLineArgb = value));
        AddRow(grid, "Selected origin color", ColorButton(_settings.Preferences.CustomTrimOriginArgb, value => _settings.Preferences.CustomTrimOriginArgb = value));
        AddRow(grid, "Minor grid thickness", Number(_settings.Preferences.CustomTrimMinorGridThickness, 0.25, 8, 2, value => _settings.Preferences.CustomTrimMinorGridThickness = (float)value));
        AddRow(grid, "Major grid thickness", Number(_settings.Preferences.CustomTrimMajorGridThickness, 0.25, 8, 2, value => _settings.Preferences.CustomTrimMajorGridThickness = (float)value));
        AddRow(grid, "Line thickness", Number(_settings.Preferences.CustomTrimLineThickness, 0.5, 12, 2, value => _settings.Preferences.CustomTrimLineThickness = (float)value));
        AddRow(grid, "Vertex size", Number(_settings.Preferences.CustomTrimVertexSize, 4, 24, 2, value => _settings.Preferences.CustomTrimVertexSize = (float)value));
        AddGroup(page, "Custom Trim Graph", grid);
        return page;
    }

    private Control TaxGrid()
    {
        var grid = Grid();
        AddRow(grid, "Tax rate (%)", Number(_settings.Prices.TaxRate * 100.0, 0, 25, 3, value => _settings.Prices.TaxRate = value / 100.0));
        AddRow(grid, "Military discount (%)", Number(_settings.Prices.MilitaryDiscountRate * 100.0, 0, 100, 3, value => _settings.Prices.MilitaryDiscountRate = value / 100.0));
        return grid;
    }

    private Control MetalGrid()
    {
        var grid = PriceGrid(4);
        Header(grid, ["Metal", "Panel $/LF", "Weight/LF", "Lap screws/bag"]);
        foreach (var metal in _settings.Prices.Metals)
        {
            int row = AddGridRow(grid);
            grid.Controls.Add(Label(metal.Label), 0, row);
            grid.Controls.Add(Number(metal.LinearFootPrice, 0, 1000, 2, value => metal.LinearFootPrice = value), 1, row);
            grid.Controls.Add(Number(metal.WeightPerFoot, 0, 1000, 3, value => metal.WeightPerFoot = value), 2, row);
            grid.Controls.Add(Number(metal.LapScrewBagPrice, 0, 1000, 2, value => metal.LapScrewBagPrice = value), 3, row);
        }
        return grid;
    }

    private Control ScrewGrid()
    {
        var grid = PriceGrid(3);
        Header(grid, ["Screw", "Unpainted bag", "Painted bag"]);
        foreach (var screw in _settings.Prices.Screws)
        {
            int row = AddGridRow(grid);
            grid.Controls.Add(Label(screw.Label), 0, row);
            grid.Controls.Add(Number(screw.UnpaintedBagPrice, 0, 1000, 2, value => screw.UnpaintedBagPrice = value), 1, row);
            grid.Controls.Add(Number(screw.PaintedBagPrice, 0, 1000, 2, value => screw.PaintedBagPrice = value), 2, row);
        }
        return grid;
    }

    private Control NamedPriceGrid(IEnumerable<NamedPriceSetting> prices)
    {
        var grid = PriceGrid(2);
        Header(grid, ["Item", "Price"]);
        foreach (var price in prices)
        {
            int row = AddGridRow(grid);
            grid.Controls.Add(Label(price.Label), 0, row);
            grid.Controls.Add(Number(price.Price, 0, 100000, 2, value => price.Price = value), 1, row);
        }
        return grid;
    }

    private Control StandardTrimExtrasGrid()
    {
        var grid = Grid();
        AddRow(grid, "26 gauge trim extra", Number(_settings.Prices.Gauge26TrimExtra, 0, 1000, 2, value => _settings.Prices.Gauge26TrimExtra = value));
        AddRow(grid, "Extra inches rate", Number(_settings.Prices.StandardTrimExtraInchRate, 0, 1000, 2, value => _settings.Prices.StandardTrimExtraInchRate = value));
        return grid;
    }

    private Control CustomTrimGrid()
    {
        var grid = Grid();
        AddRow(grid, "Base price <= 20\"", Number(_settings.Prices.CustomTrimBaseUnder20, 0, 10000, 2, value => _settings.Prices.CustomTrimBaseUnder20 = value));
        AddRow(grid, "Base price <= 30\"", Number(_settings.Prices.CustomTrimBaseUnder30, 0, 10000, 2, value => _settings.Prices.CustomTrimBaseUnder30 = value));
        AddRow(grid, "Base price > 30\"", Number(_settings.Prices.CustomTrimBaseOver30, 0, 10000, 2, value => _settings.Prices.CustomTrimBaseOver30 = value));
        AddRow(grid, "Inch rate", Number(_settings.Prices.CustomTrimInchRate, 0, 1000, 2, value => _settings.Prices.CustomTrimInchRate = value));
        AddRow(grid, "Bend rate", Number(_settings.Prices.CustomTrimBendRate, 0, 1000, 2, value => _settings.Prices.CustomTrimBendRate = value));
        AddRow(grid, "Max inches warning", Number(_settings.Prices.CustomTrimMaxInches, 1, 10000, 2, value => _settings.Prices.CustomTrimMaxInches = value));
        AddRow(grid, "Screw coverage LF/bag", Number(_settings.Prices.ScrewCoverageLinearFeetPerBag, 1, 10000, 2, value => _settings.Prices.ScrewCoverageLinearFeetPerBag = value));
        AddRow(grid, "Lap screws per bag", Number(_settings.Prices.LapScrewsPerBag, 1, 10000, 2, value => _settings.Prices.LapScrewsPerBag = value));
        return grid;
    }

    private static TableLayoutPanel Page() => new()
    {
        AutoSize = true,
        ColumnCount = 1,
        Dock = DockStyle.Top,
        Padding = new Padding(0, 0, 8, 8)
    };

    private static TableLayoutPanel Grid()
    {
        var grid = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Top, Padding = new Padding(8) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return grid;
    }

    private static TableLayoutPanel PriceGrid(int columns)
    {
        var grid = new TableLayoutPanel { AutoSize = true, ColumnCount = columns, Dock = DockStyle.Top, Padding = new Padding(8) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        for (int i = 1; i < columns; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        return grid;
    }

    private static void AddGroup(TableLayoutPanel page, string title, Control child)
    {
        var group = new GroupBox { Text = title, AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(8) };
        child.Dock = DockStyle.Top;
        group.Controls.Add(child);
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.Controls.Add(group, 0, page.RowCount++);
    }

    private static void AddRow(TableLayoutPanel grid, string label, Control control)
    {
        int row = AddGridRow(grid);
        grid.Controls.Add(Label(label), 0, row);
        control.Anchor = AnchorStyles.Left;
        grid.Controls.Add(control, 1, row);
    }

    private static int AddGridRow(TableLayoutPanel grid)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        return row;
    }

    private static void Header(TableLayoutPanel grid, string[] labels)
    {
        int row = AddGridRow(grid);
        for (int i = 0; i < labels.Length; i++) grid.Controls.Add(Label(labels[i], true), i, row);
    }

    private static Label Label(string text, bool bold = false) => new()
    {
        Text = text,
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(3, 7, 8, 3),
        Font = bold ? new Font("Segoe UI", 9, FontStyle.Bold) : SystemFonts.DefaultFont
    };

    private static CheckBox Check(bool value, Action<bool> set)
    {
        var check = new CheckBox { Checked = value, AutoSize = true };
        check.CheckedChanged += (_, _) => set(check.Checked);
        return check;
    }

    private static NumericUpDown Number(double value, double min, double max, int decimals, Action<double> set)
    {
        var box = new NumericUpDown
        {
            DecimalPlaces = decimals,
            Minimum = (decimal)min,
            Maximum = (decimal)max,
            Increment = decimals == 0 ? 1 : 0.25m,
            Value = (decimal)Math.Clamp(value, min, max),
            Width = 100,
            TextAlign = HorizontalAlignment.Right
        };
        box.ValueChanged += (_, _) => set((double)box.Value);
        return box;
    }

    private Button ColorButton(int argb, Action<int> set)
    {
        var button = new Button { Width = 100, Height = 26 };
        SetColorButton(button, Color.FromArgb(argb));
        button.Click += (_, _) =>
        {
            using var dialog = new ColorDialog { Color = button.BackColor, FullOpen = true };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            SetColorButton(button, dialog.Color);
            set(dialog.Color.ToArgb());
        };
        return button;
    }

    private static void SetColorButton(Button button, Color color)
    {
        button.BackColor = color;
        button.ForeColor = color.GetBrightness() < 0.45f ? Color.White : Color.Black;
        button.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
