using System.Windows;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

/// <summary>Portion 5 drafts drawer; all behavior is in the view-model and DraftService.</summary>
public partial class DraftsView : Window
{
    public DraftsView(DraftsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
