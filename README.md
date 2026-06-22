# Cow Pilot

Cow Pilot is the C#/.NET Windows Forms replacement for the Java panel calculator.

## Download

For normal Windows use, open the private repo's **Releases** page and download:

```text
CowPilot-1.1.10-win-x64.zip
```

Extract the zip and run `CowPilot.exe`.

The release zip is self-contained for Windows x64 and includes the .NET runtime. A separate .NET installer is not required.

## Update Check

On launch, Cow Pilot checks `release/latest.json` in this private repo and only shows a message when a newer version exists. It does not download, install, or auto-update anything.

Because the repo is private, GitHub may reject the version check unless the PC has a token available in `COW_PILOT_GITHUB_TOKEN` or `GITHUB_TOKEN`. If no token is available, Cow Pilot simply skips the update notification.

## Build

```powershell
dotnet build -c Release
```

## Self-test

```powershell
dotnet run -c Release -- --self-test
```

## Package Release Zip

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\package-release.ps1
```

The generated zip is written under `release\` for upload to the matching GitHub Release. `release\latest.json` is committed so the app can check the current version remotely.

Current app version: `1.1.10`.

## Calculator Guide

### Overview

Cow Pilot calculates metal panel quotes from panel measurements, standard trim, custom trim drawings, screws, miscellaneous products, boots, discounts, tax, and saved pricing settings.

The calculator displays all four metal options at the same time:

- `29 Galv`
- `29 Color`
- `26 Galv`
- `26 Color`

Most valid input changes recalculate automatically. If an input cannot be parsed, the status bar at the bottom of the program explains what needs to be fixed.

### Top Menu And Toolbar

#### File

- `New`: Clears the current quote. If there are unsaved changes, Cow Pilot asks whether to save first.
- `Load`: Opens an existing Cow Pilot quote file.
- `Save`: Updates the current quote file. If the quote has not been saved yet, Save opens Save As.
- `Save As`: Chooses a new file path for the quote.
- `Exit`: Closes the calculator.

#### Edit

- `Cut`, `Copy`, `Paste`, and `Select All`: Standard Windows text-editing actions for the active input.

#### View

- `Console`: Opens the debug console. Normal recalculation does not print every total. Use the console button to recalculate and print the detailed math steps for the grand totals.

#### Options

- `Settings`: Opens settings for prices and custom trim display preferences.

#### Help

- `Calculator Guide`: Opens the in-app guide.

#### Toolbar

- New page icon: Same as `File > New`.
- Open folder icon: Same as `File > Load`.
- Save icon: Same as `File > Save`.

### Saving And Loading Quotes

Cow Pilot saves quote files with the `.cowpilot` extension. The file still contains readable text for debugging, but the custom extension helps prevent accidental editing as an ordinary text estimate.

Save file naming:

- If `Customer` is blank, Cow Pilot suggests a timestamped estimate filename.
- If `Customer` has text, Cow Pilot uses the customer text as the suggested filename.
- Existing saved quotes use normal Save behavior: pressing `Save` updates the same file.

When closing with unsaved work, Cow Pilot asks:

```text
Would you like to save your work before closing?
```

- `Yes`: Runs Save.
- `No`: Closes without saving.
- `Cancel`: Returns to the calculator.

### Calculator Tab

#### Measurements

Enter one panel measurement per line in quantity x length format.

Examples:

```text
2x10'
4x12'6"
3x144
1 @ 96
```

Accepted separators:

- `x`
- `X`
- `@`

Accepted lengths:

- Feet: `10'`
- Feet and inches: `12'6"`
- Raw inches: `144`

Raw whole numbers are treated as inches. The formatted output converts grouped panel lengths into inches.

#### Formatted Output

This read-only box groups matching panel lengths and sorts them. Use it to confirm that Cow Pilot understood the measurement input correctly.

#### Screws

Choose the screw type to price:

- `1" Screws`
- `1-1/2" Screws`
- `2" Screws`
- `Tubing Screws`

Cow Pilot suggests screw bags from total panel footage and lap screw bags from panel footage plus ridge count.

