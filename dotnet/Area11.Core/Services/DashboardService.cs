using Area11.Core.Data;
using Area11.Core.Utils;

namespace Area11.Core.Services;

public sealed class DashboardSnapshot
{
    public int Products { get; init; }

    public int LowStock { get; init; }

    public int NegativeStock { get; init; }

    public int ExpiringSoon { get; init; }

    public int Expired { get; init; }

    public int TodayBills { get; init; }

    public long TodaySalesPaisa { get; init; }

    public long TodayProfitPaisa { get; init; }

    public long CustomerCreditPaisa { get; init; }

    public long SupplierPayablePaisa { get; init; }

    public bool ShiftOpen { get; init; }
}

/// <summary>Read-only numbers for the home screen (Spec 21 summary rules, simplified).</summary>
public sealed class DashboardService
{
    private readonly Database _db;
    private readonly SettingsService _settings;
    private readonly LogService _log;

    public DashboardService(Database db, SettingsService settings, LogService log)
    {
        _db = db;
        _settings = settings;
        _log = log;
    }

    public ServiceResult<DashboardSnapshot> GetSnapshot()
    {
        try
        {
            using var connection = _db.Open();

            var products = Convert.ToInt32(_db.Scalar(connection,
                "SELECT COUNT(1) FROM products WHERE active = 1;") ?? 0);

            var lowStock = Convert.ToInt32(_db.Scalar(connection, @"
SELECT COUNT(1) FROM products p
WHERE p.active = 1
  AND IFNULL((SELECT SUM(b.qty_base) FROM batches b WHERE b.product_id = p.id AND b.active = 1), 0) <= p.reorder_level;") ?? 0);

            var negative = Convert.ToInt32(_db.Scalar(connection, @"
SELECT COUNT(1) FROM products p
WHERE p.active = 1
  AND IFNULL((SELECT SUM(b.qty_base) FROM batches b WHERE b.product_id = p.id AND b.active = 1), 0) < 0;") ?? 0);

            // "Expiring soon" is the owner's nearest band (Level 3), not a hard-coded three months
            // (F-29): change the band in Settings and this card follows on the next refresh.
            var window = "+" + _settings.ExpiryBands.Level3Days + " days";

            var expiring = Convert.ToInt32(_db.Scalar(connection, @"
SELECT COUNT(1) FROM batches
WHERE active = 1 AND qty_base > 0 AND expiry_ym <> ''
  AND expiry_ym >= strftime('%Y-%m', 'now', 'localtime')
  AND expiry_ym <= strftime('%Y-%m', 'now', 'localtime', @window);", new { window }) ?? 0);

            var expired = Convert.ToInt32(_db.Scalar(connection, @"
SELECT COUNT(1) FROM batches
WHERE active = 1 AND qty_base > 0 AND expiry_ym <> ''
  AND expiry_ym < strftime('%Y-%m', 'now', 'localtime');") ?? 0);

            var todayBills = Convert.ToInt32(_db.Scalar(connection,
                "SELECT COUNT(1) FROM sales WHERE date(date) = date('now','localtime') AND status <> 'void';") ?? 0);

            var todaySales = Convert.ToInt64(_db.Scalar(connection,
                "SELECT IFNULL(SUM(total_paisa), 0) FROM sales WHERE date(date) = date('now','localtime') AND status <> 'void';") ?? 0L);

            var todayProfit = Convert.ToInt64(_db.Scalar(connection, @"
SELECT IFNULL(SUM(si.line_total_paisa - si.qty_base * si.cost_paisa_at_sale), 0)
FROM sale_items si
JOIN sales s ON s.id = si.sale_id
WHERE date(s.date) = date('now','localtime') AND s.status <> 'void';") ?? 0L);

            var credit = Convert.ToInt64(_db.Scalar(connection,
                "SELECT IFNULL(SUM(balance_paisa), 0) FROM customers WHERE active = 1;") ?? 0L);

            var payable = Convert.ToInt64(_db.Scalar(connection,
                "SELECT IFNULL(SUM(balance_paisa), 0) FROM suppliers WHERE active = 1;") ?? 0L);

            var shiftOpen = Convert.ToInt64(_db.Scalar(connection,
                "SELECT COUNT(1) FROM shifts WHERE closed_at IS NULL;") ?? 0L) > 0;

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
