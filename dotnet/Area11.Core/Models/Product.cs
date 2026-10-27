using Area11.Core.Utils;

namespace Area11.Core.Models;

/// <summary>
/// A medicine / item. Stock is NOT stored here — it is the sum of the live batches
/// (Spec 5.1.2), so <see cref="StockBase"/> is filled by the query, never written.
/// </summary>
public sealed class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Generic { get; set; }

    public string? Brand { get; set; }

    public string? Barcode { get; set; }

    public int? CategoryId { get; set; }

    public int? CompanyId { get; set; }

    public string? CategoryName { get; set; }

    public string? CompanyName { get; set; }

    public string? RackNo { get; set; }

    public string? Room { get; set; }

    public string? PackSizeLabel { get; set; }

    public string BaseUnit { get; set; } = "tablet";

    /// <summary>1 Box = ? strips.</summary>
    public int BoxStrips { get; set; }

    /// <summary>1 Strip = ? base units.</summary>
    public int StripTablets { get; set; }

    public long CostPaisa { get; set; }

    public long RetailPaisa { get; set; }

    public long VipPaisa { get; set; }

    public long DoctorPaisa { get; set; }

    public double ReorderLevel { get; set; }

    public bool TrackExpiry { get; set; } = true;

    public bool Active { get; set; } = true;

    /// <summary>Live shelf stock in base units (sum of batches).</summary>
    public double StockBase { get; set; }

    public double UnitsPerStrip => Math.Max(1, StripTablets);

    public double UnitsPerBox => Math.Max(1, BoxStrips) * Math.Max(1, StripTablets);

    /// <summary>One shared box/strip/base conversion for screen quotes and the committed sale.</summary>
    public double QuantityPerSelectedUnit(string? unit) =>
        QuantityPerSelectedUnit(unit, BoxStrips, StripTablets);

    public static double QuantityPerSelectedUnit(string? unit, int boxStrips, int stripTablets) =>
        unit?.Trim().ToLowerInvariant() switch
        {
            "box" => (double)(Math.Max(1, boxStrips) * Math.Max(1, stripTablets)),
            "strip" => (double)Math.Max(1, stripTablets),
            _ => 1d
        };

    public string PackLabel =>
        BoxStrips > 0 && StripTablets > 0
            ? $"1 box = {BoxStrips} strip = {UnitsPerBox:0.###} {BaseUnit}"
            : $"unit: {BaseUnit}";

    public string StockLabel => $"{StockBase:0.###} {BaseUnit}";

    public string CostText => Money.Format(CostPaisa);

    public string RetailText => Money.Format(RetailPaisa);

    public string VipText => Money.Format(VipPaisa);

    public string DoctorText => Money.Format(DoctorPaisa);

    public string MarginLabel
    {
        get
        {
            if (RetailPaisa <= 0)
            {
                return "—";
            }

            var margin = (RetailPaisa - CostPaisa) * 100m / RetailPaisa;
            return $"{margin:0.#}%";
        }
    }

    public override string ToString() => Name;
}

/// <summary>
/// Partial update payload (Spec 8.1.4.1): only the fields that are not null are written,
/// so a price change can never zero the pack formula, cost or rack.
/// </summary>
public sealed class ProductPatch
{
    public string? Name { get; set; }

    public string? Generic { get; set; }

    public string? Brand { get; set; }

    public string? Barcode { get; set; }

    public int? CategoryId { get; set; }

    public int? CompanyId { get; set; }

    public string? RackNo { get; set; }

    public string? Room { get; set; }

    public string? PackSizeLabel { get; set; }

    public string? BaseUnit { get; set; }

    public int? BoxStrips { get; set; }

    public int? StripTablets { get; set; }

    public long? CostPaisa { get; set; }

    public long? RetailPaisa { get; set; }

    public long? VipPaisa { get; set; }

    public long? DoctorPaisa { get; set; }

    public double? ReorderLevel { get; set; }

    public bool? TrackExpiry { get; set; }

    public bool? Active { get; set; }
}
