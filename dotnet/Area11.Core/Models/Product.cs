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

    /// <summary>Additional filter-only tags; never joined into financial category totals.</summary>
    public string? CategoryTags { get; set; }

    /// <summary>Loaded explicitly by ProductService.Get for the edit form.</summary>
    public List<int> CategoryTagIds { get; set; } = new();

    public string? CompanyName { get; set; }

    public string CategoryLabel => CategoryName ?? "Uncategorised";

    public string CompanyLabel => CompanyName ?? "No company";

    public string? RackNo { get; set; }

    public string? Room { get; set; }

    public string? PackSizeLabel { get; set; }

    public string BaseUnit { get; set; } = "tablet";

    /// <summary>Descriptive strength/volume only; it never converts stock into mL or another unit.</summary>
    public double StrengthValue { get; set; }

    public string StrengthUnit { get; set; } = string.Empty;

    public string StrengthLabel => StrengthValue > 0 && !string.IsNullOrWhiteSpace(StrengthUnit)
        ? Qty.Edit(StrengthValue) + " " + StrengthUnit
        : string.Empty;

    /// <summary>Descriptive variant text only; it is not a relationship or a stock conversion.</summary>
    public string VariantLabel
    {
        get
        {
            var strength = StrengthLabel;
            var pack = (PackSizeLabel ?? string.Empty).Trim();
            if (strength.Length == 0)
            {
                return pack;
            }

            return pack.Length == 0 ? strength : strength + " · " + pack;
        }
    }

    /// <summary>Unambiguous text for product pickers when two variants share a medicine name.</summary>
    public string ProductChoiceLabel => VariantLabel.Length == 0 ? Name : Name + " — " + VariantLabel;

    /// <summary>1 Box = ? strips/inner packs.</summary>
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

    /// <summary>First active gallery file name; the database never stores a machine-specific path.</summary>
    public string? PhotoFileName { get; set; }

    /// <summary>Resolved by the desktop shell for a thumbnail; not a database column.</summary>
    public string? PhotoPath { get; set; }

    public bool HasPhoto => !string.IsNullOrWhiteSpace(PhotoPath);

    public bool HasNoPhoto => !HasPhoto;

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

    /// <summary>Null = leave tags unchanged; an empty list = remove all additional tags.</summary>
    public List<int>? CategoryTagIds { get; set; }

    public int? CompanyId { get; set; }

    public string? RackNo { get; set; }

    public string? Room { get; set; }

    public string? PackSizeLabel { get; set; }

    public string? BaseUnit { get; set; }

    /// <summary>Zero with an empty StrengthUnit clears the optional descriptive strength.</summary>
    public double? StrengthValue { get; set; }

    public string? StrengthUnit { get; set; }

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
