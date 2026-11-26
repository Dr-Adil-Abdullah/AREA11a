using System.Windows.Controls;
using System.Windows;

namespace Area11.Desktop.Views;

public partial class DashboardView : UserControl
{
    private void OnGuidelines(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is ShellWindow shell) shell.ShowGuidelines();
    }

    public DashboardView()
    {
        InitializeComponent();
    }
}
