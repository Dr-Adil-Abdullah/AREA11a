using System.Globalization;
using Area11.Core.Utils;

namespace Area11.Core.Models;

/// <summary>
/// One bill as it appears in the sales history list (F-52, Spec 12.3).
///
/// Why this page exists at all: the counter prints a bill once. A customer comes back a week later for a
/// copy, an accountant wants last month's bills, and a return has to be checked against the bill it came
/// from - none of that is possible from the counter, which only knows the cart in front of it.
/// </summary>
public sealed class SaleHistoryRow
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    /// <summary>Stored as the shop's own text ("2026-10-26 14:05:00").</summary>
    public string Date { get; set; } = string.Empty;

    public string Status { get; set; } = "paid";

    /// <summary>cash | credit | online | card | split</summary>
    public string Method { get; set; } = "cash";

    public string Customer { get; set; } = string.Empty;

    public int Items { get; set; }

    /// <summary>How many return receipts hang from this bill.</summary>
    public int Returns { get; set; }

    public long SubtotalPaisa { get; set; }

    public long DiscountPaisa { get; set; }

    public long TaxPaisa { get; set; }

    public long RoundOffPaisa { get; set; }

    public long TotalPaisa { get; set; }

    public long PaidPaisa { get; set; }

    public long DuePaisa { get; set; }

    public string TotalLabel => Money.Format(TotalPaisa);

    public string DueLabel => DuePaisa == 0 ? "-" : Money.Format(DuePaisa);

    /// <summary>"26 Oct 2026, 2:05 pm" - a date the owner reads without counting digits.</summary>
    public string WhenLabel
    {
        get
        {
            if (DateTime.TryParse(Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var at))
            {
                return at.ToString("dd MMM yyyy, h:mm tt", CultureInfo.InvariantCulture);
            }

            return Date.Length >= 16 ? Date[..16] : Date;
        }
    }

    public string CustomerLabel => Customer.Length == 0 ? "Walking customer" : Customer;

    public string ItemsLabel => Items.ToString(CultureInfo.InvariantCulture)
                                + (Items == 1 ? " line" : " lines");

    public string StatusLabel => Status switch
    {
        "paid" => "Paid",
        "partial" => "Part paid",
        "credit" => "Khata",
        "hold" => "Held",
        "void" => "Void",
        _ => Status
    };

    public string MethodLabel => SaleHistoryLabels.Method(Method);

    public bool HasDue => DuePaisa > 0;

    public bool HasReturns => Returns > 0;
}

/// <summary>One line of a bill - exactly what the paper shows, so a reprint and the screen agree.</summary>
public sealed class SaleHistoryLine
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public double QtyEntered { get; set; }

    public string UnitSold { get; set; } = "base";

    public double QtyBase { get; set; }

    public long UnitPricePaisa { get; set; }

    public long DiscountPaisa { get; set; }

    public long LineTotalPaisa { get; set; }

    /// <summary>"2 box" - the pack the customer actually bought (Spec 9.3).</summary>
    public string QtyLabel => Qty.Format(QtyEntered) + " " + UnitSold;

    public string RateLabel => Money.Format(UnitPricePaisa);

    public string TotalLabel => Money.Format(LineTotalPaisa);

    public bool HasDiscount => DiscountPaisa > 0;

    public string DiscountLabel => Money.Format(DiscountPaisa);
}

/// <summary>Money that came in against the bill (cash, card, khata payment).</summary>
public sealed class SaleHistoryPayment
{
    public string Date { get; set; } = string.Empty;

    public string Method { get; set; } = "cash";

    public long AmountPaisa { get; set; }

    public string Note { get; set; } = string.Empty;

    public string WhenLabel => Date.Length >= 16 ? Date[..16] : Date;

    public string MethodLabel => SaleHistoryLabels.Method(Method);

    public string AmountLabel => Money.Format(AmountPaisa);
}

/// <summary>
/// A return that belongs to this bill. Its number is what the return receipt is printed from, which is the
/// whole reason the bill page shows it (Spec 14.3.2).
/// </summary>
public sealed class SaleHistoryReturn
{
    public string Code { get; set; } = string.Empty;

    public string Date { get; set; } = string.Empty;

    public long RefundPaisa { get; set; }

    public long CashPaisa { get; set; }

