using System.Windows;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

/// <summary>
/// The print preview window. Code-behind only names the window, sets the data context and closes;
/// the text comes from <c>ReceiptDocument.PlainText</c> through <c>PrintPreviewViewModel</c>.
/// </summary>
public partial class PrintPreviewWindow : Window
{
    public PrintPreviewWindow(PrintPreviewViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();

        DataContext = viewModel;
        Title = viewModel.Title;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
