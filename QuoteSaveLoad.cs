using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CowPilot;

static class QuoteSaveLoad
{
    private const string BlockStart = "--- Cow Pilot Load Data ---";
    private const string BlockEnd = "--- End Cow Pilot Load Data ---";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string CreateEstimateText(QuoteDocument document, QuoteSet quote, PriceSettings? priceSettings = null)
    {
        PriceSettings prices = priceSettings ?? new PriceSettings();
        prices.Normalize();
        var sb = new StringBuilder();
        string nl = "\r\n";
        sb.Append("Cow Pilot Estimate").Append(nl);
        sb.Append("Version: ").Append(AppVersion.Version).Append(nl);
        sb.Append("Estimate saved: ").Append(document.SavedAt).Append(nl);
        sb.Append("Customer Name: ").Append(document.CustomerName).Append(nl);
        sb.Append("Phone: ").Append(document.Phone).Append(nl);
        sb.Append("Color: ").Append(document.Color).Append(nl);
        sb.Append("Notes: ").Append(document.Notes).Append(nl).Append(nl);

        sb.Append("Price Summary:").Append(nl);
        foreach (var result in quote.Quotes.Values)
        {
            sb.Append(QuoteCalculator.MetalLabel(result.Metal))
                .Append(" - Subtotal: ").Append(Money(result.Subtotal))
                .Append(" | Grand Total: ").Append(Money(result.GrandTotal))
                .Append(nl);
        }
        if (quote.MilitaryDiscountApplied) sb.Append("Military Discount: ").Append(Num(prices.MilitaryDiscountRate * 100)).Append("%").Append(nl);
        sb.Append(nl);

        AppendTrim(sb, quote.Trim, nl, prices);
        AppendCustomTrim(sb, document.Input.CustomTrim, nl, prices);
        AppendScrews(sb, quote, nl);
        AppendMisc(sb, quote.Misc, nl);

        if (quote.GroupedPanels.Count > 0)
        {
            sb.Append("LF: ").Append(Num(quote.TotalLengthInFeet)).Append(nl).Append(nl);
            sb.Append("Panels:").Append(nl).Append(QuoteCalculator.SortedOutputText(quote.GroupedPanels)).Append(nl);
            sb.Append("Quickbooks Format:").Append(nl).Append(QuoteCalculator.QuickbooksText(quote.GroupedPanels)).Append(nl);
            sb.Append("Center of Balance: ").Append(Num(quote.CenterOfBalance)).Append("\"").Append(nl).Append(nl);
        }

        sb.Append(nl).Append(BlockStart).Append(nl);
        sb.Append(JsonSerializer.Serialize(document, JsonOptions)).Append(nl);
        sb.Append(BlockEnd);
        return sb.ToString();
    }

    public static QuoteDocument Load(string content)
    {
        int start = content.IndexOf(BlockStart, StringComparison.Ordinal);
        int end = content.IndexOf(BlockEnd, StringComparison.Ordinal);
        if (start < 0 || end <= start) throw new InvalidOperationException("This file does not contain Cow Pilot load data.");
        string json = content[(start + BlockStart.Length)..end].Trim();
        var document = JsonSerializer.Deserialize<QuoteDocument>(json, JsonOptions)
            ?? throw new InvalidOperationException("Cow Pilot load data is blank.");
        if (document.FormatVersion != AppVersion.SaveFormatVersion)
        {
            throw new InvalidOperationException($"This file uses load format version {document.FormatVersion}. Cow Pilot expects {AppVersion.SaveFormatVersion}.");
        }
        return document;
    }

    private static void AppendTrim(StringBuilder sb, TrimSelection trim, string nl, PriceSettings prices)
    {
        if (!trim.HasItems) return;
        sb.Append("Trim:").Append(nl);
        AppendTrimLine(sb, trim.Eaves, trim.EavesExtraInches, "Eaves", nl, prices);
        AppendTrimLine(sb, trim.Ridges, trim.RidgesExtraInches, "Ridges", nl, prices);
        AppendTrimLine(sb, trim.Gables, trim.GablesExtraInches, "Gables", nl, prices);
        AppendTrimLine(sb, trim.Valleys, trim.ValleysExtraInches, "Valleys", nl, prices);
        AppendTrimLine(sb, trim.Endwalls, trim.EndwallsExtraInches, "Endwalls", nl, prices);
        AppendTrimLine(sb, trim.Sidewalls, trim.SidewallsExtraInches, "Sidewalls", nl, prices);
        AppendTrimLine(sb, trim.Transitions, trim.TransitionsExtraInches, "Transitions", nl, prices);
        AppendTrimLine(sb, trim.JTrim, trim.JTrimExtraInches, "J-Trim", nl, prices);
        AppendTrimLine(sb, trim.DeluxeCorners, trim.DeluxeCornersExtraInches, "Deluxe Corners", nl, prices);
        if (trim.TotalExtraInches > 0)
        {
            sb.Append("Extra Trim Inches Cost: ").Append(Money(trim.TotalExtraInches * prices.StandardTrimExtraInchRate))
                .Append(" (").Append(Num(trim.TotalExtraInches)).Append("\" @ ").Append(Money(prices.StandardTrimExtraInchRate)).Append("/in)").Append(nl);
        }
        sb.Append(nl);
    }

