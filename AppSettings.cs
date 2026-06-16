using System.Drawing;

namespace CowPilot;

sealed class AppSettings
{
    public GeneralSettings General { get; set; } = new();
    public PriceSettings Prices { get; set; } = new();
    public PreferenceSettings Preferences { get; set; } = new();

    public void Normalize()
    {
        General ??= new GeneralSettings();
        Prices ??= new PriceSettings();
        Preferences ??= new PreferenceSettings();
        General.Normalize();
        Prices.Normalize();
        Preferences.Normalize();
    }
}

sealed class GeneralSettings
{
    public string QuickSaveFolder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Cow Pilot Estimates");
    public int AutoRecalculateDelayMs { get; set; } = 650;

    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(QuickSaveFolder))
        {
            QuickSaveFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Cow Pilot Estimates");
        }
        AutoRecalculateDelayMs = Math.Clamp(AutoRecalculateDelayMs, 150, 5000);
    }
}

sealed class PriceSettings
{
    private static readonly MetalPriceSetting[] MetalDefaults =
    [
        new() { Key = nameof(MetalOption.Galv29), Label = "29 Galv", LinearFootPrice = 2.60, WeightPerFoot = 2.0, LapScrewBagPrice = 23.00 },
        new() { Key = nameof(MetalOption.Color29), Label = "29 Color", LinearFootPrice = 3.05, WeightPerFoot = 2.0, LapScrewBagPrice = 25.00 },
        new() { Key = nameof(MetalOption.Galv26), Label = "26 Galv", LinearFootPrice = 3.10, WeightPerFoot = 2.5, LapScrewBagPrice = 23.00 },
        new() { Key = nameof(MetalOption.Color26), Label = "26 Color", LinearFootPrice = 3.55, WeightPerFoot = 2.5, LapScrewBagPrice = 25.00 }
    ];
    private static readonly NamedPriceSetting[] TrimDefaults =
    [
        new() { Key = "Ridges", Label = "Ridges", Price = 25.00 },
        new() { Key = "Gables", Label = "Gables", Price = 22.00 },
        new() { Key = "Eaves", Label = "Eaves", Price = 19.00 },
        new() { Key = "Endwalls", Label = "Endwalls", Price = 19.00 },
        new() { Key = "Sidewalls", Label = "Sidewalls", Price = 22.00 },
        new() { Key = "Valleys", Label = "Valleys", Price = 27.00 },
        new() { Key = "Transitions", Label = "Transitions", Price = 23.00 },
        new() { Key = "J-Trim", Label = "J-Trim", Price = 18.00 },
        new() { Key = "Deluxe Corners", Label = "Deluxe Corners", Price = 25.00 }
    ];
    private static readonly NamedPriceSetting[] MiscDefaults =
    [
        new() { Key = "OutsideClosures", Label = "Outside Closures (4)", Price = 6.50 },
        new() { Key = "InsideClosures", Label = "Inside Closures (4)", Price = 6.50 },
        new() { Key = "ButylTape", Label = "Butyl Tape (45')", Price = 6.00 },
        new() { Key = "Caulk", Label = "Caulk", Price = 10.50 },
        new() { Key = "VentedClosures", Label = "Vented Closures (1)", Price = 5.25 },
        new() { Key = "UniversalClosures", Label = "Universal Closures (20')", Price = 22.50 },
        new() { Key = "RedSnips", Label = "Red Snips", Price = 34.00 },
        new() { Key = "GreenSnips", Label = "Green Snips", Price = 34.00 },
        new() { Key = "BlueSnips", Label = "Blue Snips", Price = 38.00 },
        new() { Key = "TurboShear", Label = "Turbo Shear", Price = 150.00 }
    ];
    private static readonly NamedPriceSetting[] BootDefaults = QuoteCalculator.BootCatalog
        .Select(boot => new NamedPriceSetting { Key = boot.Name, Label = boot.Name, Price = boot.Price })
        .ToArray();
    private static readonly ScrewPriceSetting[] ScrewDefaults =
    [
        new() { Key = nameof(ScrewOption.OneInch), Label = "1\" Screws", UnpaintedBagPrice = 19.00, PaintedBagPrice = 21.00 },
        new() { Key = nameof(ScrewOption.OneAndHalfInch), Label = "1-1/2\" Screws", UnpaintedBagPrice = 21.00, PaintedBagPrice = 22.50 },
        new() { Key = nameof(ScrewOption.TwoInch), Label = "2\" Screws", UnpaintedBagPrice = 23.00, PaintedBagPrice = 24.00 },
        new() { Key = nameof(ScrewOption.Tubing), Label = "Tubing Screws", UnpaintedBagPrice = 27.00, PaintedBagPrice = 30.00 }
    ];

