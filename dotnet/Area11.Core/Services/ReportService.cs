using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace Area11.Core.Services;

/// <summary>
/// The reports page (Spec 21, F-34): what the shop sold, what it earned, what it is still carrying.
///
/// The one rule that needs care is **profit** (Spec 21.6):
///   profit = (sales - refunds) - (cost of goods sold - cost of the goods that came BACK to the shelf)
///
/// A returned strip that is back on the shelf is no longer a loss; a returned strip that went to
/// quarantine or the bin **is** a loss. Both numbers come from `sale_returns.restock`, the same flag
/// the return service writes, so the report and the till can never disagree.
///
/// Every money value is integer paisa from the database and is only formatted at the very edge, the
/// same rule as the rest of the app (no floating rupee arithmetic anywhere).
/// </summary>
public sealed class ReportService
{
    /// <summary>Spec 21.3: a medicine with no sale for 60 days is dead stock.</summary>
    public const int DeadStockDays = 60;

    /// <summary>Spec 21.3: the two lists show ten medicines each.</summary>
    public const int TopCount = 10;

    private readonly Database _db;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    public ReportService(Database db, PermissionService permissions, LogService log)
    {
        _db = db;
        _permissions = permissions;
        _log = log;
    }

    /// <summary>The four periods, in the order the buttons appear.</summary>
    public static IReadOnlyList<ReportRange> Ranges { get; } = new[]
    {
        ReportRange.Today, ReportRange.Last7, ReportRange.Last30, ReportRange.ThisMonth
    };

    public static string RangeLabel(ReportRange range) => range switch
    {
        ReportRange.Today => "Today",
        ReportRange.Last7 => "7 days",
        ReportRange.Last30 => "30 days",
        _ => "This month"
    };

    /// <summary>
    /// Turns a button into the dates it means. "7 days" counts today plus the six before it, which is
    /// what a shopkeeper means by "this week" - not seven whole days from yesterday.
    /// </summary>
    public ReportPeriod Period(ReportRange range)
    {
        var today = DateTime.ParseExact(Clock.Today(), "yyyy-MM-dd", CultureInfo.InvariantCulture);

        var from = range switch
        {
            ReportRange.Today => today,
            ReportRange.Last7 => today.AddDays(-6),
            ReportRange.Last30 => today.AddDays(-29),
            _ => new DateTime(today.Year, today.Month, 1)
        };

        var title = range switch
        {
            ReportRange.Today => "Today",
            ReportRange.Last7 => "The last 7 days",
            ReportRange.Last30 => "The last 30 days",
            _ => "This month"
        };

        return new ReportPeriod
        {
            Range = range,
            Title = title,
            FromDate = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ToDate = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            UntilStamp = today.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " 00:00:00"
        };
    }

    /// <summary>Builds one report. A cashier is refused; a manager sees everything except the profit.</summary>
    public ServiceResult<SalesReport> Build(ReportRange range, User? user)
    {
        if (!_permissions.Can(user, Modules.Reports))
        {
            return ServiceResult<SalesReport>.Fail("You do not have permission to see the reports.");
        }

        try
        {
            var canSeeProfit = _permissions.Can(user, Modules.Profit);
            var period = Period(range);

            using var connection = _db.Open();

            var totals = ReadTotals(connection, period, canSeeProfit);
            var days = ReadDays(connection, period, canSeeProfit);

            var report = new SalesReport
            {
                Period = period,
                Totals = totals,
                Days = days,
                TopByQty = ReadTop(connection, period, byProfit: false, canSeeProfit: canSeeProfit),
                TopByProfit = canSeeProfit
                    ? ReadTop(connection, period, byProfit: true, canSeeProfit: true)
                    : new List<ReportItemRow>(),
                Categories = ReadCategories(connection, period),
                DeadStock = ReadDeadStock(connection),
                CanSeeProfit = canSeeProfit,
                DeadStockDays = ReportService.DeadStockDays,
                BestDayLabel = Extremes(connection, best: true),
                WorstDayLabel = Extremes(connection, best: false)
            };

            return ServiceResult<SalesReport>.Success(report);
        }
        catch (Exception exception)
        {
            _log.Error("The report could not be built.", exception);
            return ServiceResult<SalesReport>.Fail("The report could not be read: " + exception.Message);
        }
    }

    /// <summary>One row per day, for a future printed sheet. Opens and closes its own connection.</summary>
    public List<ReportDayRow> Days(ReportRange range)
    {
        using var connection = _db.Open();
        return ReadDays(connection, Period(range), canSeeProfit: true);
    }