- `Use suggested screws` checked: Cow Pilot fills the charged screw boxes automatically.
- `Use suggested screws` unchecked: The screw bag boxes are manual.
- Blank manual screw fields mean zero.
- Negative values are not valid.

#### Standard Trim

Standard trim includes:

- Ridges
- Deluxe Corners
- Eaves
- Gables
- Valleys
- Sidewalls
- Endwalls
- Transitions
- J-Trim

Each trim row has a quantity and an extra-inches field.

- Extra inches add custom-priced inches to a standard trim item.
- Extra inches stay disabled until that trim row has a quantity greater than zero.
- `Custom Trim Added` appears at the bottom of the Standard Trim section when the quote includes custom trim from the Custom Trim tab.

#### Extras And Boots

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

Boots are listed independently so multiple boot sizes and quantities can be used on one quote.

#### Quote Info, Balance, And Prices

- `Customer`, `Phone`, `Color`, and `Notes` are saved with the quote.
- `Customer` also affects the suggested save filename.
- `Center of Balance` shows the calculated balance point in inches when panel measurements exist.
- `Copy COB` copies only the inch value.
- `Weight` shows estimated total weight for 29 gauge and 26 gauge options.
- `Military Discount` applies the configured discount rate to quote subtotals.
- `Prices` shows subtotal and grand total for each metal option. Grand total includes tax.

### Custom Trim Tab

#### Purpose

Custom Trim is for drawing or editing custom trim side profiles. Each piece is priced from:

- Base custom trim price
- Total inches
- Bend count
- Quantity

#### Grid Controls

- `Snap`: Controls drawing and movement increments. Default snap is 1 inch.
- `Rotation Snap`: Controls rotation increments.
- `Re-center`: Recenters the graph view.
- `Clear`: Removes all custom trim pieces after confirmation.

#### Pieces

The Pieces list shows each custom trim piece and its total drawn length. Each row is colored to match that piece in the graph.

- `Qty`: Controls how many of the selected custom trim piece are included in the quote.
- `Set Origin`: Lets you click a vertex that should snap to the grid when moving or rotating the piece.

#### Faces

Faces are the straight line segments of a trim piece.

- The Faces list shows the length of each face with a small face icon.
- Selecting a face highlights it red on the graph.
- `Length` edits the selected face length live.
- `Add Face` adds a new straight face to the selected piece.
- `Color Side` flips the reference arrow. It does not affect price.

#### Angles

Angles are bends between two connected faces.

- Selecting an angle highlights that bend red on the graph.
- `Selected angle` edits the bend live.

Accepted angle input:

- Degrees, such as `90`
- Pitch, such as `3/12`
- Negative pitch, such as `-3/12`

Angle behavior:

- `0` degrees means flat.
- `90` degrees remains 90.
- Values over 180 are shown as negative bends.

#### Drawing, Moving, And Rotating

- Left click empty grid space to start a new piece.
- Left click again to finish the current face.
- Right click and drag to pan the grid.
- Mouse wheel zooms.
- Clicking an existing face selects that face.
- Clicking an existing bend selects that angle.
- Vertices are not selected during normal editing.
- Drag a selected piece by its marquee area. It snaps to the nearest grid point while moving.
- Use the curved arrow handle to rotate. Cow Pilot shows a snapped ghost preview before applying the rotation.

### Settings

#### Prices

Use `Options > Settings > Prices` to update pricing used by the quote:

- Panel price per linear foot
- Weight per foot
- Screws
- Standard trim
- Extra inches
- Custom trim base/rates
- Misc products
- Boots
- Tax and discount rates

#### Preferences

Use `Options > Settings > Preferences` to adjust custom trim graph display:

- Grid visibility
- Random color per trim piece
- Angle arcs
- Selected piece marquee
- Graph colors
- Line thickness
- Vertex size

### Common Errors

#### Measurement Format Errors

- `Line is not in quantity x length format`: One measurement line is missing quantity, separator, or length.
- `Length is not valid`: The length could not be read as feet/inches or raw inches.

#### Other Input Errors

- `Bags of Screws must be a non-negative number`: Manual screw fields cannot contain letters or negative values.
- `This file does not contain Cow Pilot load data`: The selected file is not a Cow Pilot quote file or was saved before load data existed.
