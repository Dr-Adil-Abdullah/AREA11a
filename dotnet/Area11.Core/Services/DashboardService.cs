using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>
/// The home screen's numbers (F-12).
///
/// Why every count here is spelled out instead of reusing an alert or a report: the dashboard has no
/// drill-down. If a card says "3", the owner must be able to find those exact three rows somewhere -
/// the reorder pad, the expiry page, the khata book - and be told the same number there. Two different
/// definitions of "low stock" is how a shop learns to stop believing its own home screen (D-75).
/// </summary>
public sealed class DashboardSnapshot
{
    public int Products { get; init; }

    /// <summary>Watched medicines at or below their minimum - the same rows the reorder pad lists.</summary>
    public int LowStock { get; init; }

    public int NegativeStock { get; init; }

    /// <summary>Batches with stock left whose expiry is inside the owner's nearest band (Level 3).</summary>
    public int ExpiringSoon { get; init; }

    public int Expired { get; init; }

    public int TodayBills { get; init; }

    public long TodaySalesPaisa { get; init; }

    /// <summary>
    /// Today's profit, by the shop's own rule (Spec 21.6), and 0 when this person may not see profit.
    /// </summary>
    public long TodayProfitPaisa { get; init; }

    /// <summary>False when the signed-in person may not see profit - the screen then hides the card.</summary>
    public bool CanSeeProfit { get; init; }

    public long CustomerCreditPaisa { get; init; }

    public long SupplierPayablePaisa { get; init; }

    public bool ShiftOpen { get; init; }
}

/// <summary>Read-only numbers for the home screen (Spec 21 summary rules).</summary>
public sealed class DashboardService
{
    private readonly Database _db;
    private readonly SettingsService _settings;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    public DashboardService(Database db, SettingsService settings, PermissionService permissions, LogService log)
    {
        _db = db;
        _settings = settings;
        _permissions = permissions;
        _log = log;
    }

