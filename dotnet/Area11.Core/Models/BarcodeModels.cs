namespace Area11.Core.Models;

/// <summary>
/// The pack a barcode belongs to (Spec 35.6): the same medicine can print one code on the strip,
/// another on the box. Scanning the box must add a **box**, not one tablet, or the shelf and the
/// bill disagree from the first customer.
///
/// The three words are the same ones the counter and the purchase screen already use, so the cart
/// needs no translation table.
/// </summary>
public static class BarcodePack
{
    public const string Base = "base";
    public const string Strip = "strip";
    public const string Box = "box";

    public static readonly string[] All = { Base, Strip, Box };

    public static bool IsKnown(string? pack) =>
        !string.IsNullOrWhiteSpace(pack) && All.Contains(Clean(pack));

    public static string Clean(string? pack) => (pack ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>"1 tablet" / "1 strip" / "1 box" - what one scan adds.</summary>
    public static string LabelFor(string? pack, string? baseUnit = null) => Clean(pack) switch
    {
        Box => "box",
        Strip => "strip",
        _ => string.IsNullOrWhiteSpace(baseUnit) ? "unit" : baseUnit!.Trim()
    };
}

/// <summary>One barcode of one medicine, as `product_barcodes` holds it.</summary>
public sealed class ProductBarcode
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public string Barcode { get; set; } = string.Empty;

    public string Pack { get; set; } = BarcodePack.Base;

    public string? Note { get; set; }

    public bool Active { get; set; } = true;

    public string? CreatedAt { get; set; }

    public string PackLabel => BarcodePack.LabelFor(Pack);

    /// <summary>"on the shelf" / "put away" - a code is never deleted (D-00b).</summary>
    public string StateLabel => Active ? "works at the counter" : "put away";

    public string NoteLabel => string.IsNullOrWhiteSpace(Note) ? "-" : Note!.Trim();
}

/// <summary>
/// What a scanned code means: which medicine, which pack. The counter uses only this - it never
/// looks at `products.barcode`, so the old single code and the new list behave the same way.
/// </summary>
public sealed class BarcodeMatch
{
    public int BarcodeId { get; set; }

    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string Barcode { get; set; } = string.Empty;

    public string Pack { get; set; } = BarcodePack.Base;

    public string PackLabel => BarcodePack.LabelFor(Pack);

    /// <summary>"Panadol 500mg · box" - the line the counter shows after a scan.</summary>
    public string Caption => ProductName + " · " + PackLabel;
}
