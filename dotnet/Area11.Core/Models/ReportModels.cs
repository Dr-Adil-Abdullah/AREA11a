using Area11.Core.Utils;
using System.Globalization;

namespace Area11.Core.Models;

/// <summary>The four periods the report page offers (Spec 21.1: today, 7 days, 30 days, this month).</summary>
public enum ReportRange
{
    Today = 0,
    Last7 = 1,
    Last30 = 2,
    ThisMonth = 3
}

/// <summary>
/// The dates a report covers. Kept as text in the same `yyyy-MM-dd` form the database stores, so a
/// range compare in SQL is a plain string compare and never a culture-dependent date parse.
/// </summary>
public sealed class ReportPeriod
{
    public ReportRange Range { get; init; }

    public string Title { get; init; } = string.Empty;

    /// <summary>First day, inclusive (`2026-10-21`).</summary>
    public string FromDate { get; init; } = string.Empty;

    /// <summary>Last day, inclusive (`2026-10-21`).</summary>
    public string ToDate { get; init; } = string.Empty;

    /// <summary>`2026-10-21 00:00:00` - the stamp a query compares `date &gt;=` against.</summary>
    public string FromStamp => FromDate + " 00:00:00";

    /// <summary>The first instant AFTER the period, so `date &lt; @until` includes the whole last day.</summary>
    public string UntilStamp { get; init; } = string.Empty;

    /// <summary>How many days the period covers, both ends counted (whole days, so no rounding).</summary>
    public int Days => (DateTime.ParseExact(ToDate, "yyyy-MM-dd", CultureInfo.InvariantCulture)
                        - DateTime.ParseExact(FromDate, "yyyy-MM-dd", CultureInfo.InvariantCulture)).Days + 1;

    public string Label => FromDate == ToDate
        ? FromDate
        : FromDate + "  to  " + ToDate + "  (" + Days.ToString(CultureInfo.InvariantCulture) + " days)";
}

/// <summary>
/// The KPI row of the report (Spec 21.2). Every field carries its own label so the view binds text and
/// never formats money - the same rule the rest of the app follows.
/// </summary>
public sealed class ReportTotals
{
    public int Bills { get; set; }

    public long ItemsBase { get; set; }

    public long SalesPaisa { get; set; }

    public long DiscountPaisa { get; set; }

    public long RefundPaisa { get; set; }

    /// <summary>Cost of the goods that left the shelf in this period.</summary>
    public long CostPaisa { get; set; }

    /// <summary>Cost of returned goods that really came back to the shelf - it must not count as a loss.</summary>
    public long RestockCostPaisa { get; set; }

    /// <summary>Spec 21.6: (sales - refunds) - (cost - cost of restocked returns).</summary>
    public long ProfitPaisa { get; set; }

    public double MarginPercent { get; set; }

    public long AverageBillPaisa { get; set; }

    public long CashPaisa { get; set; }

    public long CreditPaisa { get; set; }

    public long ExpensesPaisa { get; set; }

    public long DrawingsCashPaisa { get; set; }

    public long PurchasesPaisa { get; set; }

    /// <summary>Profit after the shop's own expenses and the cash the owner took (owner only, Spec 21.5).</summary>
    public long NetPaisa { get; set; }

    public long NetSalesPaisa => SalesPaisa - RefundPaisa;

    public string BillsText => Bills.ToString(CultureInfo.InvariantCulture);

    public string ItemsText => ItemsBase.ToString(CultureInfo.InvariantCulture);

    public string SalesText => Money.Format(SalesPaisa);

    public string DiscountText => Money.Format(DiscountPaisa);

    public string RefundText => Money.Format(RefundPaisa);

    public string CostText => Money.Format(CostPaisa);

    public string ProfitText => Money.Format(ProfitPaisa);

    public string NetText => Money.Format(NetPaisa);

    public string AverageBillText => Money.Format(AverageBillPaisa);

    public string CashText => Money.Format(CashPaisa);

    public string CreditText => Money.Format(CreditPaisa);

    public string ExpensesText => Money.Format(ExpensesPaisa);

    public string DrawingsText => Money.Format(DrawingsCashPaisa);

    public string PurchasesText => Money.Format(PurchasesPaisa);

