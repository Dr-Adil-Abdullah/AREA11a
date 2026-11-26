using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;

namespace Area11.Desktop.Views;

public partial class DashboardView : UserControl
{
    private void OnGuidelines(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is ShellWindow shell) shell.ShowGuidelines();
    }

    // A background/title/description double-click opens the same lazy overlay as the keyboard-accessible button.
    // The normal button still opens on one click; it handles its own mouse input.
    private void OnGuidelinesCardMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.ClickCount != 2) return;
        e.Handled = true;
        OnGuidelines(sender, e);
    }

    public DashboardView()
    {
        InitializeComponent();
    }
}