    private static void AppendCustomTrim(StringBuilder sb, CustomTrimState customTrim, string nl, PriceSettings prices)
    {
        var pieces = customTrim.Pieces.Where(p => p.Vertices.Count > 1).ToList();
        if (pieces.Count == 0) return;
        sb.Append("Custom Trim Designs:").Append(nl);
        for (int i = 0; i < pieces.Count; i++)
        {
            var piece = pieces[i];
            double length = QuoteCalculator.CustomTrimLength(piece);
            sb.Append("Piece ").Append(i + 1).Append(":").Append(nl);
            sb.Append("Quantity: ").Append(piece.Quantity).Append(nl);
            sb.Append("Total Inches Each: ").Append(Num(length)).Append("\"").Append(nl);
            sb.Append("Bends Each: ").Append(QuoteCalculator.CustomTrimBends(piece)).Append(nl);
            double unitPrice = QuoteCalculator.CustomTrimUnitPrice(piece, prices);
            sb.Append("Unit Price: ").Append(Money(unitPrice)).Append(nl);
            sb.Append("Extended Price: ").Append(Money(unitPrice * piece.Quantity)).Append(nl).Append(nl);
        }
        sb.Append("Total Custom Trim Price: ").Append(Money(QuoteCalculator.CustomTrimPrice(customTrim, prices))).Append(nl).Append(nl);
    }

    private static void AppendScrews(StringBuilder sb, QuoteSet quote, string nl)
    {
        if (quote.SuggestedScrewBags <= 0 && quote.SuggestedLapScrewBags <= 0 && quote.ChargedScrewBags <= 0 && quote.ChargedLapScrewBags <= 0) return;
        sb.Append("Screws:").Append(nl);
        sb.Append("Screw Size: ").Append(QuoteCalculator.ScrewSaveText(quote.Screw)).Append(nl);
        sb.Append("Suggested Screw Bags: ").Append(Num(quote.SuggestedScrewBags)).Append(nl);
        sb.Append("Suggested Lap Screw Bags: ").Append(Num(quote.SuggestedLapScrewBags)).Append(nl);
        sb.Append("Charged Screw Bags: ").Append(Num(quote.ChargedScrewBags)).Append(nl);
        sb.Append("Charged Lap Screw Bags: ").Append(Num(quote.ChargedLapScrewBags)).Append(nl).Append(nl);
    }

    private static void AppendMisc(StringBuilder sb, MiscSelection misc, string nl)
    {
        if (!misc.HasItems) return;
        sb.Append("Misc:").Append(nl);
        AppendLine(sb, misc.OutsideClosures, "Outside Closures (Pack of 4)", nl);
        AppendLine(sb, misc.InsideClosures, "Inside Closures (Pack of 4)", nl);
        AppendLine(sb, misc.ButylTape, "Butyl Tape (45')", nl);
        AppendLine(sb, misc.Caulk, "Ultra 1000 Caulk", nl);
        AppendLine(sb, misc.VentedClosures, "Vented Closures (Individual)", nl);
        AppendLine(sb, misc.UniversalClosures, "Universal Expanding Closures 20'", nl);
        for (int i = 0; i < QuoteCalculator.BootNames.Length; i++) AppendLine(sb, misc.BootCount(i), QuoteCalculator.BootNames[i], nl);
        sb.Append(nl);
    }

    private static void AppendTrimLine(StringBuilder sb, int count, double extraInches, string label, string nl, PriceSettings prices)
    {
        if (count <= 0 && extraInches <= 0) return;
        sb.Append(label).Append(": ");
        if (count > 0) sb.Append(count).Append(count == 1 ? " piece" : " pieces");
        if (extraInches > 0)
        {
            if (count > 0) sb.Append(", ");
            sb.Append(Num(extraInches)).Append("\" extra @ ").Append(Money(prices.StandardTrimExtraInchRate)).Append("/in");
        }
        sb.Append(nl);
    }

    private static void AppendLine(StringBuilder sb, int count, string label, string nl)
    {
        if (count > 0) sb.Append(count).Append(' ').Append(label).Append(nl);
    }

    private static string Money(double value) => value.ToString("C", CultureInfo.GetCultureInfo("en-US"));
    private static string Num(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
