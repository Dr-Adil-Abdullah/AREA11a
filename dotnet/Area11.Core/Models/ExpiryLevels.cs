using System.Text.Json;
using System.Text.Json.Serialization;
using Area11.Core.Utils;

namespace Area11.Core.Models;

/// <summary>One band of the owner's expiry scale, exactly as the reference schema stores it.</summary>
public sealed class ExpiryBand
{
    /// <summary>1 = farthest (a year away), 3 = nearest (three months away).</summary>
    [JsonPropertyName("level")]
    public int Level { get; set; } = 1;

    [JsonPropertyName("days")]
    public int Days { get; set; }

    /// <summary>blue | yellow | red - the colour the owner sees on the shelf list.</summary>
    [JsonPropertyName("color")]
    public string Color { get; set; } = "blue";

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;
}

/// <summary>
/// The three expiry bands read from the `expiry.levels` setting (Spec 6.1, F-29).
///
/// Why this type exists: the bands used to be three numbers typed into the code, so changing them in
/// Settings changed nothing. They are now read fresh on every screen - no restart, no second copy.
///
/// A stored list that is broken, empty, out of order or has duplicate days is **repaired instead of
/// trusted**: the bands are sorted longest-first, forced above zero, and the defaults are used when
/// nothing usable is left. A shop must never lose its expiry colours because one JSON bracket moved.
/// </summary>
public sealed class ExpiryLevels
{
    public const int DefaultLevel1Days = 365;
    public const int DefaultLevel2Days = 180;
    public const int DefaultLevel3Days = 90;

    public static readonly ExpiryLevels Default = new(MakeDefaultBands());

    private ExpiryLevels(IReadOnlyList<ExpiryBand> bands)
    {
        Bands = bands;
    }

    /// <summary>Always three bands, sorted from the farthest (Level 1) to the nearest (Level 3).</summary>
    public IReadOnlyList<ExpiryBand> Bands { get; }

    public int Level1Days => Bands[0].Days;

    public int Level2Days => Bands[1].Days;

    public int Level3Days => Bands[2].Days;

    public string Level3Color => Bands[2].Color;

    /// <summary>
    /// One sentence for the Settings screen, with both edges of every band, so the owner can check
    /// that the numbers he typed mean what he thinks: "Level 1: 181 to 365 days · Level 2: 91 to
    /// 180 · Level 3: 90 or less · EXPIRED: past the date".
    /// </summary>
    public string Summary =>
        "Level 1: " + (Level2Days + 1) + " to " + Level1Days + " days · "
        + "Level 2: " + (Level3Days + 1) + " to " + Level2Days + " days · "
        + "Level 3: " + Level3Days + " days or less · EXPIRED: past the date";

    /// <summary>Parses the stored JSON. Anything unusable falls back to 365 / 180 / 90.</summary>
    public static ExpiryLevels Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Default;
        }

        try
        {
            var bands = JsonSerializer.Deserialize<List<ExpiryBand>>(json, Json.ReadOptions);
            return Build(bands);
        }
        catch (JsonException)
        {
            return Default;
        }
    }

    /// <summary>Normalises any three bands: sorted, three of them, unique days, labels filled in.</summary>
    public static ExpiryLevels Build(IReadOnlyList<ExpiryBand>? bands)
    {
        if (bands is null || bands.Count != 3)
        {
            return Default;
        }

        var clean = new List<ExpiryBand>();

        foreach (var band in bands)
        {
            if (band is null)
            {
                return Default;
            }

            clean.Add(new ExpiryBand
            {
                Days = Math.Clamp(band.Days, 1, 3650),
                Color = string.IsNullOrWhiteSpace(band.Color) ? "blue" : band.Color.Trim(),
                Label = band.Label ?? string.Empty
            });
        }

        clean = clean.OrderByDescending(band => band.Days).ToList();

        if (clean[0].Days == clean[1].Days || clean[1].Days == clean[2].Days)
        {
            // Two bands with the same number of days means one of them can never appear; the
            // defaults are clearer than a colour the owner can never see.
            return Default;
        }

        for (var index = 0; index < clean.Count; index++)
        {
            clean[index].Level = index + 1;

            if (string.IsNullOrWhiteSpace(clean[index].Label))
            {
                clean[index].Label = "Level " + (index + 1);
            }
        }

        return new ExpiryLevels(clean);
    }

    /// <summary>The same three bands with new day counts - what the Settings screen saves.</summary>
    public ExpiryLevels WithDays(int level1Days, int level2Days, int level3Days)
    {
        var bands = new List<ExpiryBand>
        {
            new() { Days = level1Days, Color = Bands[0].Color, Label = Bands[0].Label },
            new() { Days = level2Days, Color = Bands[1].Color, Label = Bands[1].Label },
            new() { Days = level3Days, Color = Bands[2].Color, Label = Bands[2].Label }
        };

        return Build(bands);
    }

    /// <summary>True when the three numbers can be saved: all different, nearest to farthest.</summary>
    public static bool IsOrdered(int level1Days, int level2Days, int level3Days) =>
        level3Days > 0 && level3Days < level2Days && level2Days < level1Days;

    /// <summary>
    /// The rule that actually protects the shop: three different positive numbers, in any order.
    /// The screen sorts them, but the service must not accept a list where two bands mean the same
    /// day - one of those colours could never be seen on the shelf list.
    /// </summary>
    public static bool IsDistinct(IReadOnlyList<ExpiryBand>? bands)
    {
        if (bands is null || bands.Count != 3 || bands.Any(band => band is null))
        {
            return false;
        }

        var days = bands.Select(band => band.Days).OrderByDescending(day => day).ToList();

        return days[2] > 0 && days[0] > days[1] && days[1] > days[2];
    }

    /// <summary>The stored JSON as bands; an empty list when the text is not a usable list.</summary>
    public static List<ExpiryBand> ParseBands(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<ExpiryBand>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<ExpiryBand>>(json, Json.ReadOptions)
                ?? new List<ExpiryBand>();
        }
        catch (JsonException)
        {
            return new List<ExpiryBand>();
        }
    }

    public string LabelFor(string? expiryDate) =>
        Expiry.StatusLabel(expiryDate, Level1Days, Level2Days, Level3Days);

    public ExpiryKind KindFor(string? expiryDate) =>
        Expiry.KindOf(expiryDate, Level1Days, Level2Days, Level3Days);

    /// <summary>The JSON the `expiry.levels` setting stores.</summary>
    public string ToJson() => JsonSerializer.Serialize(Bands, Json.WriterOptions);

    private static List<ExpiryBand> MakeDefaultBands() => new()
    {
        new() { Level = 1, Days = DefaultLevel1Days, Color = "blue", Label = "Level 1 - 1 year" },
        new() { Level = 2, Days = DefaultLevel2Days, Color = "yellow", Label = "Level 2 - 6 months" },
        new() { Level = 3, Days = DefaultLevel3Days, Color = "red", Label = "Level 3 - 3 months" }
    };
}
