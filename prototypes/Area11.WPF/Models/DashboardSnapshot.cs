namespace Area11.WPF.Models;

/// <summary>Headline numbers for the dashboard. Money is paisa.</summary>
public sealed class DashboardSnapshot
{
    public long TodaySalesPaisa { get; init; }

    public int TodayBills { get; init; }

    public int ActiveProducts { get; init; }

    public int LowStockProducts { get; init; }

    public int ExpiringBatches { get; init; }

    public long StockValuePaisa { get; init; }
}
