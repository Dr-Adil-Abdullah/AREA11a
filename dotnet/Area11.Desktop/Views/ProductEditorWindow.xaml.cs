using System.ComponentModel;
using System.Windows;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

/// <summary>Modal form: closing without a successful save requires an explicit confirmation.</summary>
public partial class ProductEditorWindow : Window
{
    private readonly ProductsViewModel _viewModel;
    private bool _finished;

    public ProductEditorWindow(ProductsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.EditorSaved += OnEditorSaved;
        Closing += ConfirmEditorClosing;
        Closed += (_, _) => _viewModel.EditorSaved -= OnEditorSaved;
    }

    private void OnEditorSaved()
    {
        _finished = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();

    private void ConfirmEditorClosing(object? sender, CancelEventArgs e)
    {
        if (_finished) return;
        e.Cancel = MessageBox.Show(this,
            "Close the editor and discard any unsaved changes? Saved products, extra fields, photos and barcodes are not undone.",
            "Close product editor", MessageBoxButton.YesNo, MessageBoxImage.Warning,
            MessageBoxResult.No) != MessageBoxResult.Yes;
    }
}
