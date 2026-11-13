using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>A read-only snapshot of the committed bill, not the next cart's changing totals.</summary>
public sealed class SaleSuccessViewModel
{
    public SaleSuccessViewModel(AppServices services, SaleResult sale, string printStatus)
    {
        var appName = services.Settings.GetString("brand.appName").Trim();
        WindowTitle = (appName.Length == 0 ? "Area11" : appName) + " — Sale saved";
        Invoice = sale.Code;
        TotalText = Money.Format(sale.TotalPaisa);
        CashReceivedText = Money.Format(sale.CashReceivedPaisa);
        CashReceivedLabel = sale.CashReceivedAssumed
            ? "Cash received (exact amount assumed)" : "Cash received";
        ChangeText = Money.Format(sale.ChangePaisa);
        PaidText = Money.Format(sale.PaidPaisa);
        DueText = Money.Format(sale.DuePaisa);
        PrintStatus = printStatus;
    }

    public string WindowTitle { get; }
    public string Invoice { get; }
    public string TotalText { get; }
    public string CashReceivedText { get; }
    public string CashReceivedLabel { get; }
    public string ChangeText { get; }
    public string PaidText { get; }
    public string DueText { get; }
    public string PrintStatus { get; }
}
