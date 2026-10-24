using Area11.Core.Utils;

namespace Area11.Core.Models;

/// <summary>
/// The three reorder bands of Spec 10.5.2. They are **derived from the medicine's own minimum**
/// rather than from a second setting: one number is set per medicine, and every screen reads the
/// same rule (D-55).
/// </summary>
public static class ReorderTier
{
    /// <summary>Nothing on the shelf at all - red.</summary>
    public const string Out = "out";

    /// <summary>At or below the minimum - order now.</summary>
    public const string Critical = "critical";

    /// <summary>Above the minimum but within half of it - running low.</summary>
    public const string Low = "low";
}

/// <summary>
/// One medicine on the reorder pad: what is left, what the owner wants to keep, what to ask for,
/// and who supplied it last.
///
/// It is a transport shape, not a table row: the list is built from products + batches + the last
/// purchase, and nothing on this page writes unless the owner changes a minimum (D-55).
/// </summary>
public sealed class ReorderRow
{
    public int ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Generic { get; set; }

    public string? Company { get; set; }

    public string BaseUnit { get; set; } = "unit";

    public int BoxStrips { get; set; }

    public int StripTablets { get; set; }

    /// <summary>Base units on the shelf, summed over active batches (may be negative).</summary>
    public double StockQty { get; set; }

    /// <summary>The owner's minimum - `products.reorder_level`, 0 = not watched.</summary>
    public double LevelQty { get; set; }

    /// <summary>How much to ask for so the shelf reaches twice the minimum.</summary>
    public double SuggestQty { get; set; }

    public int? SupplierId { get; set; }

    public string? SupplierName { get; set; }

    public string? SupplierPhone { get; set; }

    public string? LastPurchaseDate { get; set; }

    public long LastCostPaisa { get; set; }

    /// <summary>One of <see cref="ReorderTier"/> - the colour trigger on screen.</summary>
    public string Tier { get; set; } = ReorderTier.Low;

    public bool HasSupplier => SupplierId is not null;

    public string NameLabel => string.IsNullOrWhiteSpace(Generic) ? Name : Name + " (" + Generic + ")";

    public string CompanyLabel => Company ?? string.Empty;

    public string StockText => Qty.Format(StockQty) + " " + BaseUnit;

    public string LevelText => LevelQty > 0 ? Qty.Format(LevelQty) + " " + BaseUnit : "not watched";

    public string SuggestText => SuggestQty > 0 ? Qty.Format(SuggestQty) : "-";

    public string TierLabel => Tier switch
    {
        ReorderTier.Out => "OUT OF STOCK",
        ReorderTier.Critical => "ORDER NOW",
        _ => "RUNNING LOW"
    };

    public string SupplierLabel => HasSupplier && !string.IsNullOrWhiteSpace(SupplierName)
        ? SupplierName!
        : "no supplier yet";

    public string PhoneLabel => string.IsNullOrWhiteSpace(SupplierPhone) ? "no mobile saved" : SupplierPhone!;

    public string LastBoughtLabel => string.IsNullOrWhiteSpace(LastPurchaseDate)
        ? "never bought"
        : "last bought " + LastPurchaseDate![..Math.Min(10, LastPurchaseDate!.Length)];

    /// <summary>"1 box = 2 strip = 20 tablet" - so the owner knows what a quantity means.</summary>
    public string PackLabel => Packs.Label(BoxStrips, StripTablets, BaseUnit);
}

/// <summary>The whole pad in one object: the rows and what each band adds up to.</summary>
public sealed class ReorderReport
{
    public List<ReorderRow> Rows { get; } = new();

    /// <summary>True when the owner switched `reorder.enabled` off - the page then only says so.</summary>
    public bool IsSwitchedOff { get; set; }

    public int OutCount => Rows.Count(row => row.Tier == ReorderTier.Out);

    public int CriticalCount => Rows.Count(row => row.Tier == ReorderTier.Critical);

    public int LowCount => Rows.Count(row => row.Tier == ReorderTier.Low);

    public int Count => Rows.Count;

    public string SummaryText => IsSwitchedOff
        ? "The reorder list is switched off in Settings."
        : Rows.Count == 0
            ? "Nothing needs ordering - every watched medicine is above its minimum."
            : Rows.Count + " medicine(s) need ordering: " + OutCount + " out of stock, "
              + CriticalCount + " at the minimum, " + LowCount + " running low.";

    /// <summary>The suppliers that appear on the pad, biggest list first - for the filter.</summary>
    public List<string> SupplierNames => Rows
        .Where(row => !string.IsNullOrWhiteSpace(row.SupplierName))
        .GroupBy(row => row.SupplierName!, StringComparer.OrdinalIgnoreCase)
        .OrderByDescending(group => group.Count())
        .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
        .Select(group => group.Key)
        .ToList();
}

/// <summary>One line of the WhatsApp order, already decided by the owner on the pad.</summary>
public sealed class ReorderLine
{
    public string Name { get; init; } = string.Empty;

    public string? Generic { get; init; }

    public string? Company { get; init; }

    /// <summary>How much to ask for, in base units.</summary>
    public double Quantity { get; init; }

    public string BaseUnit { get; init; } = "unit";

    public int BoxStrips { get; init; }

    public int StripTablets { get; init; }

    /// <summary>"1 box + 2 strip + 5 tablet" - the words the supplier reads, not a raw number.</summary>
    public string QuantityLabel => ReorderUnits.Describe(Quantity, BoxStrips, StripTablets, BaseUnit);
}

/// <summary>
/// Base units → the words a supplier uses. Kept beside the line rather than in the service so the
/// test and the screen cannot disagree about what "35 tablet" means on paper.
/// </summary>
public static class ReorderUnits
{
    public static string Describe(double quantity, int boxStrips, int stripTablets, string baseUnit)
    {
        var unit = string.IsNullOrWhiteSpace(baseUnit) ? "unit" : baseUnit.Trim();
        var left = quantity;

        if (left <= 0)
        {
            return "0 " + unit;
        }

        // A pack formula of zero means the base unit IS the unit: never invent a box.
        if (boxStrips <= 0 && stripTablets <= 0)
        {
            return Qty.Format(left) + " " + unit;
        }

        var perStrip = stripTablets > 0 ? stripTablets : 1;
        var perBox = boxStrips > 0 ? boxStrips * perStrip : 0;
        var parts = new List<string>();

        if (perBox > 0)
        {
            var boxes = Math.Floor((left / perBox) + 0.0000001);
            if (boxes > 0)
            {
                parts.Add(Qty.Format(boxes) + " box");
                left -= boxes * perBox;
            }
        }

        if (stripTablets > 0)
        {
            var strips = Math.Floor((left / perStrip) + 0.0000001);
            if (strips > 0)
            {
                parts.Add(Qty.Format(strips) + " strip");
                left -= strips * perStrip;
            }
        }

        if (!Qty.NearlyZero(left))
        {
            parts.Add(Qty.Format(left) + " " + unit);
        }

        return parts.Count == 0 ? Qty.Format(quantity) + " " + unit : string.Join(" + ", parts);
    }
}