    // ------------------------------------------------------------------ reading

    private ReportTotals ReadTotals(SqliteConnection connection, ReportPeriod period, bool canSeeProfit)
    {
        var totals = _db.QuerySingle<TotalsRow>(connection, @"
SELECT COUNT(1) AS bills,
       IFNULL(SUM(total_paisa), 0) AS sales,
       IFNULL(SUM(discount_paisa), 0) AS discount,
       IFNULL(SUM(due_paisa), 0) AS credit
FROM sales
WHERE status IN ('paid','partial','credit') AND date >= @from AND date < @until;",
            new { from = period.FromStamp, until = period.UntilStamp }) ?? new TotalsRow();

        var items = _db.QuerySingle<ItemsRow>(connection, @"
SELECT IFNULL(SUM(si.qty_base), 0) AS items,
       CAST(IFNULL(SUM(si.qty_base * si.cost_paisa_at_sale), 0) AS INTEGER) AS cost
FROM sale_items si
JOIN sales s ON s.id = si.sale_id
WHERE s.status IN ('paid','partial','credit') AND s.date >= @from AND s.date < @until;",
            new { from = period.FromStamp, until = period.UntilStamp }) ?? new ItemsRow();

        // Scalar, not QuerySingle<long>: `T?` on an unconstrained type parameter is only an
        // annotation, so the long would stay a long and `?? 0` would not compile (v0.3.7 lesson).
        var cash = Convert.ToInt64(_db.Scalar(connection, @"
SELECT IFNULL(SUM(p.amount_paisa), 0)
FROM payments p
JOIN sales s ON s.id = p.sale_id
WHERE p.method = 'cash' AND s.date >= @from AND s.date < @until;",
            new { from = period.FromStamp, until = period.UntilStamp }) ?? 0L);

        // A return belongs to the day the money went back, not to the day of the bill - the same rule
        // the drawer uses (Spec 15.6).
        var returns = _db.QuerySingle<ReturnsRow>(connection, @"
SELECT IFNULL(SUM(r.refund_paisa), 0) AS refunds,
       CAST(IFNULL(SUM(CASE WHEN r.restock = 1 THEN r.qty_base * si.cost_paisa_at_sale ELSE 0 END), 0) AS INTEGER) AS restockCost
FROM sale_returns r
JOIN sale_items si ON si.id = r.sale_item_id
WHERE r.date >= @from AND r.date < @until;",
            new { from = period.FromStamp, until = period.UntilStamp }) ?? new ReturnsRow();

        var outflow = _db.QuerySingle<OutflowRow>(connection, @"
SELECT (SELECT IFNULL(SUM(amount_paisa), 0) FROM expenses
        WHERE date >= @from AND date < @until) AS expenses,
       (SELECT IFNULL(SUM(amount_paisa), 0) FROM owner_drawings
        WHERE type = 'cash' AND date >= @from AND date < @until) AS drawings,
       (SELECT IFNULL(SUM(total_paisa), 0) FROM purchases
        WHERE date >= @from AND date < @until) AS purchases;",
            new { from = period.FromStamp, until = period.UntilStamp }) ?? new OutflowRow();

        var netSales = totals.Sales - returns.Refunds;
        var cost = items.Cost - returns.RestockCost;
        var profit = netSales - cost;

        var result = new ReportTotals
        {
            Bills = totals.Bills,
            ItemsBase = items.Items,
            SalesPaisa = totals.Sales,
            DiscountPaisa = totals.Discount,
            RefundPaisa = returns.Refunds,
            CostPaisa = items.Cost,
            RestockCostPaisa = returns.RestockCost,
            ProfitPaisa = canSeeProfit ? profit : 0,
            MarginPercent = canSeeProfit && netSales > 0 ? profit * 100.0 / netSales : 0,
            AverageBillPaisa = totals.Bills > 0 ? totals.Sales / totals.Bills : 0,
            CashPaisa = cash,
            CreditPaisa = totals.Credit,
            ExpensesPaisa = outflow.Expenses,
            DrawingsCashPaisa = outflow.Drawings,
            PurchasesPaisa = outflow.Purchases,
            NetPaisa = canSeeProfit ? profit - outflow.Expenses - outflow.Drawings : 0
        };

        return result;
    }