    public double TaxRate { get; set; } = 0.075;
    public double MilitaryDiscountRate { get; set; } = 0.05;
    public double Gauge26TrimExtra { get; set; } = 2.00;
    public double StandardTrimExtraInchRate { get; set; } = 0.50;
    public double CustomTrimBaseUnder20 { get; set; } = 14.00;
    public double CustomTrimBaseUnder30 { get; set; } = 24.00;
    public double CustomTrimBaseOver30 { get; set; } = 34.00;
    public double CustomTrimInchRate { get; set; } = 0.50;
    public double CustomTrimBendRate { get; set; } = 1.00;
    public double CustomTrimMaxInches { get; set; } = 41.00;
    public double ScrewCoverageLinearFeetPerBag { get; set; } = 250.00 / 3.00;
    public double LapScrewsPerBag { get; set; } = 250.00;

    public List<MetalPriceSetting> Metals { get; set; } = DefaultMetals();
    public List<NamedPriceSetting> StandardTrim { get; set; } = DefaultStandardTrim();
    public List<NamedPriceSetting> Misc { get; set; } = DefaultMisc();
    public List<NamedPriceSetting> Boots { get; set; } = DefaultBoots();
    public List<ScrewPriceSetting> Screws { get; set; } = DefaultScrews();

    public void Normalize()
    {
        TaxRate = ClampNonNegative(TaxRate);
        MilitaryDiscountRate = Math.Clamp(MilitaryDiscountRate, 0, 1);
        Gauge26TrimExtra = ClampNonNegative(Gauge26TrimExtra);
        StandardTrimExtraInchRate = ClampNonNegative(StandardTrimExtraInchRate);
        CustomTrimBaseUnder20 = ClampNonNegative(CustomTrimBaseUnder20);
        CustomTrimBaseUnder30 = ClampNonNegative(CustomTrimBaseUnder30);
        CustomTrimBaseOver30 = ClampNonNegative(CustomTrimBaseOver30);
        CustomTrimInchRate = ClampNonNegative(CustomTrimInchRate);
        CustomTrimBendRate = ClampNonNegative(CustomTrimBendRate);
        CustomTrimMaxInches = Math.Max(1, CustomTrimMaxInches);
        ScrewCoverageLinearFeetPerBag = Math.Max(1, ScrewCoverageLinearFeetPerBag);
        LapScrewsPerBag = Math.Max(1, LapScrewsPerBag);
        Metals = Merge(Metals, DefaultMetals(), item => item.Key);
        StandardTrim = Merge(StandardTrim, DefaultStandardTrim(), item => item.Key);
        Misc = Merge(Misc, DefaultMisc(), item => item.Key);
        Boots = Merge(Boots, DefaultBoots(), item => item.Key);
        Screws = Merge(Screws, DefaultScrews(), item => item.Key);
    }

    public MetalPriceSetting Metal(MetalOption option) => Find(Metals, MetalDefaults, option.ToString());
    public NamedPriceSetting Trim(string key) => Find(StandardTrim, TrimDefaults, key);
    public NamedPriceSetting MiscItem(string key) => Find(Misc, MiscDefaults, key);
    public NamedPriceSetting Boot(int index) => Find(Boots, BootDefaults, index >= 0 && index < QuoteCalculator.BootCatalog.Length ? QuoteCalculator.BootCatalog[index].Name : "");
    public ScrewPriceSetting Screw(ScrewOption option) => Find(Screws, ScrewDefaults, option.ToString());

