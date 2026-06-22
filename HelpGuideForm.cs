namespace CowPilot;

sealed class HelpGuideForm : Form
{
    public HelpGuideForm()
    {
        Text = "Cow Pilot Calculator Guide";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(900, 700);
        MinimumSize = new Size(700, 500);
        ShowIcon = false;

        var guide = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = true,
            Font = new Font("Consolas", 10),
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.White,
            Text = GuideText
        };

        var close = new Button { Text = "Close", Dock = DockStyle.Bottom, Height = 34 };
        close.Click += (_, _) => Close();

        Controls.Add(guide);
        Controls.Add(close);
    }

    private const string GuideText = """
Cow Pilot Calculator Guide

Overview
Cow Pilot calculates metal panel quotes. It takes panel measurements, standard trim, custom trim drawings, screws, miscellaneous products, boots, discounts, and pricing settings, then displays totals for each metal option at the same time.

The calculator recalculates automatically when valid input changes. If an entry cannot be parsed, the status bar at the bottom explains the problem.


Top Menu And Toolbar

File
- New: clears the current quote. If there are unsaved changes, Cow Pilot asks whether to save first.
- Load: opens an existing Cow Pilot quote file.
- Save: updates the current quote file. If the quote has not been saved yet, Save opens Save As.
- Save As: chooses a new file path for the quote.
- Exit: closes the calculator. If there are unsaved changes, Cow Pilot asks whether to save first.

Edit
- Cut, Copy, Paste, Select All: standard Windows text-editing actions for the active input.

View
- Console: opens the debug console. Normal recalculation does not print every total. Use the console button to recalculate and print the detailed math steps for the grand totals.

Options
- Settings: opens settings for prices and custom trim display preferences.

Help
- Calculator Guide: opens this guide.

Toolbar
- New page icon: same as File > New.
- Open folder icon: same as File > Load.
- Save icon: same as File > Save.


Saving And Loading Quotes

Cow Pilot saves quotes as .cowpilot files. These files use a Cow Pilot save-data extension to prevent accidental editing as ordinary text files, but the contents remain readable plain text for debugging.

Save file naming:
- If Customer is blank, Cow Pilot suggests a timestamped estimate filename.
- If Customer has text, Cow Pilot uses the customer text as the suggested filename.
- Existing saved quotes use normal Save behavior: pressing Save updates the same file.

When closing with unsaved changes, the popup asks:
"Would you like to save your work before closing?"
- Yes: runs Save.
- No: closes without saving.
- Cancel: returns to the calculator.


Calculator Tab

Measurements
Enter panel measurements here. Each line should be quantity x length.

Examples:
2x10'
4x12'6"
3x144
1 @ 96

Accepted separators:
- x
- X
- @

Accepted lengths:
- Feet: 10'
- Feet and inches: 12'6"
- Raw inches: 144

Raw whole numbers are treated as inches. The formatted output converts grouped panel lengths into inches.

Formatted Output
This read-only box groups and sorts the panel measurements. It is useful for checking that the input was understood correctly.

Screws
Choose the screw type that should be priced:
- 1" Screws
- 1-1/2" Screws
- 2" Screws
- Tubing Screws

Suggested screws:
- Cow Pilot calculates suggested screw bags from total panel footage.
- Suggested lap screws are calculated from panel footage and ridge count.

Use suggested screws:
- Checked: Cow Pilot fills the screw bag boxes with suggested values.
- Unchecked: screw bag boxes are manual.

Manual screw bags:
- Screw bags and lap screw bags can be typed directly.
- Blank means zero.
- Negative values are not valid.

Standard Trim
Enter quantities for standard trim pieces:
- Ridges
- Deluxe Corners
- Eaves
- Gables
- Valleys
- Sidewalls
- Endwalls
- Transitions
- J-Trim

Extra inches:
- Each trim row has an extra-inches field.
- Extra inches are for adding custom priced inches onto a standard trim item.
- Extra inches are disabled unless that trim item quantity is greater than zero.

Custom Trim Added
This appears at the bottom of the Standard Trim section when the quote includes custom trim from the Custom Trim tab.

Extras And Boots
Extras include:
- Outside Closures
- Inside Closures
- Butyl Tape
- Caulk
- Vented Closures
- Universal Closures
- Red Snips
- Green Snips
- Blue Snips
- Turbo Shear

Boots:
- Each boot type has its own quantity field.
- Multiple boot sizes can be used on the same quote.

Quote Info
Customer, phone, color, and notes are saved with the quote.

Customer also affects the suggested save filename. If Customer is filled in, Save As suggests that customer name.

Balance
Center of Balance shows the calculated balance point in inches when panel measurements exist.

Copy COB copies only the inch value to the clipboard.

Weight shows estimated total weight for 29 gauge and 26 gauge panel options.

Military Discount
Applies the configured military discount rate to quote subtotals.

Prices
Cow Pilot displays all four panel price options at the same time:
- 29 Galv
- 29 Color
- 26 Galv
- 26 Color

Each option shows:
- Subtotal
- Grand Total

Grand Total includes tax.


Custom Trim Tab

Purpose
The Custom Trim tab is for drawing or editing custom trim side profiles. Each piece is priced from:
- Base custom trim price
- Total inches
- Bend count
- Quantity

Grid
The grid represents measurement space. Snapping controls how points align.

Snap
Controls drawing and movement increments. Default snap is 1".

Rotation Snap
Controls rotation increments when rotating a selected piece.

Re-center
Recenters the graph view.

Clear
Clears all custom trim pieces after confirmation.

Pieces
The pieces list shows each custom trim piece and its total drawn length. Each row is colored to match that piece in the graph.

Quantity
Qty sets how many of the selected custom trim piece are included in the quote.

Origin
Origin is the vertex used when snapping a moved or rotated piece to the grid.

Set Origin
Click Set Origin, then click a vertex on the graph. That vertex becomes the selected piece origin.

Faces
Faces are the straight line segments of a trim piece.

The Faces list shows the length of each face. Each face row has a small face icon next to the length. Selecting a face highlights that face in red on the graph.

Length
Edits the selected face length live.

Add Face
Adds a new straight face to the selected piece.

Color Side
Flips the color-side arrow. The arrow is for visual reference only; it does not change price.

Angles
Angles are bends between two connected faces.

The Angles list shows bend values. Selecting an angle highlights that bend in red on the graph.

Selected angle
Edits the selected bend live.

Accepted angle input:
- Degrees, such as 90
- Pitch, such as 3/12
- Negative pitch, such as -3/12

Angle behavior:
- 0 degrees means flat.
- 90 degrees remains 90.
- Values over 180 are shown as negative bends because real bends do not exceed 180 degrees.

Drawing
- Left click empty grid space to start a new piece.
- Left click again to finish the current face.
- Right click and drag to pan the grid.
- Mouse wheel zooms.
- Clicking an existing face selects that face.
- Clicking an existing bend selects that angle.
- Vertices are not selected during normal editing.

Moving
Drag a selected piece by its marquee area. The selected piece snaps to the nearest grid point while moving.

Rotating
Use the curved arrow handle on the selected piece marquee. While rotating, Cow Pilot shows a ghost preview. Releasing the mouse applies the snapped rotation.

Maximum Size Warning
If a custom trim piece exceeds the configured maximum size, a red warning appears above the graph.


Settings

Prices
Use Options > Settings > Prices to update pricing used by the quote:
- Panel price per linear foot
- Weight per foot
- Screws
- Standard trim
- Extra inches
- Custom trim base/rates
- Misc products
- Boots
- Tax and discount rates

Preferences
Use Options > Settings > Preferences to adjust custom trim graph display:
- Grid visibility
- Random color per trim piece
- Angle arcs
- Selected piece marquee
- Graph colors
- Line thickness
- Vertex size


Common Errors

"Line is not in quantity x length format"
One measurement line is missing quantity, separator, or length.

"Length is not valid"
The length could not be read as feet/inches or raw inches.

"Bags of Screws must be a non-negative number"
A manual screw field is blank-safe, but it cannot contain letters or negative numbers.

"This file does not contain Cow Pilot load data"
The selected file is not a Cow Pilot quote file or was saved before load data existed.

""";
}
