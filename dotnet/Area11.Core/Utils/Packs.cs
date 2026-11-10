namespace Area11.Core.Utils;

/// <summary>
/// Box / strip / unit conversion (Spec 9.2.3, 5.1.3).
///
/// One medicine is sold and returned in three units, and every unit is only a multiple of the base
/// unit: 1 box = <c>box_strips</c> strips, 1 strip = <c>strip_tablets</c> tablets. The same formula
/// existed privately in half a dozen services; it now lives in one place and every screen and
/// service calls it, so a new screen cannot invent another version of "how many tablets are in
/// a box" and quietly disagree with the stock.
///
/// A zero or missing pack size means "the unit IS the base unit" - never a division by zero.
/// </summary>
public static class Packs
{
    /// <summary>How many base units are in one of <paramref name="unit"/> (box / strip / unit).</summary>
    public static double PerUnit(int boxStrips, int stripTablets, string? unit) =>
        Models.Product.QuantityPerSelectedUnit(unit, boxStrips, stripTablets);

    /// <summary>
    /// The word the owner reads for one loose single: "unit". Old rows stored the "base"
    /// token; slips, history lines and carts show "unit" for both, so every screen speaks the
    /// same word without rewriting history.
    /// </summary>
    public static string Display(string? unit) =>
        string.IsNullOrWhiteSpace(unit) || unit.Trim().Equals("base", StringComparison.OrdinalIgnoreCase)
            ? "unit"
            : unit.Trim();

    /// <summary>"1 box = 2 strip = 20 tablet" - the words under a quantity box.</summary>
    public static string Label(int boxStrips, int stripTablets, string baseUnit)
    {
        var unit = string.IsNullOrWhiteSpace(baseUnit) ? "unit" : baseUnit.Trim();

        return boxStrips > 0 && stripTablets > 0
            ? "1 box = " + boxStrips + " strip = " + Qty.Format(PerUnit(boxStrips, stripTablets, "box")) + " " + unit
            : "unit: " + unit;
    }
}
