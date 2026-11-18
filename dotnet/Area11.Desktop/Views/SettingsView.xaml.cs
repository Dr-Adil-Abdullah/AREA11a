using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

/// <summary>Pointer gesture arbitration only; permissions, persistence and owner checks stay in Core.</summary>
public partial class SettingsView : UserControl
{
    private readonly DispatcherTimer _permissionClickTimer;
    private readonly DispatcherTimer _staffAccessTimer;
    private Action? _pendingPermissionClick;

    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    public SettingsView()
    {
        InitializeComponent();
        _staffAccessTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _staffAccessTimer.Tick += CheckStaffAccess;
        Loaded += (_, _) => { CheckStaffAccess(this, EventArgs.Empty); _staffAccessTimer.Start(); };
        Unloaded += (_, _) => _staffAccessTimer.Stop();
        _permissionClickTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(GetDoubleClickTime() + 1)
        };
        _permissionClickTimer.Tick += CommitPermissionClick;
        Unloaded += (_, _) => CancelPermissionClick();
        DataContextChanged += (_, _) => CancelPermissionClick();
    }

    private void CheckStaffAccess(object? sender, EventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel) viewModel.RefreshStaffAccess();
    }

    private void PermissionMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not CheckBox { DataContext: PermissionToggle toggle }
            || DataContext is not SettingsViewModel viewModel) return;
        e.Handled = true; // never let CheckBox commit the first half of a double click
        if (e.ClickCount >= 2)
        {
            CancelPermissionClick();
            viewModel.OpenPermissionDetails(toggle);
            return;
        }
        // A different pending checkbox is a separate deliberate single click.
        CommitPermissionClick(null, EventArgs.Empty);
        if (!toggle.CanEdit) return;
        ((CheckBox)sender).Focus();
        _pendingPermissionClick = () => viewModel.TogglePermissionFromPointer(toggle);
        ((CheckBox)sender).CaptureMouse();
    }

    private void PermissionMouseUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is not CheckBox checkbox) return;
        checkbox.ReleaseMouseCapture();
        if (!checkbox.IsMouseOver) CancelPermissionClick();
        else if (_pendingPermissionClick is not null) _permissionClickTimer.Start();
    }

    private void CommitPermissionClick(object? sender, EventArgs e)
    {
        _permissionClickTimer.Stop();
        var click = _pendingPermissionClick;
        _pendingPermissionClick = null;
        click?.Invoke();
    }

    private void CancelPermissionClick()
    {
        _permissionClickTimer.Stop();
        _pendingPermissionClick = null;
    }

    private void PermissionDetailsClick(object sender, RoutedEventArgs e)
    {
        CancelPermissionClick();
        if (sender is FrameworkElement { DataContext: PermissionToggle toggle }
            && DataContext is SettingsViewModel viewModel)
            viewModel.OpenPermissionDetails(toggle);
    }
}
