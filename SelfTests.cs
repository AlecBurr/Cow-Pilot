namespace CowPilot;

static class SelfTests
{
    public static void Run()
    {
        Assert(QuoteCalculator.ParseLengthInInches("10'") == 120, "10 feet parse");
        Assert(QuoteCalculator.ParseLengthInInches("12'6\"") == 150, "feet and inches parse");
        Assert(QuoteCalculator.ParseLengthInInches("144") == 144, "raw inches parse");

        var misc = MiscSelection.Empty with { RedSnips = 1, TurboShear = 1, BootCounts = NewBootCounts((0, 2), (1, 3)) };
        var input = new QuoteInput(
            "2x10'\r\n1x12'6\"",
            ScrewOption.OneInch,
            true,
            "",
            "",
            false,
            TrimSelection.Empty with { Ridges = 1, DeluxeCorners = 1, Eaves = 1, EavesExtraInches = 6 },
            misc,
            new CustomTrimState([new CustomTrimPieceState(2, [new PointF(0, 0), new PointF(3, 0), new PointF(3, 3)])], 64, 0, 0, 1f, 1));

        var quote = QuoteCalculator.Calculate(input);
        Assert(quote.GroupedPanels[150] == 1 && quote.GroupedPanels[120] == 2, "grouped panels");
        Assert(quote.Quotes[MetalOption.Galv29].GrandTotal > 0, "quote total");
        Assert(quote.CustomTrimPrice > 0, "custom trim price");
        Assert(quote.Quotes[MetalOption.Galv29].TotalWeight > 0, "quote weight");

        var emptyQuote = QuoteCalculator.Calculate(new QuoteInput("", ScrewOption.OneInch, false, "0", "0", false,
            TrimSelection.Empty, MiscSelection.Empty, CustomTrimState.Empty));
        Assert(emptyQuote.GroupedPanels.Count == 0, "empty quote panels");
        Assert(emptyQuote.Quotes.Values.All(result => result.Subtotal == 0 && result.GrandTotal == 0), "empty quote zero totals");

        var prices = new PriceSettings();
        prices.Normalize();
        prices.Metal(MetalOption.Galv29).LinearFootPrice = 100;
        prices.StandardTrimExtraInchRate = 2;
        prices.CustomTrimBendRate = 10;
        prices.TaxRate = 0;
        var customPriceQuote = QuoteCalculator.Calculate(input, prices);
        Assert(customPriceQuote.Quotes[MetalOption.Galv29].Subtotal > quote.Quotes[MetalOption.Galv29].Subtotal, "custom metal price applied");
        Assert(QuoteCalculator.CustomTrimUnitPrice(input.CustomTrim.Pieces[0], prices) > QuoteCalculator.CustomTrimUnitPrice(input.CustomTrim.Pieces[0]), "custom trim bend price applied");

        var document = new QuoteDocument(AppVersion.SaveFormatVersion, AppVersion.Version, "06/15/2026 12:00:00",
            "Test Customer", "555-0100", "Red", "Round trip", input);
        var text = QuoteSaveLoad.CreateEstimateText(document, customPriceQuote, prices);
        Assert(text.Contains("$2.00/in", StringComparison.Ordinal), "custom extra-inch price saved");
        Assert(text.Contains("2 #1 Boot", StringComparison.Ordinal) && text.Contains("3 #2 Boot", StringComparison.Ordinal), "multiple boot quantities saved");
        Assert(text.Contains("1 Red Snips", StringComparison.Ordinal) && text.Contains("1 Turbo Shear", StringComparison.Ordinal), "new extras saved");
        var loaded = QuoteSaveLoad.Load(text);
        Assert(loaded.FormatVersion == AppVersion.SaveFormatVersion, "load format version");
        Assert(loaded.Input.CustomTrim.Pieces[0].Quantity == 2, "custom trim quantity load");
        Assert(loaded.Input.CustomTrim.Pieces[0].Vertices.Count == 3, "custom trim vertices load");

        string releaseJson = """{"version":"v9.8.7","url":"https://github.com/AlecBurr/Cow-Pilot/releases/download/v9.8.7/CowPilot-9.8.7-win-x64.zip"}""";
        Assert(UpdateChecker.TryReadLatestManifest(releaseJson, out string latest, out string? downloadUrl), "update manifest parse");
        Assert(latest == "9.8.7" && downloadUrl!.EndsWith("CowPilot-9.8.7-win-x64.zip", StringComparison.Ordinal), "update manifest values");
        string encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(releaseJson));
        Assert(UpdateChecker.TryReadLatestManifestFromGitHubContent($$"""{"content":"{{encoded}}"}""", out latest, out _), "github content manifest parse");
        Assert(UpdateChecker.IsNewerVersion("v9.8.7", AppVersion.Version), "newer release compare");
        Assert(!UpdateChecker.IsNewerVersion(AppVersion.Version, AppVersion.Version), "same release compare");
        Console.WriteLine("Cow Pilot self-tests passed.");
    }

    private static int[] NewBootCounts(params (int Index, int Count)[] values)
    {
        var counts = new int[QuoteCalculator.BootCatalog.Length];
        foreach (var value in values) counts[value.Index] = value.Count;
        return counts;
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException($"Self-test failed: {name}");
    }
}
