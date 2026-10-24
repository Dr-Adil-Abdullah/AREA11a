using Area11.Core.Utils;

namespace Area11.Core.Models;

/// <summary>What happened to one line of the owner's file.</summary>
public enum ImportState
{
    /// <summary>The line is understood and will be written.</summary>
    Ready,

    /// <summary>Already in the shop - skipped, not an error (a second import must not double anything).</summary>
    Skipped,

    /// <summary>Something is wrong with the line; the whole file waits until it is fixed.</summary>
    Error
}

/// <summary>
/// One line of a preview: what the line says, whether it can be written, and why not.
///
/// The line number is the one the owner sees in Excel, so a refusal can be found and fixed without
/// guessing which row was meant.
/// </summary>
public sealed class ImportRow
{
    public int Line { get; set; }

    /// <summary>What the row is about: the medicine, the customer, the supplier.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The rest of the row in one readable line, so the preview is checkable by eye.</summary>
    public string Detail { get; set; } = string.Empty;

    public ImportState State { get; set; } = ImportState.Ready;

    /// <summary>"already exists", "retail is not a number", "expiry 31-02-2026 does not exist".</summary>
    public string Message { get; set; } = string.Empty;

    public bool IsReady => State == ImportState.Ready;

    public bool IsSkipped => State == ImportState.Skipped;

    public bool IsError => State == ImportState.Error;

    public string StateLabel => State switch
    {
        ImportState.Ready => "OK",
        ImportState.Skipped => "Skipped",
        _ => "Error"
    };

    public string LineLabel => "Line " + Line.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>"Line 7 - OK" for the list, and the reason beside it.</summary>
    public string Caption => IsError
        ? $"{LineLabel} · {Key} · {Message}"
        : $"{LineLabel} · {Key} · {Detail}";
}

/// <summary>
/// The whole file, checked but not yet written.
///
/// **Nothing is written while a preview exists.** The owner reads it first; only "Import" writes, and
/// it writes the rows that were shown, so what he approved is exactly what goes in.
/// </summary>
public sealed class ImportPreview
{
    /// <summary>"Products", "Customers" or "Suppliers" - the word used on the screen and in the log.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>The line the headings were found on (1 for a normal file), or 0 when they were missing.</summary>
    public int HeaderLine { get; set; }

    public List<ImportRow> Rows { get; init; } = new();

    /// <summary>The typed rows, ready to be written. One list per kind; the others stay empty.</summary>
    public List<ProductImport> Products { get; init; } = new();

    public List<CustomerImport> Customers { get; init; } = new();

    public List<SupplierImport> Suppliers { get; init; } = new();

    /// <summary>Columns the file has, after normalising - shown when a required one is missing.</summary>
    public List<string> Headings { get; init; } = new();

    public int ReadyCount => Rows.Count(row => row.IsReady);

    public int SkippedCount => Rows.Count(row => row.IsSkipped);

    public int ErrorCount => Rows.Count(row => row.IsError);

    /// <summary>
    /// A file with any error writes nothing: half-importing a stock list leaves the shop unable to say
    /// which half. The owner fixes the red lines and imports again.
    /// </summary>
    public bool CanApply => ErrorCount == 0 && ReadyCount > 0;

    public string SummaryText
    {
        get
        {
            if (Headings.Count == 0)
            {
                return "The file could not be read: the first line must be the headings.";
            }

            var text = $"{ReadyCount} line(s) ready";

            if (SkippedCount > 0)
            {
                text += $", {SkippedCount} already in the shop (skipped)";
            }

            if (ErrorCount > 0)
            {
                text += $", {ErrorCount} with a problem - fix them and choose the file again";
            }

            return text + ".";
        }
    }
}

/// <summary>One medicine read from the file. Money is already in paisa, so no rounding happens later.</summary>
public sealed class ProductImport
{
    public string Name { get; set; } = string.Empty;

    public string? Generic { get; set; }

    public string? Brand { get; set; }

    public string? Barcode { get; set; }

    public string? Category { get; set; }

    public string? RackNo { get; set; }

    public string? PackLabel { get; set; }

    public string BaseUnit { get; set; } = "tablet";

    public int BoxStrips { get; set; }

    public int StripTablets { get; set; }

    public long CostPaisa { get; set; }

    public long RetailPaisa { get; set; }

    public int ReorderLevel { get; set; }

    /// <summary>Opening stock in BASE units (tablets). Zero means "make the card only".</summary>
    public double OpeningQty { get; set; }

    public string? BatchNo { get; set; }

    /// <summary>yyyy-MM-dd, or empty. Empty on a row with opening stock is refused.</summary>
    public string? Expiry { get; set; }
}

/// <summary>One customer read from the file.</summary>
public sealed class CustomerImport
{
    public string Name { get; set; } = string.Empty;

