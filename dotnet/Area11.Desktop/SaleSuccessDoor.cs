using System.Windows;
using Area11.Core.Models;
using Area11.Desktop.ViewModels;
using Area11.Desktop.Views;

namespace Area11.Desktop;

/// <summary>Post-commit confirmation only. Failure to display it must never imply a failed sale.</summary>
internal static class SaleSuccessDoor
{
    public static bool Show(AppServices services, SaleResult sale, string printStatus)
    {
        try
        {
            var window = new SaleSuccessWindow(new SaleSuccessViewModel(services, sale, printStatus));
            var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(candidate => candidate.IsActive)
                        ?? Application.Current?.MainWindow;
            if (owner is not null) window.Owner = owner;
            // Keep Done visible even on a short work area; only the receipt summary scrolls.
            window.MaxHeight = Math.Max(1, SystemParameters.WorkArea.Height - 24);
            window.MaxWidth = Math.Max(1, SystemParameters.WorkArea.Width - 24);
            window.ShowDialog();
            return true;
        }
        catch (Exception exception)
        {
            services.Log.Error("The bill was saved but its confirmation could not open.", exception);
            return false;
        }
    }
}
