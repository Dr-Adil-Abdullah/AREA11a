using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Area11.Core.Models;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

public partial class ProductsView : UserControl
{
    public ProductsView()
    {
        InitializeComponent();
    }

    private void OnProductDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Photo/category/company buttons retain their own actions; blank headers and empty grid
        // space do not accidentally edit the previously selected medicine.
        var node = e.OriginalSource as DependencyObject;
        while (node is not null && !ReferenceEquals(node, ProductGrid))
        {
            if (node is ButtonBase) return;
            if (node is DataGridRow { Item: Product product } && DataContext is ProductsViewModel vm)
            {
                vm.SelectedProduct = product;
                vm.EditCommand.Execute(null);
                e.Handled = true;
                return;
            }
            node = node is Visual or Visual3D ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
        }
    }
}
