using System.Windows;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

/// <summary>
/// The product history window. Code-behind only names the window, sets the data context and closes;
/// the story comes from <c>ProductHistoryService</c> through <c>ProductHistoryViewModel</c>.
/// </summary>
public partial class ProductHistoryWindow : Window
{
    public ProductHistoryWindow(ProductHistoryViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();

        DataContext = viewModel;
        Title = viewModel.Title;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
