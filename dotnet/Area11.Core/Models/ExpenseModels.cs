using Area11.Core.Utils;

namespace Area11.Core.Models;

/// <summary>
/// The eight words the shop uses for its own costs. They live in the model layer so the list on
/// screen and the text in the audit log can never drift apart, and so a model never has to call a
/// service just to print a word.
/// </summary>
public static class ExpenseCategories
{
    public static readonly string[] All =
    {
        "rent", "electricity", "salary", "transport", "tea", "packing", "repair", "other"
    };

    /// <summary>The stored word -> the words the owner reads. An unknown value is "Other", never blank.</summary>
    public static string Label(string? category) => (category ?? "other").Trim().ToLowerInvariant() switch
    {
        "rent" => "Rent",
        "electricity" => "Electricity",
        "salary" => "Salary",
        "transport" => "Transport",
        "tea" => "Tea / food",
        "packing" => "Packing",
        "repair" => "Repair",
        _ => "Other"
    };
}

/// <summary>
/// Money the shop pays to run itself: rent, the electricity bill, the boy's salary, the tea, a
/// broken shutter (§9.1, "expenses"). It is **not** a purchase: no stock arrives and no supplier's
/// account moves - only cash leaves the drawer.
/// </summary>
public sealed class ExpenseInput
{
    /// <summary>rent | electricity | salary | transport | tea | packing | repair | other</summary>
    public string Category { get; set; } = "other";

    /// <summary>What it was, in the owner's own words: "August rent", "tea for the counter".</summary>
    public string Title { get; set; } = string.Empty;

    public long AmountPaisa { get; set; }

    public string? Note { get; set; }
}

/// <summary>
/// The owner taking money or medicine out of his own shop (`owner_drawings`).
///
/// Two kinds, and the difference matters:
///   • **cash** - money leaves the drawer, so the shift count must know about it;
///   • **goods** - medicine leaves the shelf at **cost**, and **no cash moves**; it is valued at
///     purchase cost so the owner can see what he took, not so a profit is invented.
/// </summary>
public sealed class DrawingInput
{
    /// <summary>cash | goods</summary>
    public string Type { get; set; } = "cash";

    public long AmountPaisa { get; set; }

    // Goods only: which medicine, from which batch, how much.
    public int ProductId { get; set; }

    public int? BatchId { get; set; }

    public double QtyEntered { get; set; }

    /// <summary>box | strip | unit</summary>
    public string Unit { get; set; } = "unit";

    public string? Note { get; set; }
}

/// <summary>What was stored, and anything the owner must read afterwards.</summary>
public sealed class ExpenseResult
{
    public int Id { get; set; }

    public string Kind { get; set; } = "expense";

    public long AmountPaisa { get; set; }

    public List<string> Warnings { get; } = new();

    public bool HasWarning => Warnings.Count > 0;

    public string WarningText => string.Join(" ", Warnings);
}

/// <summary>What the owner took: the value at cost, and the stock left in that batch.</summary>
public sealed class DrawingResult
{
    public int Id { get; set; }

    public string Type { get; set; } = "cash";

    public long AmountPaisa { get; set; }

    public double QtyBase { get; set; }

    public double StockAfterBase { get; set; }

    public List<string> Warnings { get; } = new();

    public bool HasWarning => Warnings.Count > 0;

    public string WarningText => string.Join(" ", Warnings);
}

/// <summary>One line of the cash-out list: an expense and a drawing look the same to the reader.</summary>
public sealed class CashOutRow
{
    public int Id { get; set; }

    /// <summary>expense | drawing_cash | drawing_goods</summary>
    public string Kind { get; set; } = "expense";

    public string? Date { get; set; }

    /// <summary>expense category, or "cash" / "goods" for a drawing.</summary>
    public string? Category { get; set; }

    public string Title { get; set; } = string.Empty;

    public long AmountPaisa { get; set; }

    public string? Note { get; set; }

    public string? UserName { get; set; }

    public bool IsDrawing => Kind.StartsWith("drawing", StringComparison.Ordinal);

    /// <summary>True = money really left the drawer. A goods drawing took stock, not cash.</summary>
    public bool TookCash => Kind is "expense" or "drawing_cash";

    public string AmountLabel => Money.Format(AmountPaisa);

    public string DateLabel => string.IsNullOrWhiteSpace(Date)
        ? "-"
        : Date!.Length >= 16 ? Date![..16] : Date!;

    public string KindLabel => Kind switch
    {
        "drawing_cash" => "Owner took cash",
        "drawing_goods" => "Owner took goods",
        _ => ExpenseCategories.Label(Category)
    };

    public string DetailLabel => string.IsNullOrWhiteSpace(Note) ? Title : Title + " - " + Note;

    public string WhoLabel => string.IsNullOrWhiteSpace(UserName) ? "-" : UserName!;

    /// <summary>The list uses this to show a goods drawing in a different colour: no money moved.</summary>
    public string CashLabel => TookCash ? AmountLabel : "stock only";
}

/// <summary>A day's cash-out at a glance, for the top of the screen.</summary>
public sealed class CashOutToday
{
    public long ExpensePaisa { get; set; }

    public long DrawingCashPaisa { get; set; }

    public long DrawingGoodsCostPaisa { get; set; }

    public int Entries { get; set; }

    /// <summary>Money that left the drawer today (expenses + cash drawings).</summary>
    public long CashOutPaisa => ExpensePaisa + DrawingCashPaisa;

    public string ExpenseLabel => Money.Format(ExpensePaisa);

    public string DrawingLabel => Money.Format(DrawingCashPaisa);

    public string GoodsLabel => Money.Format(DrawingGoodsCostPaisa);

    public string CashOutLabel => Money.Format(CashOutPaisa);
}