    public long CreditPaisa { get; set; }

    public double QtyEntered { get; set; }

    public string UnitSold { get; set; } = "base";

    public int Restock { get; set; }

    public string WhenLabel => Date.Length >= 16 ? Date[..16] : Date;

    public string AmountLabel => Money.Format(RefundPaisa);

    /// <summary>"2 box back to the shelf" / "2 box to quarantine" - where the medicine went (D-51).</summary>
    public string WhereLabel => Qty.Format(QtyEntered) + " " + UnitSold
                                + (Restock != 0 ? " back to the shelf" : " to quarantine");

    public string MoneyLabel => CashPaisa > 0
        ? Money.Format(CashPaisa) + " cash back"
        : CreditPaisa > 0 ? Money.Format(CreditPaisa) + " off the khata" : "no money moved";

    public bool CanPrint => Code.Length > 0;
}

/// <summary>What the list is asked for: the two dates, a word, and two drop-downs (Spec 12.3.2).</summary>
public sealed class SaleHistoryFilter
{
    public string Search { get; set; } = string.Empty;

    /// <summary>yyyy-MM-dd, inclusive. Empty means "no lower limit".</summary>
    public string From { get; set; } = string.Empty;

    /// <summary>yyyy-MM-dd, inclusive. Empty means "today".</summary>
    public string To { get; set; } = string.Empty;

    /// <summary>Empty means every method.</summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>Empty means every status.</summary>
    public string Status { get; set; } = string.Empty;
}

/// <summary>One bill, opened: its lines, its money and its returns.</summary>
public sealed class SaleHistoryDetail
{
    public SaleHistoryRow Sale { get; set; } = new();

    public List<SaleHistoryLine> Lines { get; } = new();

    public List<SaleHistoryPayment> Payments { get; } = new();

    public List<SaleHistoryReturn> Returns { get; } = new();

    public string SubtotalLabel => Money.Format(Sale.SubtotalPaisa);

    public string DiscountLabel => Money.Format(Sale.DiscountPaisa);

    public string TaxLabel => Money.Format(Sale.TaxPaisa);

    /// <summary>"-Rs 1" when the bill was rounded down, "+Rs 2" when up, "-" when nothing was rounded.</summary>
    public string RoundOffLabel => Sale.RoundOffPaisa == 0 ? "-" : Money.Format(Sale.RoundOffPaisa);

    public string TotalLabel => Sale.TotalLabel;

    public string PaidLabel => Money.Format(Sale.PaidPaisa);

    public string DueLabel => Sale.DueLabel;

    public bool HasDiscount => Sale.DiscountPaisa > 0;

    public bool HasTax => Sale.TaxPaisa != 0;

    public bool HasRoundOff => Sale.RoundOffPaisa != 0;

    public bool HasReturns => Returns.Count > 0;

    public bool HasPayments => Payments.Count > 0;

    public string TitleLabel => Sale.Code + " - " + Sale.WhenLabel + " - " + Sale.CustomerLabel;
}

/// <summary>The whole answer of one search: rows, and the totals the owner checks them against.</summary>
public sealed class SaleHistoryPage
{
    public List<SaleHistoryRow> Rows { get; } = new();

    /// <summary>True when the filter was refused (a bad date, a reversed range) - the page says why.</summary>
    public string? FilterProblem { get; set; }

    public bool IsTruncated { get; set; }

    public int Count => Rows.Count;

    public long TotalPaisa => Rows.Sum(row => row.TotalPaisa);

    public long DuePaisa => Rows.Sum(row => row.DuePaisa);

    public bool IsEmpty => Rows.Count == 0;

    public string CountLabel => Count.ToString(CultureInfo.InvariantCulture)
                                + (Count == 1 ? " bill" : " bills")
                                + (IsTruncated ? " (first 500 - narrow the dates)" : string.Empty);

    public string TotalLabel => Money.Format(TotalPaisa);

    public string DueLabel => Money.Format(DuePaisa);
}

/// <summary>The two words the page and the models must say the same way.</summary>
public static class SaleHistoryLabels
{
    public static string Method(string? method) => (method ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "cash" => "Cash",
        "credit" => "Khata",
        "online" => "Online",
        "card" => "Card",
        "split" => "Split",
        "" => "-",
        _ => method!.Trim()
    };
}
