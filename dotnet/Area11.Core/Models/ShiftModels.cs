using Area11.Core.Utils;

namespace Area11.Core.Models;

/// <summary>
/// One shift (galla) - from the moment the drawer is opened with a float until it is counted and
/// closed (F-31, Spec 14). The six money lines are read from the history, never typed by hand:
///
///   in  : the float the drawer started with, cash bills, khata money received, cash a supplier
///         handed back for goods;
///   out : cash refunds to customers, money paid to suppliers (including the amount paid at the
///         moment of a purchase), **shop expenses** and **cash the owner took** (F-32).
///
/// Cards and online money are **not** here: they never entered the drawer.
/// </summary>
public sealed class ShiftMoney
{
    public int Id { get; set; }

    /// <summary>Who opened the drawer - only that person (or a manager) may close it.</summary>
    public int UserId { get; set; }

    public string OpenedAt { get; set; } = string.Empty;

    public string OpenedBy { get; set; } = string.Empty;

    public long OpeningFloatPaisa { get; set; }

    public long CashSalesPaisa { get; set; }

    public long CashCollectionsPaisa { get; set; }

    public long SupplierCashInPaisa { get; set; }

    public long SupplierCashOutPaisa { get; set; }

    public long CashRefundsPaisa { get; set; }

    /// <summary>Rent, electricity, tea, a repair - money that left the till (F-32).</summary>
    public long ExpensesPaisa { get; set; }

    /// <summary>Cash the owner took for himself. A goods drawing is NOT here: no money moved.</summary>
    public long DrawingsPaisa { get; set; }

    /// <summary>What the drawer should hold: float + everything in - everything out.</summary>
    public long ExpectedPaisa { get; set; }

    /// <summary>What the owner counted. Null while the shift is still open.</summary>
    public long? ActualPaisa { get; set; }

    /// <summary>actual - expected. Positive = extra money, negative = short.</summary>
    public long? DifferencePaisa { get; set; }

    public string Status { get; set; } = "open";

    public string? Note { get; set; }

    public bool IsOpen => Status == "open";

    public string StatusLabel => IsOpen ? "Open" : "Closed";

    public long MoneyInPaisa =>
        OpeningFloatPaisa + CashSalesPaisa + CashCollectionsPaisa + SupplierCashInPaisa;

    public long MoneyOutPaisa => CashRefundsPaisa + SupplierCashOutPaisa + ExpensesPaisa + DrawingsPaisa;

    public string OpenedLabel => OpenedAt.Length >= 16 ? OpenedAt[..16] : OpenedAt;

    public string FloatLabel => Money.Format(OpeningFloatPaisa);

    public string CashSalesLabel => Money.Format(CashSalesPaisa);

    public string CollectionsLabel => Money.Format(CashCollectionsPaisa);

    public string SupplierInLabel => Money.Format(SupplierCashInPaisa);

    public string SupplierOutLabel => Money.Format(SupplierCashOutPaisa);

    public string RefundsLabel => Money.Format(CashRefundsPaisa);

    public string ExpensesLabel => Money.Format(ExpensesPaisa);

    public string DrawingsLabel => Money.Format(DrawingsPaisa);

    public string ExpectedLabel => Money.Format(ExpectedPaisa);

    public string ActualLabel => ActualPaisa is long actual ? Money.Format(actual) : "-";

    /// <summary>The sentence the owner reads before he signs the day off: exact, over, or short.</summary>
    public string DifferenceLabel => DifferencePaisa switch
    {
        null => "-",
        0L => "exact",
        > 0 => "over by " + Money.Format(DifferencePaisa.Value),
        _ => "short by " + Money.Format(-DifferencePaisa.Value)
    };

    public bool IsShort => DifferencePaisa < 0;

    public bool IsExact => DifferencePaisa == 0;

    /// <summary>Sidebar line: "Opened 21 Oct 09:00 by Owner".</summary>
    public string HeaderLabel => IsOpen
        ? "Opened " + OpenedLabel + " by " + OpenedBy
        : "Closed - opened " + OpenedLabel + " by " + OpenedBy;

    public string NoteLabel => string.IsNullOrWhiteSpace(Note) ? "-" : Note!;
}