    /// <summary>
    /// One snapshot for one person. Profit is the money the shop makes; a cashier has no business
    /// seeing it (the Reports page already hides it behind the same module), so the number is not read
    /// at all unless the permission is there.
    /// </summary>
    public ServiceResult<DashboardSnapshot> GetSnapshot(User? user)
    {
        if (!_permissions.Can(user, Modules.Pos) && !_permissions.Can(user, Modules.Reports))
        {
            return ServiceResult<DashboardSnapshot>.Fail("You do not have permission to see the dashboard.");
        }

        try
        {
            var canSeeProfit = _permissions.Can(user, Modules.Profit);

            using var connection = _db.Open();

            var products = Convert.ToInt32(_db.Scalar(connection,
                "SELECT COUNT(1) FROM products WHERE active = 1;") ?? 0);

            // "Running out" is the RED half of the reorder pad: nothing left at all, or at/below the
            // minimum the owner set. That is `ReorderService.TierFor` (Out + Critical) written in SQL,
            // so the card and the pad always name the same medicines. A medicine with no minimum and a
            // full shelf (level 0, stock 500) is on neither list - nobody asked to watch it.
            //
            // A MINUS shelf is not "running out": it is the other card, because the two need different
            // actions (order more vs. find out where the stock went). The alert board has always split
            // them this way; the home screen now does too (D-75).
            var lowStock = Convert.ToInt32(_db.Scalar(connection, @"
SELECT COUNT(1) FROM products p
WHERE p.active = 1
  AND IFNULL((SELECT SUM(b.qty_base) FROM batches b WHERE b.product_id = p.id AND b.active = 1), 0) >= 0
  AND IFNULL((SELECT SUM(b.qty_base) FROM batches b WHERE b.product_id = p.id AND b.active = 1), 0) <= p.reorder_level;") ?? 0);

            var negative = Convert.ToInt32(_db.Scalar(connection, @"
SELECT COUNT(1) FROM products p
WHERE p.active = 1
  AND IFNULL((SELECT SUM(b.qty_base) FROM batches b WHERE b.product_id = p.id AND b.active = 1), 0) < 0;") ?? 0);

            // "Expiring soon" is the owner's nearest band (Level 3), not a hard-coded three months
            // (F-29): change the band in Settings and this card follows on the next refresh.
            //
            // The comparison is by DAY (`julianday`), never by the stored year-month. A strip that
            // expired on the 3rd of this month has the same `expiry_ym` as one that expires on the
            // 28th, so a month-based count calls the first one "still fine" - and it would stay
            // "still fine" until the calendar turned. The alert board has always counted by day;
            // the home screen now does too, so the bell and the card name the same batches (D-75).
            //
            // `expiry_ym` is only the fallback for a row that carries a month and no date (an old
            // import). A row with a real date never reaches that branch, so nothing is counted twice.
            var days = _settings.ExpiryBands.Level3Days;

            var expiring = Convert.ToInt32(_db.Scalar(connection, @"
SELECT COUNT(1) FROM batches b
JOIN products p ON p.id = b.product_id
WHERE p.active = 1 AND b.active = 1 AND b.qty_base > 0
  AND (
        (IFNULL(b.expiry_date, '') <> ''
             AND julianday(b.expiry_date) - julianday(date('now','localtime')) BETWEEN 0 AND @days)
     OR (IFNULL(b.expiry_date, '') = '' AND b.expiry_ym <> ''
             AND b.expiry_ym >= strftime('%Y-%m', 'now', 'localtime')
             AND b.expiry_ym <= strftime('%Y-%m', 'now', 'localtime', @window))
      );", new { days, window = "+" + days + " days" }) ?? 0);

            var expired = Convert.ToInt32(_db.Scalar(connection, @"
SELECT COUNT(1) FROM batches b
JOIN products p ON p.id = b.product_id
WHERE p.active = 1 AND b.active = 1 AND b.qty_base > 0
  AND (
        (IFNULL(b.expiry_date, '') <> '' AND b.expiry_date < date('now','localtime'))
     OR (IFNULL(b.expiry_date, '') = '' AND b.expiry_ym <> ''
             AND b.expiry_ym < strftime('%Y-%m', 'now', 'localtime'))
      );") ?? 0);

            // One rule for "a bill that counts", used by every card below: the three states the counter
            // and the reports charge for. A held cart is not a sale and a void bill was never money.
            var todayBills = Convert.ToInt32(_db.Scalar(connection, @"
SELECT COUNT(1) FROM sales
WHERE date(date) = date('now','localtime') AND status IN ('paid','partial','credit');") ?? 0);

            var todaySales = Convert.ToInt64(_db.Scalar(connection, @"
SELECT IFNULL(SUM(total_paisa), 0) FROM sales
WHERE date(date) = date('now','localtime') AND status IN ('paid','partial','credit');") ?? 0L);

            // Profit, rebuilt with the same four rows the Reports page uses for one day (Spec 21.6):
            // what the bills took, minus refunds paid back today, minus the cost of the goods that went
            // out, plus the cost of the goods that came back to the shelf. Prices already include the
            // bill's own discount and round-off, so neither has to be subtracted separately.
            var todayProfit = 0L;

            if (canSeeProfit)
            {
                // What the bills actually took - `sales.total_paisa`, which already has the bill's
                // discount and round-off applied. Summing the lines instead would quietly report the
                // discount as profit.
                var sold = Convert.ToInt64(_db.Scalar(connection, @"
SELECT IFNULL(SUM(s.total_paisa), 0) FROM sales s
WHERE date(s.date) = date('now','localtime') AND s.status IN ('paid','partial','credit');") ?? 0L);

                // CAST to INTEGER, the same as the Reports page: a half strip sold must lose the same
                // half paisa on both pages, or the two numbers differ by one and look like a bug.
                var costSold = Convert.ToInt64(_db.Scalar(connection, @"
SELECT CAST(IFNULL(SUM(si.qty_base * si.cost_paisa_at_sale), 0) AS INTEGER)
FROM sale_items si
JOIN sales s ON s.id = si.sale_id
WHERE date(s.date) = date('now','localtime') AND s.status IN ('paid','partial','credit');") ?? 0L);

                // A return belongs to the day the money went back, not the day of the bill - the same
                // rule the drawer uses (Spec 15.6).
                var refunds = Convert.ToInt64(_db.Scalar(connection, @"
SELECT IFNULL(SUM(refund), 0)
FROM (
    SELECT r.refund_paisa AS refund FROM sale_returns r
    WHERE date(r.date) = date('now','localtime')
    UNION ALL
    SELECT pr.refund_paisa FROM provisional_report_lines pr
    WHERE date(pr.event_date) = date('now','localtime')
);") ?? 0L);

                var restockedCost = Convert.ToInt64(_db.Scalar(connection, @"
SELECT CAST(IFNULL(SUM(cost), 0) AS INTEGER)
FROM (
    SELECT r.qty_base * si.cost_paisa_at_sale AS cost
    FROM sale_returns r
    JOIN sale_items si ON si.id = r.sale_item_id
    WHERE r.restock = 1 AND date(r.date) = date('now','localtime')
    UNION ALL
    SELECT pr.restock_cost_paisa FROM provisional_report_lines pr
    WHERE date(pr.event_date) = date('now','localtime')
);") ?? 0L);

                todayProfit = sold - refunds - (costSold - restockedCost);
            }

            var credit = Convert.ToInt64(_db.Scalar(connection,
                "SELECT IFNULL(SUM(balance_paisa), 0) FROM customers WHERE active = 1;") ?? 0L);

            var payable = Convert.ToInt64(_db.Scalar(connection,
                "SELECT IFNULL(SUM(balance_paisa), 0) FROM suppliers WHERE active = 1;") ?? 0L);

            var shiftOpen = Convert.ToInt64(_db.Scalar(connection,
                "SELECT COUNT(1) FROM shifts WHERE status = 'open' AND closed_at IS NULL;") ?? 0L) > 0;

            return ServiceResult<DashboardSnapshot>.Success(new DashboardSnapshot
            {
                Products = products,
                LowStock = lowStock,
                NegativeStock = negative,
                ExpiringSoon = expiring,
                Expired = expired,
                TodayBills = todayBills,
                TodaySalesPaisa = todaySales,
                TodayProfitPaisa = todayProfit,
                CanSeeProfit = canSeeProfit,
                CustomerCreditPaisa = credit,
                SupplierPayablePaisa = payable,
                ShiftOpen = shiftOpen
            });
        }
        catch (Exception exception)
        {
            _log.Error("The dashboard could not be loaded.", exception);
            return ServiceResult<DashboardSnapshot>.Fail("The dashboard could not be loaded.");
        }
    }
}
