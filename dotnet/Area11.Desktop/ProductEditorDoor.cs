using System.Windows;
using Area11.Desktop.ViewModels;
using Area11.Desktop.Views;

namespace Area11.Desktop;

/// <summary>The modal editor keeps the page and selection stable until the form is closed.</summary>
internal static class ProductEditorDoor
{
    public static void Show(ProductsViewModel viewModel)
    {
        var window = new ProductEditorWindow(viewModel);
        if (Application.Current?.MainWindow is Window owner && !ReferenceEquals(owner, window))
        {
            window.Owner = owner;
        }
        window.ShowDialog();
    }
}