    public static List<MetalPriceSetting> DefaultMetals() => MetalDefaults.Select(Clone).ToList();
    public static List<NamedPriceSetting> DefaultStandardTrim() => TrimDefaults.Select(Clone).ToList();
    public static List<NamedPriceSetting> DefaultMisc() => MiscDefaults.Select(Clone).ToList();
    public static List<NamedPriceSetting> DefaultBoots() => BootDefaults.Select(Clone).ToList();
    public static List<ScrewPriceSetting> DefaultScrews() => ScrewDefaults.Select(Clone).ToList();

    private static T Find<T>(IEnumerable<T>? current, IEnumerable<T> defaults, string key) where T : IKeyedSetting
    {
        return (current ?? []).FirstOrDefault(item => item.Key == key) ?? defaults.First(item => item.Key == key);
    }

    private static List<T> Merge<T>(IEnumerable<T>? current, IEnumerable<T> defaults, Func<T, string> keySelector) where T : IKeyedSetting
    {
        var byKey = (current ?? []).Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .GroupBy(keySelector)
            .ToDictionary(group => group.Key, group => group.First());
        foreach (var item in defaults)
        {
            if (!byKey.ContainsKey(item.Key)) byKey[item.Key] = item;
        }
        return defaults.Select(item => byKey[item.Key]).ToList();
    }

    private static double ClampNonNegative(double value) => double.IsFinite(value) ? Math.Max(0, value) : 0;
    private static MetalPriceSetting Clone(MetalPriceSetting item) => new() { Key = item.Key, Label = item.Label, LinearFootPrice = item.LinearFootPrice, WeightPerFoot = item.WeightPerFoot, LapScrewBagPrice = item.LapScrewBagPrice };
    private static NamedPriceSetting Clone(NamedPriceSetting item) => new() { Key = item.Key, Label = item.Label, Price = item.Price };
    private static ScrewPriceSetting Clone(ScrewPriceSetting item) => new() { Key = item.Key, Label = item.Label, UnpaintedBagPrice = item.UnpaintedBagPrice, PaintedBagPrice = item.PaintedBagPrice };
}

interface IKeyedSetting
{
    string Key { get; set; }
}

sealed class MetalPriceSetting : IKeyedSetting
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public double LinearFootPrice { get; set; }
    public double WeightPerFoot { get; set; }
    public double LapScrewBagPrice { get; set; }
}

sealed class NamedPriceSetting : IKeyedSetting
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public double Price { get; set; }
}

sealed class ScrewPriceSetting : IKeyedSetting
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public double UnpaintedBagPrice { get; set; }
    public double PaintedBagPrice { get; set; }
}

sealed class PreferenceSettings
{
    public bool ShowCustomTrimGrid { get; set; } = true;
    public bool UseRandomCustomTrimPieceColors { get; set; } = true;
    public bool ShowCustomTrimAngleArcs { get; set; } = true;
    public bool ShowCustomTrimMarquee { get; set; } = true;
    public int CustomTrimBackgroundArgb { get; set; } = Color.FromArgb(30, 30, 30).ToArgb();
    public int CustomTrimMinorGridArgb { get; set; } = Color.FromArgb(55, 55, 55).ToArgb();
    public int CustomTrimMajorGridArgb { get; set; } = Color.FromArgb(85, 85, 85).ToArgb();
    public int CustomTrimLineArgb { get; set; } = Color.FromArgb(91, 166, 255).ToArgb();
    public int CustomTrimSelectedLineArgb { get; set; } = Color.FromArgb(255, 193, 7).ToArgb();
    public int CustomTrimOriginArgb { get; set; } = Color.LimeGreen.ToArgb();
    public float CustomTrimMinorGridThickness { get; set; } = 1f;
    public float CustomTrimMajorGridThickness { get; set; } = 1.25f;
    public float CustomTrimLineThickness { get; set; } = 3f;
    public float CustomTrimVertexSize { get; set; } = 8f;

    public void Normalize()
    {
        CustomTrimMinorGridThickness = Math.Clamp(CustomTrimMinorGridThickness, 0.25f, 8f);
        CustomTrimMajorGridThickness = Math.Clamp(CustomTrimMajorGridThickness, 0.25f, 8f);
        CustomTrimLineThickness = Math.Clamp(CustomTrimLineThickness, 0.5f, 12f);
        CustomTrimVertexSize = Math.Clamp(CustomTrimVertexSize, 4f, 24f);
    }
}
