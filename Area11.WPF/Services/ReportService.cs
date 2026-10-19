using Area11.WPF.Data;
using Area11.WPF.Models;

namespace Area11.WPF.Services;

/// <summary>Reporting totals. Profit always uses the cost saved on the batch, never today's cost.</summary>
public sealed class ReportService
{
    private readonly Database _db;
    private readonly LoggerService _log;

    public ReportService(Database db, LoggerService log)
    {
        _db = db;
        _log = log;
    }

    public DashboardSnapshot GetDashboard()
    {
        try
        {
            using var connection = _db.Open();

            var todaySales = _db.Scalar<long>(connection,
                "SELECT COALESCE(SUM(total_paisa), 0) FROM sales WHERE date(created_at) = date('now','localtime') AND status <> 'void';");

            var todayBills = _db.Scalar<long>(connection,
                "SELECT COUNT(1) FROM sales WHERE date(created_at) = date('now','localtime') AND status <> 'void';");

            var products = _db.Scalar<long>(connection,
                "SELECT COUNT(1) FROM products WHERE active = 1;");

            var lowStock = _db.Scalar<long>(connection,
                "SELECT COUNT(1) FROM products WHERE active = 1 AND reorder_level > 0 AND stock_base <= reorder_level;");

            var expiring = _db.Scalar<long>(connection,
                "SELECT COUNT(1) FROM batches WHERE qty_base > 0 AND expiry <> '' AND expiry <= date('now','localtime','+365 days');");

            var stockValue = _db.Scalar<long>(connection,
                "SELECT COALESCE(SUM(qty_base * cost_paisa), 0) FROM batches WHERE qty_base > 0;");

            return new DashboardSnapshot
            {
                TodaySalesPaisa = todaySales,
                TodayBills = (int)todayBills,
                ActiveProducts = (int)products,
                LowStockProducts = (int)lowStock,
                ExpiringBatches = (int)expiring,
                StockValuePaisa = stockValue
            };
        }
        catch (Exception exception)
        {
            _log.Error("Dashboard figures could not be calculated.", exception);
            return new DashboardSnapshot();
        }
    }
}
