using System.Text.Json.Serialization;

namespace CowPilot;

enum MetalOption
{
    Galv29,
    Color29,
    Galv26,
    Color26
}

enum ScrewOption
{
    OneInch,
    OneAndHalfInch,
    TwoInch,
    Tubing
}

readonly record struct PanelMeasurement(int Quantity, int LengthInInches);

sealed record TrimSelection(
    int Ridges,
    int Gables,
    int Eaves,
    int Endwalls,
    int Sidewalls,
    int Valleys,
    int Transitions,
    int JTrim,
    int DeluxeCorners,
    double RidgesExtraInches,
    double GablesExtraInches,
    double EavesExtraInches,
    double EndwallsExtraInches,
    double SidewallsExtraInches,
    double ValleysExtraInches,
    double TransitionsExtraInches,
    double JTrimExtraInches,
    double DeluxeCornersExtraInches)
{
    public static TrimSelection Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    [JsonIgnore]
    public double TotalExtraInches => RidgesExtraInches + GablesExtraInches + EavesExtraInches + EndwallsExtraInches
        + SidewallsExtraInches + ValleysExtraInches + TransitionsExtraInches + JTrimExtraInches + DeluxeCornersExtraInches;

    [JsonIgnore]
    public bool HasItems => Ridges > 0 || Gables > 0 || Eaves > 0 || Endwalls > 0 || Sidewalls > 0
        || Valleys > 0 || Transitions > 0 || JTrim > 0 || DeluxeCorners > 0 || TotalExtraInches > 0;
}

sealed record MiscSelection(
    int OutsideClosures,
    int InsideClosures,
    int ButylTape,
    int Caulk,
    int VentedClosures,
    int UniversalClosures,
    int RedSnips,
    int GreenSnips,
    int BlueSnips,
    int TurboShear,
    int[] BootCounts)
{
    public static MiscSelection Empty => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, new int[QuoteCalculator.BootCatalog.Length]);

    [JsonIgnore]
    public bool HasItems => OutsideClosures > 0 || InsideClosures > 0 || ButylTape > 0 || Caulk > 0
        || VentedClosures > 0 || UniversalClosures > 0 || RedSnips > 0 || GreenSnips > 0 || BlueSnips > 0
        || TurboShear > 0 || BootCounts.Any(count => count > 0);

    public int BootCount(int index) => index >= 0 && index < BootCounts.Length ? BootCounts[index] : 0;
}

sealed class CustomTrimPieceState(int quantity, List<PointF> vertices)
{
    public int Quantity { get; set; } = quantity;
    public List<PointF> Vertices { get; set; } = vertices;
    public int OriginIndex { get; set; }
}

sealed record CustomTrimState(
    List<CustomTrimPieceState> Pieces,
    float ZoomPixelsPerInch,
    float OffsetX,
    float OffsetY,
    float SnapInches,
    int ColorSide)
{
    public static CustomTrimState Empty => new([], 64, 0, 0, 1f, 1);
}

sealed record QuoteInput(
    string MeasurementsText,
    ScrewOption Screw,
    bool UseSuggestedScrews,
    string ScrewBagsText,
    string LapScrewBagsText,
    bool MilitaryDiscount,
    TrimSelection Trim,
    MiscSelection Misc,
    CustomTrimState CustomTrim);

sealed record QuoteSet(
    SortedDictionary<int, int> GroupedPanels,
    TrimSelection Trim,
    MiscSelection Misc,
    ScrewOption Screw,
    double TotalLengthInFeet,
    double CenterOfBalance,
    double SuggestedScrewBags,
    double SuggestedLapScrewBags,
    double ChargedScrewBags,
    double ChargedLapScrewBags,
    double CustomTrimPrice,
    bool MilitaryDiscountApplied,
    Dictionary<MetalOption, QuoteResult> Quotes);

sealed record QuoteResult(
    MetalOption Metal,
    double TotalWeight,
    double CenterOfBalance,
    double ScrewBags,
    double LapScrewBags,
    double Subtotal,
    double GrandTotal);

sealed record QuoteDocument(
    string FormatVersion,
    string AppVersion,
    string SavedAt,
    string CustomerName,
    string Phone,
    string Color,
    string Notes,
    QuoteInput Input);
