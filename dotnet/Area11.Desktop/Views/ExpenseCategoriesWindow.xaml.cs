using System.Windows;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

public partial class ExpenseCategoriesWindow : Window
{
    public ExpenseCategoriesWindow(ExpenseCategoryManagerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
