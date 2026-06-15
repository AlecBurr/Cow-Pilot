using System.Globalization;
using System.Text.RegularExpressions;

namespace CowPilot;

static class QuoteCalculator
{
    public const double TaxRate = 0.075;
    public const double MilitaryDiscountRate = 0.05;
    public const double Gauge26TrimExtra = 2.0;
    public const double CustomTrimExtraInchRate = 0.50;

    public static readonly string[] BootNames =
    [
        "#1 Boot", "#2 Boot", "#3 Boot", "#4 Boot", "#5 Boot", "#7 Boot", "#8 Boot", "#9 Boot",
        "#801 Zipper Boot", "#802 Zipper Boot", "#803 Zipper Boot",
        "Hi-Temp #3 Boot", "Hi-Temp #5 Boot", "Hi-Temp #8 Boot", "Hi-Temp #9 Boot"
    ];

    public static readonly double[] BootPrices =
    [
        12.00, 13.00, 15.00, 20.00, 25.00, 35.00, 40.00, 85.00,
        30.00, 40.00, 60.00, 35.00, 40.00, 96.50, 220.00
    ];

    private static readonly Regex MeasurementPattern = new(@"^\s*(\d+)\s*(?:x|X|@)\s*(.+?)\s*$", RegexOptions.Compiled);
    private static readonly Regex LengthPattern = new(@"^\s*(?:(\d+)\s*')?\s*(?:(\d+)\s*(?:""|in)?)?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly MetalData[] Metals =
    [
        new(MetalOption.Galv29, "29 Galv", 2.60, 2.0, false, false, 23.00),
        new(MetalOption.Color29, "29 Color", 3.05, 2.0, true, false, 25.00),
        new(MetalOption.Galv26, "26 Galv", 3.10, 2.5, false, true, 23.00),
        new(MetalOption.Color26, "26 Color", 3.55, 2.5, true, true, 25.00)
    ];

    public static QuoteSet Calculate(QuoteInput input)
    {
        var measurements = ParseMeasurements(input.MeasurementsText);
        var grouped = GroupMeasurements(measurements);
        double totalLengthInInches = measurements.Sum(m => m.Quantity * m.LengthInInches);
        double totalLengthInFeet = totalLengthInInches / 12.0;
        double centerOfBalance = CalculateCenterOfBalance(measurements);
        double suggestedScrewBags = CalculateSuggestedScrewBags(totalLengthInFeet);
        double suggestedLapScrewBags = CalculateLapScrewBags(totalLengthInFeet, input.Trim.Ridges);
        double chargedScrewBags = input.UseSuggestedScrews ? suggestedScrewBags : ParseNonNegative(input.ScrewBagsText, "Bags of Screws");
        double chargedLapScrewBags = input.UseSuggestedScrews ? suggestedLapScrewBags : ParseNonNegative(input.LapScrewBagsText, "Bags of Lap");
        double customTrimPrice = CustomTrimPrice(input.CustomTrim);

        if (measurements.Count == 0 && !input.Trim.HasItems && !input.Misc.HasItems && customTrimPrice == 0
            && chargedScrewBags == 0 && chargedLapScrewBags == 0)
        {
            throw new InvalidOperationException("Enter measurements, trim, or misc items before calculating.");
        }

        var quotes = new Dictionary<MetalOption, QuoteResult>();
        foreach (var metal in Metals)
        {
            quotes[metal.Option] = PriceQuote(metal, measurements, input.Trim, input.Misc, customTrimPrice,
                totalLengthInFeet, centerOfBalance, input.Screw, chargedScrewBags, chargedLapScrewBags, input.MilitaryDiscount);
        }

        return new QuoteSet(grouped, input.Trim, input.Misc, input.Screw, totalLengthInFeet, centerOfBalance,
            suggestedScrewBags, suggestedLapScrewBags, chargedScrewBags, chargedLapScrewBags, customTrimPrice,
            input.MilitaryDiscount, quotes);
    }

    public static List<PanelMeasurement> ParseMeasurements(string input)
    {
        var measurements = new List<PanelMeasurement>();
        var lines = Regex.Split(input, "\r\n|\r|\n");
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0) continue;

            var match = MeasurementPattern.Match(line);
            if (!match.Success) throw new InvalidOperationException($"Line {i + 1} is not in quantity x length format: {line}");

            int quantity = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            int lengthInInches = ParseLengthInInches(match.Groups[2].Value);
            if (quantity <= 0 || lengthInInches <= 0) throw new InvalidOperationException($"Line {i + 1} has an invalid quantity or length.");
            measurements.Add(new PanelMeasurement(quantity, lengthInInches));
        }
        return measurements;
    }

    public static int ParseLengthInInches(string rawLength)
    {
        string length = rawLength.Trim();
        if (length.Length == 0) throw new InvalidOperationException("Length is blank.");
        if (Regex.IsMatch(length, @"^\d+$")) return int.Parse(length, CultureInfo.InvariantCulture);

        var match = LengthPattern.Match(length);
        if (!match.Success) throw new InvalidOperationException($"Length is not valid: {rawLength}");
        int feet = match.Groups[1].Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
        int inches = match.Groups[2].Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
        return feet * 12 + inches;
    }

    public static string FormatLength(int lengthInInches)
    {
        int feet = lengthInInches / 12;
        int inches = lengthInInches % 12;
        return inches == 0 ? $"{feet}'" : $"{feet}'{inches}\"";
    }

    public static string MetalLabel(MetalOption option) => Data(option).Label;

    public static string ScrewLabel(ScrewOption option) => option switch
    {
        ScrewOption.OneInch => "1\" Screws",
        ScrewOption.OneAndHalfInch => "1-1/2\" Screws",
        ScrewOption.TwoInch => "2\" Screws",
        ScrewOption.Tubing => "Tubing Screws",
        _ => ""
    };

    public static string ScrewSaveText(ScrewOption option) => option == ScrewOption.Tubing ? "12x1.25\" Screws" : ScrewLabel(option);

    public static double CustomTrimPrice(CustomTrimState state) => state.Pieces
        .Where(piece => piece.Vertices.Count > 1)
        .Sum(piece => CustomTrimUnitPrice(piece) * Math.Max(1, piece.Quantity));

    public static double CustomTrimUnitPrice(CustomTrimPieceState piece)
    {
        double inches = CustomTrimLength(piece);
        return inches <= 0 ? 0 : CustomTrimBasePrice(inches) + (inches * 0.50) + CustomTrimBends(piece);
    }

    public static double CustomTrimLength(CustomTrimPieceState piece)
    {
        double total = 0;
        for (int i = 1; i < piece.Vertices.Count; i++)
        {
            total += Distance(piece.Vertices[i - 1], piece.Vertices[i]);
        }
        return total;
    }

    public static int CustomTrimBends(CustomTrimPieceState piece)
    {
        int bends = 0;
        for (int i = 1; i < piece.Vertices.Count - 1; i++)
        {
            var a = piece.Vertices[i - 1];
            var b = piece.Vertices[i];
            var c = piece.Vertices[i + 1];
            double abx = b.X - a.X;
            double aby = b.Y - a.Y;
            double bcx = c.X - b.X;
            double bcy = c.Y - b.Y;
            double cross = (abx * bcy) - (aby * bcx);
            double dot = (abx * bcx) + (aby * bcy);
            double lengths = Distance(a, b) * Distance(b, c);
            if (lengths > 0 && (Math.Abs(cross / lengths) > 0.01 || dot / lengths < 0.999)) bends++;
        }
        return bends;
    }

    public static double CustomTrimBasePrice(double totalInches) => totalInches <= 20 ? 14.0 : totalInches <= 30 ? 24.0 : 34.0;

    public static string SortedOutputText(SortedDictionary<int, int> grouped)
        => string.Join(Environment.NewLine, grouped.Select(kvp => $"{kvp.Value} @ {kvp.Key}\""));

    public static string QuickbooksText(SortedDictionary<int, int> grouped)
        => string.Join(", ", grouped.Select(kvp => $"{kvp.Value} @ {FormatLength(kvp.Key)}"));

    private static QuoteResult PriceQuote(MetalData metal, List<PanelMeasurement> measurements, TrimSelection trim, MiscSelection misc,
        double customTrimPrice, double totalLengthInFeet, double centerOfBalance, ScrewOption screw, double chargedScrewBags,
        double chargedLapScrewBags, bool militaryDiscount)
    {
        double totalWeight = measurements.Sum(m => m.Quantity * (m.LengthInInches / 12.0) * metal.WeightPerFoot);
        double trimPrice = TrimPrice(trim, metal.UsesGauge26TrimExtra);
        double miscPrice = MiscPrice(misc);
        double screwPrice = chargedScrewBags > 0 ? ScrewPrice(screw, metal.Painted) : 0;
        double lapScrewPrice = chargedLapScrewBags > 0 ? metal.LapScrewPrice : 0;
        double subtotal = miscPrice + trimPrice + customTrimPrice + (totalLengthInFeet * metal.LinearFootPrice)
            + (chargedScrewBags * screwPrice) + (chargedLapScrewBags * lapScrewPrice);
        if (militaryDiscount) subtotal *= 1.0 - MilitaryDiscountRate;
        return new QuoteResult(metal.Option, totalWeight, centerOfBalance, chargedScrewBags, chargedLapScrewBags, subtotal, subtotal * (1.0 + TaxRate));
    }

    private static double TrimPrice(TrimSelection trim, bool gauge26)
    {
        double extra = gauge26 ? Gauge26TrimExtra : 0;
        return trim.Ridges * (25 + extra)
            + trim.Gables * (22 + extra)
            + trim.Eaves * (19 + extra)
            + trim.Endwalls * (19 + extra)
            + trim.Sidewalls * (22 + extra)
            + trim.Valleys * (27 + extra)
            + trim.Transitions * (23 + extra)
            + trim.JTrim * (18 + extra)
            + trim.DeluxeCorners * (25 + extra)
            + trim.TotalExtraInches * CustomTrimExtraInchRate;
    }

    private static double MiscPrice(MiscSelection misc)
    {
        double boots = 0;
        for (int i = 0; i < BootNames.Length; i++) boots += misc.BootCount(i) * BootPrices[i];
        return misc.OutsideClosures * 6.50
            + misc.InsideClosures * 6.50
            + misc.ButylTape * 6.00
            + misc.Caulk * 10.50
            + misc.VentedClosures * 5.25
            + misc.UniversalClosures * 22.50
            + boots;
    }

    private static SortedDictionary<int, int> GroupMeasurements(IEnumerable<PanelMeasurement> measurements)
    {
        var grouped = new SortedDictionary<int, int>(Comparer<int>.Create((left, right) => right.CompareTo(left)));
        foreach (var measurement in measurements)
        {
            grouped.TryGetValue(measurement.LengthInInches, out int current);
            grouped[measurement.LengthInInches] = current + measurement.Quantity;
        }
        return grouped;
    }

    private static double CalculateCenterOfBalance(IEnumerable<PanelMeasurement> measurements)
    {
        double weightedMidpoints = 0;
        double totalLinearWeight = 0;
        foreach (var measurement in measurements)
        {
            double linearWeight = measurement.Quantity * measurement.LengthInInches;
            weightedMidpoints += linearWeight * (measurement.LengthInInches / 2.0);
            totalLinearWeight += linearWeight;
        }
        return totalLinearWeight == 0 ? 0 : weightedMidpoints / totalLinearWeight;
    }

    private static double CalculateSuggestedScrewBags(double totalLengthInFeet) => totalLengthInFeet <= 0 ? 0 : Math.Ceiling((totalLengthInFeet * 3) / 250.0);

    private static double CalculateLapScrewBags(double totalLengthInFeet, int ridgeCount)
    {
        double panelLapScrews = totalLengthInFeet / 2.0;
        double ridgeLapScrews = ((ridgeCount * 10.0 * 12.0) / 9.0) * 2.0;
        double totalLapScrews = panelLapScrews + ridgeLapScrews;
        return totalLapScrews <= 0 ? 0 : Math.Ceiling(totalLapScrews / 250.0);
    }

    private static double ParseNonNegative(string text, string label)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || value < 0)
        {
            throw new InvalidOperationException($"{label} must be a non-negative number.");
        }
        return value;
    }

    private static double ScrewPrice(ScrewOption option, bool painted) => option switch
    {
        ScrewOption.OneInch => painted ? 21.0 : 19.0,
        ScrewOption.OneAndHalfInch => painted ? 22.50 : 21.0,
        ScrewOption.TwoInch => painted ? 24.0 : 23.0,
        ScrewOption.Tubing => painted ? 30.0 : 27.0,
        _ => 0
    };

    private static MetalData Data(MetalOption option) => Metals.First(m => m.Option == option);

    private static double Distance(PointF a, PointF b) => Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));

    private sealed record MetalData(MetalOption Option, string Label, double LinearFootPrice, double WeightPerFoot, bool Painted, bool UsesGauge26TrimExtra, double LapScrewPrice);
}