    public string MarginText => MarginPercent.ToString("0.0", CultureInfo.InvariantCulture) + " %";
}

/// <summary>One bar of the per-day chart: the day, its money, and how wide the bar should be.</summary>
public sealed class ReportDayRow
{
    public string Date { get; set; } = string.Empty;

    public int Bills { get; set; }

    public long SalesPaisa { get; set; }

    public long ProfitPaisa { get; set; }

    /// <summary>0..1 of the busiest day in this period - the view multiplies it by its own width.</summary>
    public double BarShare { get; set; }

    public bool HasSales => SalesPaisa != 0;

    /// <summary>The day as the owner writes it: `21 Oct`.</summary>
    public string DayLabel
    {
        get
        {
            if (!DateTime.TryParseExact(Date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var day))
            {
                return Date;
            }

            return day.ToString("dd MMM", CultureInfo.InvariantCulture);
        }
    }

    public string SalesLabel => Money.Format(SalesPaisa);

    public string ProfitLabel => Money.Format(ProfitPaisa);
}

/// <summary>A row of the top-10 medicine list, by quantity or by profit (Spec 21.3).</summary>
public sealed class ReportItemRow
{
    public int Rank { get; set; }

    public int ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public double QtyBase { get; set; }

    public long SalesPaisa { get; set; }

    public long ProfitPaisa { get; set; }

    public string RankLabel => Rank.ToString(CultureInfo.InvariantCulture);

    public string QtyLabel => Qty.Format(QtyBase);

    public string SalesLabel => Money.Format(SalesPaisa);

    public string ProfitLabel => Money.Format(ProfitPaisa);
}

/// <summary>One slice of the category split.</summary>
public sealed class ReportCategoryRow
{
    public string Name { get; set; } = string.Empty;

    public long SalesPaisa { get; set; }

    public double SharePercent { get; set; }

    public string SalesLabel => Money.Format(SalesPaisa);

    public string ShareLabel => SharePercent.ToString("0.0", CultureInfo.InvariantCulture) + " %";
}

/// <summary>Medicine that has not moved for two months but is still on the shelf (Spec 21.3).</summary>
public sealed class ReportDeadRow
{
    public int ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public long StockBase { get; set; }

    public long ValuePaisa { get; set; }

    public string? LastSold { get; set; }

    public string StockLabel => StockBase.ToString(CultureInfo.InvariantCulture);

    public string ValueLabel => Money.Format(ValuePaisa);

    public string LastSoldLabel => string.IsNullOrWhiteSpace(LastSold)
        ? "never sold"
        : LastSold!.Length >= 10 ? LastSold![..10] : LastSold!;
}

/// <summary>Everything the report page shows, built in one call so a screen never half-updates.</summary>
public sealed class SalesReport
{
    public ReportPeriod Period { get; init; } = new();

    public ReportTotals Totals { get; init; } = new();

    public List<ReportDayRow> Days { get; init; } = new();

    public List<ReportItemRow> TopByQty { get; init; } = new();

    public List<ReportItemRow> TopByProfit { get; init; } = new();

    public List<ReportCategoryRow> Categories { get; init; } = new();

    public List<ReportDeadRow> DeadStock { get; init; } = new();

    /// <summary>False for a manager or a cashier: the net profit is the owner's (Spec 12.1.2, D-47).</summary>
    public bool CanSeeProfit { get; init; }

    public int DeadStockDays { get; init; } = 60;

    public string BestDayLabel { get; init; } = "no sales yet";

    public string WorstDayLabel { get; init; } = "no sales yet";

    public bool HasDays => Days.Count > 0;

    public bool HasTop => TopByQty.Count > 0;

    public bool HasCategories => Categories.Count > 0;

    public bool HasDeadStock => DeadStock.Count > 0;

    public string PeriodLabel => Period.Label;

    public string DeadStockTitle => "Dead stock - no sale for " + DeadStockDays.ToString(CultureInfo.InvariantCulture) + " days";

    public string ProfitNote => CanSeeProfit
        ? "Profit = sales - refunds - cost of the goods sold (plus the cost of goods that came back to the shelf)."
        : "Net profit is hidden here: it belongs to the owner (Spec 12.1.2).";
}
