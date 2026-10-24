namespace Area11.Core.Utils;

/// <summary>
/// Box / strip / base conversion (Spec 9.2.3, 5.1.3).
///
/// One medicine is sold and returned in three units, and every unit is only a multiple of the base
/// unit: 1 box = <c>box_strips</c> strips, 1 strip = <c>strip_tablets</c> tablets. The same twelve
/// lines existed privately in three services; keeping the formula here means a new screen cannot
/// invent a fourth version of "how many tablets are in a box" and quietly disagree with the stock.
///
/// A zero or missing pack size means "the unit IS the base unit" - never a division by zero.
/// </summary>
public static class Packs
{
    /// <summary>How many base units are in one of <paramref name="unit"/> (box / strip / base).</summary>
    public static double PerUnit(int boxStrips, int stripTablets, string? unit)
    {
        var perStrip = Math.Max(1, stripTablets);
        var perBox = Math.Max(1, boxStrips) * perStrip;

        return (unit ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "box" => boxStrips > 0 ? perBox : 1,
            "strip" => stripTablets > 0 ? perStrip : 1,
            _ => 1
        };
    }

    /// <summary>"1 box = 2 strip = 20 tablet" - the words under a quantity box.</summary>
    public static string Label(int boxStrips, int stripTablets, string baseUnit)
    {
        var unit = string.IsNullOrWhiteSpace(baseUnit) ? "unit" : baseUnit.Trim();

        return boxStrips > 0 && stripTablets > 0
            ? "1 box = " + boxStrips + " strip = " + Qty.Format(PerUnit(boxStrips, stripTablets, "box")) + " " + unit
            : "unit: " + unit;
    }
}
