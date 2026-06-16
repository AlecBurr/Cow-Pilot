using System.Globalization;
using System.Text.RegularExpressions;

namespace CowPilot;

static class QuoteCalculator
{
    public static readonly BootCatalogItem[] BootCatalog =
    [
        new("#1 Boot", 12.00),
        new("#2 Boot", 13.00),
        new("#3 Boot", 15.00),
        new("#4 Boot", 20.00),
        new("#5 Boot", 25.00),
        new("#7 Boot", 35.00),
        new("#8 Boot", 40.00),
        new("#9 Boot", 85.00),
        new("#801 Zipper Boot", 30.00),
        new("#802 Zipper Boot", 40.00),
        new("#803 Zipper Boot", 60.00),
        new("Hi-Temp #3 Boot", 35.00),
        new("Hi-Temp #5 Boot", 40.00),
        new("Hi-Temp #8 Boot", 96.50),
        new("Hi-Temp #9 Boot", 220.00)
    ];

    private static readonly Regex MeasurementPattern = new(@"^\s*(\d+)\s*(?:x|X|@)\s*(.+?)\s*$", RegexOptions.Compiled);
    private static readonly Regex LengthPattern = new(@"^\s*(?:(\d+)\s*')?\s*(?:(\d+)\s*(?:""|in)?)?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly MetalData[] Metals =
    [
        new(MetalOption.Galv29, "29 Galv", false, false),
        new(MetalOption.Color29, "29 Color", true, false),
        new(MetalOption.Galv26, "26 Galv", false, true),
        new(MetalOption.Color26, "26 Color", true, true)
    ];

    public static QuoteSet Calculate(QuoteInput input, PriceSettings? priceSettings = null)
    {
        PriceSettings prices = Prices(priceSettings);
        var measurements = ParseMeasurements(input.MeasurementsText);
        var grouped = GroupMeasurements(measurements);
        double totalLengthInInches = measurements.Sum(m => m.Quantity * m.LengthInInches);
        double totalLengthInFeet = totalLengthInInches / 12.0;
        double centerOfBalance = CalculateCenterOfBalance(measurements);
        double suggestedScrewBags = CalculateSuggestedScrewBags(totalLengthInFeet, prices);
        double suggestedLapScrewBags = CalculateLapScrewBags(totalLengthInFeet, input.Trim.Ridges, prices);
        double chargedScrewBags = input.UseSuggestedScrews ? suggestedScrewBags : ParseNonNegative(input.ScrewBagsText, "Bags of Screws");
        double chargedLapScrewBags = input.UseSuggestedScrews ? suggestedLapScrewBags : ParseNonNegative(input.LapScrewBagsText, "Bags of Lap");
        double customTrimPrice = CustomTrimPrice(input.CustomTrim, prices);

        if (measurements.Count == 0 && !input.Trim.HasItems && !input.Misc.HasItems && customTrimPrice == 0
            && chargedScrewBags == 0 && chargedLapScrewBags == 0)
        {
            throw new InvalidOperationException("Enter measurements, trim, or misc items before calculating.");
        }

        var quotes = new Dictionary<MetalOption, QuoteResult>();
        foreach (var metal in Metals)
        {
            quotes[metal.Option] = PriceQuote(metal, measurements, input.Trim, input.Misc, customTrimPrice,
                totalLengthInFeet, centerOfBalance, input.Screw, chargedScrewBags, chargedLapScrewBags, input.MilitaryDiscount, prices);
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

    public static double CustomTrimPrice(CustomTrimState state, PriceSettings? priceSettings = null)
    {
        PriceSettings prices = Prices(priceSettings);
        return state.Pieces
        .Where(piece => piece.Vertices.Count > 1)
        .Sum(piece => CustomTrimUnitPrice(piece, prices) * Math.Max(1, piece.Quantity));
    }

    public static double CustomTrimUnitPrice(CustomTrimPieceState piece, PriceSettings? priceSettings = null)
    {
        PriceSettings prices = Prices(priceSettings);
        double inches = CustomTrimLength(piece);
        return inches <= 0 ? 0 : CustomTrimBasePrice(inches, prices) + (inches * prices.CustomTrimInchRate) + (CustomTrimBends(piece) * prices.CustomTrimBendRate);
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

    public static double CustomTrimBasePrice(double totalInches, PriceSettings? priceSettings = null)
    {
        PriceSettings prices = Prices(priceSettings);
        return totalInches <= 20 ? prices.CustomTrimBaseUnder20 : totalInches <= 30 ? prices.CustomTrimBaseUnder30 : prices.CustomTrimBaseOver30;
    }

    public static string SortedOutputText(SortedDictionary<int, int> grouped)
        => string.Join(Environment.NewLine, grouped.Select(kvp => $"{kvp.Value} @ {kvp.Key}\""));

    public static string QuickbooksText(SortedDictionary<int, int> grouped)
        => string.Join(", ", grouped.Select(kvp => $"{kvp.Value} @ {FormatLength(kvp.Key)}"));

    private static QuoteResult PriceQuote(MetalData metal, List<PanelMeasurement> measurements, TrimSelection trim, MiscSelection misc,
        double customTrimPrice, double totalLengthInFeet, double centerOfBalance, ScrewOption screw, double chargedScrewBags,
        double chargedLapScrewBags, bool militaryDiscount, PriceSettings prices)
    {
        MetalPriceSetting metalPrice = prices.Metal(metal.Option);
        double totalWeight = measurements.Sum(m => m.Quantity * (m.LengthInInches / 12.0) * metalPrice.WeightPerFoot);
        double trimPrice = TrimPrice(trim, metal.UsesGauge26TrimExtra, prices);
        double miscPrice = MiscPrice(misc, prices);
        double screwPrice = chargedScrewBags > 0 ? ScrewPrice(screw, metal.Painted, prices) : 0;
        double lapScrewPrice = chargedLapScrewBags > 0 ? metalPrice.LapScrewBagPrice : 0;
        double subtotal = miscPrice + trimPrice + customTrimPrice + (totalLengthInFeet * metalPrice.LinearFootPrice)
            + (chargedScrewBags * screwPrice) + (chargedLapScrewBags * lapScrewPrice);
        if (militaryDiscount) subtotal *= 1.0 - prices.MilitaryDiscountRate;
        return new QuoteResult(metal.Option, totalWeight, centerOfBalance, chargedScrewBags, chargedLapScrewBags, subtotal, subtotal * (1.0 + prices.TaxRate));
    }

    private static double TrimPrice(TrimSelection trim, bool gauge26, PriceSettings prices)
    {
        double extra = gauge26 ? prices.Gauge26TrimExtra : 0;
        return trim.Ridges * (prices.Trim("Ridges").Price + extra)
            + trim.Gables * (prices.Trim("Gables").Price + extra)
            + trim.Eaves * (prices.Trim("Eaves").Price + extra)
            + trim.Endwalls * (prices.Trim("Endwalls").Price + extra)
            + trim.Sidewalls * (prices.Trim("Sidewalls").Price + extra)
            + trim.Valleys * (prices.Trim("Valleys").Price + extra)
            + trim.Transitions * (prices.Trim("Transitions").Price + extra)
            + trim.JTrim * (prices.Trim("J-Trim").Price + extra)
            + trim.DeluxeCorners * (prices.Trim("Deluxe Corners").Price + extra)
            + trim.TotalExtraInches * prices.StandardTrimExtraInchRate;
    }

    private static double MiscPrice(MiscSelection misc, PriceSettings prices)
    {
        double boots = 0;
        for (int i = 0; i < BootCatalog.Length; i++) boots += misc.BootCount(i) * prices.Boot(i).Price;
        return misc.OutsideClosures * prices.MiscItem("OutsideClosures").Price
            + misc.InsideClosures * prices.MiscItem("InsideClosures").Price
            + misc.ButylTape * prices.MiscItem("ButylTape").Price
            + misc.Caulk * prices.MiscItem("Caulk").Price
            + misc.VentedClosures * prices.MiscItem("VentedClosures").Price
            + misc.UniversalClosures * prices.MiscItem("UniversalClosures").Price
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

    private static double CalculateSuggestedScrewBags(double totalLengthInFeet, PriceSettings prices) => totalLengthInFeet <= 0 ? 0 : Math.Ceiling(totalLengthInFeet / prices.ScrewCoverageLinearFeetPerBag);

    private static double CalculateLapScrewBags(double totalLengthInFeet, int ridgeCount, PriceSettings prices)
    {
        double panelLapScrews = totalLengthInFeet / 2.0;
        double ridgeLapScrews = ((ridgeCount * 10.0 * 12.0) / 9.0) * 2.0;
        double totalLapScrews = panelLapScrews + ridgeLapScrews;
        return totalLapScrews <= 0 ? 0 : Math.Ceiling(totalLapScrews / prices.LapScrewsPerBag);
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

    private static double ScrewPrice(ScrewOption option, bool painted, PriceSettings prices)
    {
        ScrewPriceSetting screw = prices.Screw(option);
        return painted ? screw.PaintedBagPrice : screw.UnpaintedBagPrice;
    }

    private static PriceSettings Prices(PriceSettings? priceSettings)
    {
        priceSettings ??= new PriceSettings();
        priceSettings.Normalize();
        return priceSettings;
    }

    private static MetalData Data(MetalOption option) => Metals.First(m => m.Option == option);

    private static double Distance(PointF a, PointF b) => Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));

    private sealed record MetalData(MetalOption Option, string Label, bool Painted, bool UsesGauge26TrimExtra);
}

sealed record BootCatalogItem(string Name, double Price);
