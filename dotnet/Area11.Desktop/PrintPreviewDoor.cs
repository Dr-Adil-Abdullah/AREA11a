using System.Windows;
using Area11.Core.Models;
using Area11.Desktop.ViewModels;
using Area11.Desktop.Views;

namespace Area11.Desktop;

/// <summary>The one UI door to the print preview; view-models do not construct Windows.</summary>
internal static class PrintPreviewDoor
{
    public static void Show(AppServices services, ReceiptDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ShowWindow(services, new PrintPreviewViewModel(services, document));
    }

    public static void ShowBulk(AppServices services, BulkPrintJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        ShowWindow(services, new PrintPreviewViewModel(services, job));
    }

    private static void ShowWindow(AppServices services, PrintPreviewViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(services);
        var window = new PrintPreviewWindow(viewModel);
        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(candidate => candidate.IsActive)
                    ?? Application.Current?.MainWindow;

        if (owner is not null && !ReferenceEquals(owner, window))
        {
            window.Owner = owner;
        }

        window.ShowDialog();
    }
}
