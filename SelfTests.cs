namespace CowPilot;

static class SelfTests
{
    public static void Run()
    {
        Assert(QuoteCalculator.ParseLengthInInches("10'") == 120, "10 feet parse");
        Assert(QuoteCalculator.ParseLengthInInches("12'6\"") == 150, "feet and inches parse");
        Assert(QuoteCalculator.ParseLengthInInches("144") == 144, "raw inches parse");

        var misc = MiscSelection.Empty with { BootCounts = NewBootCounts((0, 2)) };
        var input = new QuoteInput(
            "2x10'\r\n1x12'6\"",
            ScrewOption.OneInch,
            true,
            "",
            "",
            false,
            TrimSelection.Empty with { Ridges = 1, EavesExtraInches = 6 },
            misc,
            new CustomTrimState([new CustomTrimPieceState(2, [new PointF(0, 0), new PointF(3, 0), new PointF(3, 3)])], 64, 0, 0, 0.125f, 1));

        var quote = QuoteCalculator.Calculate(input);
        Assert(quote.GroupedPanels[150] == 1 && quote.GroupedPanels[120] == 2, "grouped panels");
        Assert(quote.Quotes[MetalOption.Galv29].GrandTotal > 0, "quote total");
        Assert(quote.CustomTrimPrice > 0, "custom trim price");

        var document = new QuoteDocument(AppVersion.SaveFormatVersion, AppVersion.Version, "06/15/2026 12:00:00",
            "Test Customer", "555-0100", "Red", "Round trip", input);
        var text = QuoteSaveLoad.CreateEstimateText(document, quote);
        var loaded = QuoteSaveLoad.Load(text);
        Assert(loaded.FormatVersion == AppVersion.SaveFormatVersion, "load format version");
        Assert(loaded.Input.CustomTrim.Pieces[0].Quantity == 2, "custom trim quantity load");
        Assert(loaded.Input.CustomTrim.Pieces[0].Vertices.Count == 3, "custom trim vertices load");
        Console.WriteLine("Cow Pilot self-tests passed.");
    }

    private static int[] NewBootCounts(params (int Index, int Count)[] values)
    {
        var counts = new int[QuoteCalculator.BootNames.Length];
        foreach (var value in values) counts[value.Index] = value.Count;
        return counts;
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException($"Self-test failed: {name}");
    }
}