    public string? Phone { get; set; }

    /// <summary>normal | vip | doctor (anything else becomes normal, and the preview says so).</summary>
    public string Category { get; set; } = "normal";

    public long OpeningPaisa { get; set; }

    public long CreditLimitPaisa { get; set; }
}

/// <summary>One supplier read from the file.</summary>
public sealed class SupplierImport
{
    public string Name { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Agency { get; set; }

    public long OpeningPaisa { get; set; }
}

/// <summary>What was actually written, in one sentence the owner can check against the file.</summary>
public sealed class ImportOutcome
{
    public string Kind { get; set; } = string.Empty;

    public int Added { get; set; }

    public int Skipped { get; set; }

    /// <summary>Batches created for opening stock (products only).</summary>
    public int Batches { get; set; }

    /// <summary>Total opening stock, in base units.</summary>
    public double StockBase { get; set; }

    public string SummaryText
    {
        get
        {
            var text = $"{Added} {Kind.ToLowerInvariant()} added";

            if (Skipped > 0)
            {
                text += $", {Skipped} already existed";
            }

            if (Batches > 0)
            {
                text += $", {Batches} opening batch(es) with {Qty.Format(StockBase)} units";
            }

            return text + ".";
        }
    }
}

/// <summary>How a bulk price change is expressed: a percentage or a fixed number of rupees.</summary>
public enum PriceChangeKind
{
    /// <summary>Raise or lower every price by a percentage of itself.</summary>
    Percent,

    /// <summary>Add or subtract the same number of rupees from every price.</summary>
    Rupees
}

/// <summary>The owner's instruction for the bulk price screen.</summary>
public sealed class PriceChangeRequest
{
    public PriceChangeKind Kind { get; set; } = PriceChangeKind.Percent;

    /// <summary>May be negative: -5 lowers by five percent, or by five rupees.</summary>
    public decimal Amount { get; set; }

    /// <summary>Only this category, or 0 for every medicine.</summary>
    public int CategoryId { get; set; }

    /// <summary>Change the purchase cost as well (the supplier raised his rates).</summary>
    public bool IncludeCost { get; set; }
}

/// <summary>One medicine before and after - the screen the owner approves.</summary>
public sealed class PriceChangeRow
{
    public int ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public long OldRetailPaisa { get; set; }

    public long NewRetailPaisa { get; set; }

    public long OldCostPaisa { get; set; }

    public long NewCostPaisa { get; set; }

    public bool CostChanges { get; set; }

    /// <summary>True = the new retail is below the new cost; the line is refused, not silently applied.</summary>
    public bool BelowCost => NewRetailPaisa < NewCostPaisa;

    public bool Changes => OldRetailPaisa != NewRetailPaisa || OldCostPaisa != NewCostPaisa;

    public string RetailLabel => Money.Format(OldRetailPaisa) + "  →  " + Money.Format(NewRetailPaisa);

    public string CostLabel => IncludeCostText;

    public string IncludeCostText => CostChanges
        ? Money.Format(OldCostPaisa) + "  →  " + Money.Format(NewCostPaisa)
        : Money.Format(OldCostPaisa);

    /// <summary>"Rs 120 → Rs 126  (+5%)" - the movement in one glance.</summary>
    public string ChangeLabel
    {
        get
        {
            if (OldRetailPaisa == 0)
            {
                return NewRetailPaisa == 0 ? "no change" : "from zero";
            }

            var delta = (decimal)(NewRetailPaisa - OldRetailPaisa) / OldRetailPaisa * 100m;

            return (delta >= 0 ? "+" : string.Empty) + delta.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%";
        }
    }
}

/// <summary>The preview of a bulk price change: every row, and how many can be written.</summary>
public sealed class PricePreview
{
    public List<PriceChangeRow> Rows { get; init; } = new();

    public int ChangeCount => Rows.Count(row => row.Changes && !row.BelowCost);

    public int BelowCostCount => Rows.Count(row => row.BelowCost);

    public int SameCount => Rows.Count(row => !row.Changes);

    /// <summary>Only rows that really change and stay at or above cost are written.</summary>
    public bool CanApply => ChangeCount > 0 && BelowCostCount == 0;

    public string SummaryText
    {
        get
        {
            var text = $"{ChangeCount} price(s) will change";

            if (SameCount > 0)
            {
                text += $", {SameCount} already correct";
            }

            if (BelowCostCount > 0)
            {
                text += $", {BelowCostCount} would fall below cost - those are refused, so nothing is written";
            }

            return text + ".";
        }
    }
}

/// <summary>What the bulk change wrote.</summary>
public sealed class PriceOutcome
{
    public int Changed { get; set; }

    public int BelowCost { get; set; }

    public string SummaryText =>
        $"{Changed} price(s) changed. Every change is in the black box, old value and new.";
}
