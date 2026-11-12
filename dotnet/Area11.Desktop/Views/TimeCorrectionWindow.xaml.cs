using System.Windows;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

/// <summary>
/// Modal list of the wrongly-stamped bills: nothing moves until the one fix button is pressed, and
/// closing the window changes nothing - so there is no closing guard to trip over.
/// </summary>
public partial class TimeCorrectionWindow : Window
{
    public TimeCorrectionWindow(TimeCorrectionViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
