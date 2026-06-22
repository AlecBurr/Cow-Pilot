namespace CowPilot;

sealed class HelpGuideForm : Form
{
    private static readonly Color WindowBack = Color.FromArgb(24, 24, 24);
    private static readonly Color PanelBack = Color.FromArgb(36, 36, 36);
    private static readonly Color HeaderBack = Color.FromArgb(20, 77, 120);
    private static readonly Color Accent = Color.FromArgb(102, 187, 255);
    private static readonly Color MutedText = Color.FromArgb(210, 210, 210);

    private readonly FlowLayoutPanel _content = new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = true,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        Padding = new Padding(18, 14, 18, 24),
        BackColor = WindowBack
    };

    private readonly List<Label> _wrappingLabels = [];

    public HelpGuideForm()
    {
        Text = "Cow Pilot Calculator Guide";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(980, 760);
        MinimumSize = new Size(780, 560);
        ShowIcon = false;
        BackColor = WindowBack;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(BuildTitle(), 0, 0);
        root.Controls.Add(_content, 0, 1);
        root.Controls.Add(BuildCloseButton(), 0, 2);
        Controls.Add(root);

        BuildGuide();
        _content.Resize += (_, _) => UpdateWrapWidths();
        Shown += (_, _) => UpdateWrapWidths();
    }

    private Control BuildTitle()
    {
        var title = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = HeaderBack, Padding = new Padding(18, 10, 18, 8) };
        title.Controls.Add(new Label
        {
            Text = "Cow Pilot Calculator Guide",
            Dock = DockStyle.Top,
            Height = 34,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 18, FontStyle.Bold)
        });
        title.Controls.Add(new Label
        {
            Text = "How to enter measurements, build quotes, use custom trim, save files, and read each section.",
            Dock = DockStyle.Bottom,
            Height = 24,
            ForeColor = Color.FromArgb(230, 240, 250),
            Font = new Font("Segoe UI", 10, FontStyle.Regular)
        });
        return title;
    }

    private Control BuildCloseButton()
    {
        var close = new Button
        {
            Text = "Close",
            Dock = DockStyle.Bottom,
            Height = 38,
            BackColor = Color.FromArgb(48, 48, 48),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        close.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
        close.Click += (_, _) => Close();
        return close;
    }

    private void BuildGuide()
    {
        AddSection("Overview",
            ("What Cow Pilot Does",
            [
                "Cow Pilot calculates metal panel quotes from panel measurements, trim, custom trim, screws, boots, extras, discount settings, tax, and saved pricing.",
                "The calculator displays all four metal options at once: 29 Galv, 29 Color, 26 Galv, and 26 Color.",
                "Most inputs recalculate automatically. If an input cannot be understood, the bottom status bar shows what needs to be fixed."
            ]));

        AddSection("Top Menu And Toolbar",
            ("File Menu",
            [
                "New clears the current quote. If unsaved work exists, Cow Pilot asks whether to save before closing or clearing.",
                "Load opens an existing Cow Pilot quote file.",
                "Save updates the current quote file. If the quote has not been saved yet, Save opens Save As.",
                "Save As chooses a new file path.",
                "Exit closes the calculator."
            ]),
            ("Other Menus",
            [
                "Edit contains standard Windows text actions: Cut, Copy, Paste, and Select All.",
                "View opens the debug console. Use the console button to recalculate and print the detailed math steps.",
                "Options opens Settings for prices and custom trim display preferences.",
                "Help opens this calculator guide."
            ]),
            ("Toolbar Icons",
            [
                "New page icon: same as File > New.",
                "Open folder icon: same as File > Load.",
                "Save icon: same as File > Save."
            ]));

        AddSection("Saving And Loading Quotes",
            ("Cow Pilot Quote Files",
            [
                "Cow Pilot saves quote files with the .cowpilot extension.",
                "The file still contains readable text for debugging, but the custom extension helps prevent accidental editing as an ordinary text estimate.",
                "When Customer is blank, Cow Pilot suggests a timestamped filename.",
                "When Customer is filled in, Cow Pilot uses the customer name as the suggested filename."
            ]),
            ("Unsaved Work Prompt",
            [
                "When closing with unsaved work, Cow Pilot asks: Would you like to save your work before closing?",
                "Yes runs Save.",
                "No closes without saving.",
                "Cancel returns to the calculator."
            ]));

        AddSection("Calculator Tab",
            ("Measurements",
            [
                "Enter one panel measurement per line in quantity x length format.",
                "Examples: 2x10', 4x12'6\", 3x144, or 1 @ 96.",
                "Accepted separators are x, X, and @.",
                "Accepted lengths are feet, feet plus inches, or raw inches. A whole number by itself is treated as inches."
            ]),
            ("Formatted Output",
            [
                "This read-only box groups matching panel lengths and sorts them.",
                "Use it to confirm that Cow Pilot understood the measurement input correctly."
            ]),
            ("Screws",
            [
                "Choose the screw type to price: 1\", 1-1/2\", 2\", or Tubing Screws.",
                "Cow Pilot suggests screw bags from total panel footage and lap screw bags from panel footage plus ridge count.",
                "Use suggested screws fills the charged screw boxes automatically.",
                "When Use suggested screws is unchecked, the screw bag boxes are manual. Blank means zero."
            ]),
            ("Standard Trim",
            [
                "Standard trim includes Ridges, Deluxe Corners, Eaves, Gables, Valleys, Sidewalls, Endwalls, Transitions, and J-Trim.",
                "Each trim row has a quantity and an extra-inches field.",
                "Extra inches add custom-priced inches to a standard trim item.",
                "Extra inches stay disabled until that trim row has a quantity greater than zero."
            ]),
            ("Extras And Boots",
            [
                "Extras include closures, butyl tape, caulk, snips, and turbo shear products.",
                "Boots are listed independently so multiple boot sizes and quantities can be used on one quote."
            ]),
            ("Quote Info, Balance, And Prices",
            [
                "Customer, phone, color, and notes are saved with the quote.",
                "Center of Balance shows the calculated balance point in inches when panel measurements exist.",
                "Copy COB copies only the inch value.",
                "Weight shows estimated total weight for 29 gauge and 26 gauge options.",
                "Prices shows subtotal and grand total for each metal option. Grand total includes tax."
            ]));

        AddSection("Custom Trim Tab",
            ("Purpose",
            [
                "Custom Trim is for drawing or editing custom trim side profiles.",
                "Each piece is priced from base custom trim price, total inches, bend count, and quantity."
            ]),
            ("Grid Controls",
            [
                "Snap controls drawing and movement increments. Default snap is 1 inch.",
                "Rotation Snap controls rotation increments.",
                "Re-center recenters the graph view.",
                "Clear removes all custom trim pieces after confirmation."
            ]),
            ("Pieces",
            [
                "The Pieces list shows each custom trim piece and its total drawn length.",
                "Each row is colored to match that piece in the graph.",
                "Qty controls how many of the selected custom trim piece are included in the quote.",
                "Set Origin lets you click a vertex that should snap to the grid when moving or rotating the piece."
            ]),
            ("Faces",
            [
                "Faces are the straight line segments of a trim piece.",
                "The Faces list shows the length of each face with a small face icon.",
                "Selecting a face highlights it red on the graph.",
                "Length edits the selected face length live.",
                "Add Face adds a new straight face to the selected piece.",
                "Color Side flips the reference arrow. It does not affect price."
            ]),
            ("Angles",
            [
                "Angles are bends between two connected faces.",
                "Selecting an angle highlights that bend red on the graph.",
                "Selected angle edits the bend live.",
                "Accepted input includes degrees, pitch such as 3/12, and negative pitch such as -3/12.",
                "0 degrees means flat, 90 remains 90, and values over 180 are shown as negative bends."
            ]),
            ("Drawing, Moving, And Rotating",
            [
                "Left click empty grid space to start a new piece, then left click again to finish the current face.",
                "Right click and drag pans the grid. Mouse wheel zooms.",
                "Clicking an existing face selects that face. Clicking an existing bend selects that angle.",
                "Drag a selected piece by its marquee area. It snaps to the nearest grid point while moving.",
                "Use the curved arrow handle to rotate. Cow Pilot shows a snapped ghost preview before applying the rotation."
            ]));

        AddSection("Settings",
            ("Prices",
            [
                "Options > Settings > Prices controls panel price per linear foot, weight per foot, screws, trim, extra inches, custom trim, misc products, boots, tax, and discounts.",
                "These settings are used by the quote calculation and saved estimate text."
            ]),
            ("Preferences",
            [
                "Options > Settings > Preferences controls custom trim graph display.",
                "Available display options include grid visibility, random piece colors, angle arcs, selected-piece marquee, graph colors, line thickness, and vertex size."
            ]));

        AddSection("Common Errors",
            ("Measurement Format Errors",
            [
                "Line is not in quantity x length format: one measurement line is missing quantity, separator, or length.",
                "Length is not valid: the length could not be read as feet/inches or raw inches."
            ]),
            ("Other Input Errors",
            [
                "Bags of Screws must be a non-negative number: manual screw fields cannot contain letters or negative values.",
                "This file does not contain Cow Pilot load data: the selected file is not a Cow Pilot quote file or was saved before load data existed."
            ]));
    }

    private void AddSection(string title, params (string Heading, string[] Lines)[] groups)
    {
        _content.Controls.Add(SectionHeader(title));
        foreach (var group in groups) _content.Controls.Add(InfoBlock(group.Heading, group.Lines));
    }

    private Control SectionHeader(string text)
    {
        var label = new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = Accent,
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            Margin = new Padding(0, 18, 0, 8)
        };
        _wrappingLabels.Add(label);
        return label;
    }

    private Control InfoBlock(string heading, IEnumerable<string> lines)
    {
        var block = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 1,
            BackColor = PanelBack,
            Padding = new Padding(14, 10, 14, 12),
            Margin = new Padding(0, 0, 0, 10)
        };
        block.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var headingLabel = new Label
        {
            Text = heading,
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 6)
        };
        _wrappingLabels.Add(headingLabel);
        block.Controls.Add(headingLabel);

        foreach (string line in lines)
        {
            var bullet = new Label
            {
                Text = "- " + line,
                AutoSize = true,
                ForeColor = MutedText,
                Font = new Font("Segoe UI", 10.5f),
                Margin = new Padding(18, 2, 8, 2)
            };
            _wrappingLabels.Add(bullet);
            block.Controls.Add(bullet);
        }

        return block;
    }

    private void UpdateWrapWidths()
    {
        int width = Math.Max(420, _content.ClientSize.Width - 60);
        foreach (Label label in _wrappingLabels) label.MaximumSize = new Size(width, 0);
        foreach (Control control in _content.Controls) control.Width = width;
    }
}