    /// <summary>
    /// One row per day in the period, including the days with no sale: a chart with holes would make
    /// a quiet Sunday look like a crash. Profit per day uses the same formula as the totals.
    /// </summary>
    private List<ReportDayRow> ReadDays(SqliteConnection connection, ReportPeriod period, bool canSeeProfit)
    {
        var sales = _db.Query<DaySalesRow>(connection, @"
SELECT substr(date, 1, 10) AS date, COUNT(1) AS bills, IFNULL(SUM(total_paisa), 0) AS sales
FROM sales
WHERE status IN ('paid','partial','credit') AND date >= @from AND date < @until
GROUP BY substr(date, 1, 10);",
            new { from = period.FromStamp, until = period.UntilStamp });

        var costs = _db.Query<DayCostRow>(connection, @"
SELECT substr(s.date, 1, 10) AS date,
       CAST(IFNULL(SUM(si.qty_base * si.cost_paisa_at_sale), 0) AS INTEGER) AS cost
FROM sale_items si
JOIN sales s ON s.id = si.sale_id
WHERE s.status IN ('paid','partial','credit') AND s.date >= @from AND s.date < @until
GROUP BY substr(s.date, 1, 10);",
            new { from = period.FromStamp, until = period.UntilStamp });

        var refunds = _db.Query<DayReturnRow>(connection, @"
SELECT substr(r.date, 1, 10) AS date,
       IFNULL(SUM(r.refund_paisa), 0) AS refunds,
       CAST(IFNULL(SUM(CASE WHEN r.restock = 1 THEN r.qty_base * si.cost_paisa_at_sale ELSE 0 END), 0) AS INTEGER) AS restockCost
FROM sale_returns r
JOIN sale_items si ON si.id = r.sale_item_id
WHERE r.date >= @from AND r.date < @until
GROUP BY substr(r.date, 1, 10);",
            new { from = period.FromStamp, until = period.UntilStamp });

        var salesByDay = sales.ToDictionary(row => row.Date ?? string.Empty, row => row);
        var costByDay = costs.ToDictionary(row => row.Date ?? string.Empty, row => row.Cost);
        var refundByDay = refunds.ToDictionary(row => row.Date ?? string.Empty, row => row);

        var first = DateTime.ParseExact(period.FromDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var last = DateTime.ParseExact(period.ToDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);

        var rows = new List<ReportDayRow>();

        for (var day = first; day <= last; day = day.AddDays(1))
        {
            var key = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            salesByDay.TryGetValue(key, out var sale);
            costByDay.TryGetValue(key, out var cost);
            refundByDay.TryGetValue(key, out var refund);

            var salesPaisa = sale?.Sales ?? 0;
            var refundPaisa = refund?.Refunds ?? 0;
            var restockCost = refund?.RestockCost ?? 0;

            rows.Add(new ReportDayRow
            {
                Date = key,
                Bills = sale?.Bills ?? 0,
                SalesPaisa = salesPaisa,
                ProfitPaisa = canSeeProfit ? salesPaisa - refundPaisa - (cost - restockCost) : 0
            });
        }

        // The bar is relative to the busiest day of THIS period, so a quiet week still shows shape.
        var busiest = rows.Count == 0 ? 0 : rows.Max(row => row.SalesPaisa);

        foreach (var row in rows)
        {
            row.BarShare = busiest <= 0 ? 0 : (double)row.SalesPaisa / busiest;
        }

        return rows;
    }

    /// <summary>
    /// The top ten, by quantity or by profit. Two literal queries instead of one with the ORDER BY
    /// pasted in: SQL built by string joining is the one habit that turns a report into a risk, and
    /// `tools/check_repo.py` prepares every statement it finds - a placeholder would fail the check.
    ///
    /// **Net of returns.** A strip that came back to the shelf is not a sale any more: both the
    /// quantity and the money are lowered, and the cost of the restocked strip is taken off as well.
    /// A strip that went to quarantine is a loss, so only its refund is taken off - the cost stays.
    /// That is the same rule as the totals (Spec 21.6), so the two can never contradict each other.
    /// </summary>
    private List<ReportItemRow> ReadTop(
        SqliteConnection connection, ReportPeriod period, bool byProfit, bool canSeeProfit)
    {
        const string soldAndCameBack = @"
WITH sold AS (
    SELECT si.product_id AS productId,
           SUM(si.qty_base) AS qty,
           SUM(si.line_total_paisa) AS sales,
           SUM(si.qty_base * si.cost_paisa_at_sale) AS cost
    FROM sale_items si
    JOIN sales s ON s.id = si.sale_id
    WHERE s.status IN ('paid','partial','credit') AND s.date >= @from AND s.date < @until
    GROUP BY si.product_id
),
came AS (
    SELECT r.product_id AS productId,
           SUM(CASE WHEN r.restock = 1 THEN r.qty_base ELSE 0 END) AS qty,
           SUM(r.refund_paisa) AS refunds,
           SUM(CASE WHEN r.restock = 1 THEN r.qty_base * si.cost_paisa_at_sale ELSE 0 END) AS restockCost
    FROM sale_returns r
    JOIN sale_items si ON si.id = r.sale_item_id
    WHERE r.date >= @from AND r.date < @until
    GROUP BY r.product_id
)
SELECT sold.productId AS productId,
       IFNULL(p.name, 'Item #' || sold.productId) AS name,
       CAST(sold.qty - IFNULL(came.qty, 0) AS INTEGER) AS qty,
       CAST(sold.sales - IFNULL(came.refunds, 0) AS INTEGER) AS sales,
       CAST((sold.sales - sold.cost) - (IFNULL(came.refunds, 0) - IFNULL(came.restockCost, 0)) AS INTEGER) AS profitPaisa
FROM sold
LEFT JOIN came ON came.productId = sold.productId
LEFT JOIN products p ON p.id = sold.productId
";

        const string byQuantity = soldAndCameBack + "ORDER BY qty DESC, sales DESC LIMIT @top;";

        const string byProfitSql = soldAndCameBack + "ORDER BY profitPaisa DESC, sales DESC LIMIT @top;";

        var rows = _db.Query<ItemRow>(connection, byProfit ? byProfitSql : byQuantity,
            new { from = period.FromStamp, until = period.UntilStamp, top = TopCount });

        var rank = 0;

        foreach (var row in rows)
        {
            row.Rank = ++rank;

            if (!canSeeProfit)
            {
                row.ProfitPaisa = 0;
            }
        }

        return rows.Select(row => row.ToRow()).ToList();
    }

    /// <summary>
    /// How the money splits across the shop's categories, net of refunds - the same formula as the
    /// totals (Spec 21.6), so a category with a big return does not look better than it was.
    /// </summary>
    private List<ReportCategoryRow> ReadCategories(SqliteConnection connection, ReportPeriod period)
    {
        var rows = _db.Query<CategoryRow>(connection, @"
WITH net AS (
    SELECT IFNULL(c.name, 'Uncategorised') AS name, SUM(si.line_total_paisa) AS sales
    FROM sale_items si
    JOIN sales s ON s.id = si.sale_id
    LEFT JOIN products p ON p.id = si.product_id
    LEFT JOIN categories c ON c.id = p.category_id
    WHERE s.status IN ('paid','partial','credit') AND s.date >= @from AND s.date < @until
    GROUP BY IFNULL(c.name, 'Uncategorised')
),
back AS (
    SELECT IFNULL(c.name, 'Uncategorised') AS name, SUM(r.refund_paisa) AS refunds
    FROM sale_returns r
    LEFT JOIN products p ON p.id = r.product_id
    LEFT JOIN categories c ON c.id = p.category_id
    WHERE r.date >= @from AND r.date < @until
    GROUP BY IFNULL(c.name, 'Uncategorised')
)
SELECT net.name AS name, CAST(net.sales - IFNULL(back.refunds, 0) AS INTEGER) AS sales
FROM net
LEFT JOIN back ON back.name = net.name
ORDER BY sales DESC;",
            new { from = period.FromStamp, until = period.UntilStamp });

        var total = rows.Sum(row => row.Sales);

        return rows.Select(row => new ReportCategoryRow
        {
            Name = row.Name,
            SalesPaisa = row.Sales,
            SharePercent = total > 0 ? Math.Max(0, row.Sales) * 100.0 / total : 0
        }).ToList();
    }

    /// <summary>
    /// Stock that has not moved for 60 days. "Has stock" means the batches on the shelf add up to more
    /// than zero, so a product with negative book stock is not called dead - that is a different alert.
    /// </summary>
    private List<ReportDeadRow> ReadDeadStock(SqliteConnection connection)
    {
        var cutoff = DateTime.ParseExact(Clock.Today(), "yyyy-MM-dd", CultureInfo.InvariantCulture)
            .AddDays(-DeadStockDays)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " 00:00:00";

        var rows = _db.Query<DeadRow>(connection, @"
SELECT p.id AS productId, p.name AS name,
       IFNULL((SELECT SUM(b.qty_base) FROM batches b WHERE b.product_id = p.id AND b.active = 1), 0) AS stockBase,
       CAST(IFNULL((SELECT SUM(b.qty_base * b.cost_paisa) FROM batches b
                    WHERE b.product_id = p.id AND b.active = 1), 0) AS INTEGER) AS valuePaisa,
       (SELECT MAX(s.date) FROM sale_items si JOIN sales s ON s.id = si.sale_id
        WHERE si.product_id = p.id AND s.status IN ('paid','partial','credit')) AS lastSold
FROM products p
WHERE p.active = 1
  AND IFNULL((SELECT SUM(b.qty_base) FROM batches b WHERE b.product_id = p.id AND b.active = 1), 0) > 0
  AND NOT EXISTS (SELECT 1 FROM sale_items si JOIN sales s ON s.id = si.sale_id
                  WHERE si.product_id = p.id AND s.status IN ('paid','partial','credit') AND s.date >= @cutoff)
ORDER BY valuePaisa DESC, p.name COLLATE NOCASE
LIMIT 50;",
            new { cutoff });

        return rows.Select(row => row.ToRow()).ToList();
    }

    /// <summary>Spec 21.4: the best and the worst selling day the shop has ever had.</summary>
    private string Extremes(SqliteConnection connection, bool best)
    {
        const string bestDay = @"
SELECT substr(date, 1, 10) AS date, COUNT(1) AS bills, IFNULL(SUM(total_paisa), 0) AS sales
FROM sales
WHERE status IN ('paid','partial','credit')
GROUP BY substr(date, 1, 10)
ORDER BY sales DESC, date ASC
LIMIT 1;";

        const string worstDay = @"
SELECT substr(date, 1, 10) AS date, COUNT(1) AS bills, IFNULL(SUM(total_paisa), 0) AS sales
FROM sales
WHERE status IN ('paid','partial','credit')
GROUP BY substr(date, 1, 10)
ORDER BY sales ASC, date ASC
LIMIT 1;";

        var row = _db.QuerySingle<DaySalesRow>(connection, best ? bestDay : worstDay);

        if (row?.Date is null || row.Date.Length < 10)
        {
            return "no sales yet";
        }

        return ExtremesLabel(row.Date, row.Sales);
    }

    private static string ExtremesLabel(string date, long paisa)
    {
        var day = DateTime.ParseExact(date[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture);

        return day.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) + " - " + Money.Format(paisa);
    }

    // ------------------------------------------------------------------ the rows Dapper fills

    private sealed class TotalsRow
    {
        public int Bills { get; set; }

        public long Sales { get; set; }

        public long Discount { get; set; }

        public long Credit { get; set; }
    }

    private sealed class ItemsRow
    {
        public long Items { get; set; }

        public long Cost { get; set; }
    }

    private sealed class ReturnsRow
    {
        public long Refunds { get; set; }

        public long RestockCost { get; set; }
    }

    private sealed class OutflowRow
    {
        public long Expenses { get; set; }

        public long Drawings { get; set; }

        public long Purchases { get; set; }
    }

    private sealed class DaySalesRow
    {
        public string? Date { get; set; }

        public int Bills { get; set; }

        public long Sales { get; set; }
    }

    private sealed class DayCostRow
    {
        public string? Date { get; set; }

        public long Cost { get; set; }
    }

    private sealed class DayReturnRow
    {
        public string? Date { get; set; }

        public long Refunds { get; set; }

        public long RestockCost { get; set; }
    }

    private sealed class ItemRow
    {
        public int ProductId { get; set; }

        public string? Name { get; set; }

        public long Qty { get; set; }

        public long Sales { get; set; }

        public long ProfitPaisa { get; set; }

        public int Rank { get; set; }

        public ReportItemRow ToRow() => new()
        {
            Rank = Rank,
            ProductId = ProductId,
            Name = string.IsNullOrWhiteSpace(Name) ? "Unknown" : Name!,
            QtyBase = Qty,
            SalesPaisa = Sales,
            ProfitPaisa = ProfitPaisa
        };
    }

    private sealed class CategoryRow
    {
        public string Name { get; set; } = "Uncategorised";

        public long Sales { get; set; }
    }

    private sealed class DeadRow
    {
        public int ProductId { get; set; }

        public string Name { get; set; } = string.Empty;

        public long StockBase { get; set; }

        public long ValuePaisa { get; set; }

        public string? LastSold { get; set; }

        public ReportDeadRow ToRow() => new()
        {
            ProductId = ProductId,
            Name = Name,
            StockBase = StockBase,
            ValuePaisa = ValuePaisa,
            LastSold = LastSold
        };
    }
}
