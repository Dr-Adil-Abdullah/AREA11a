using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Area11.Core.Utils;
using Area11.Desktop.ViewModels;

namespace Area11.Desktop.Views;

/// <summary>
/// The frame. Its only job in code is to tell the session clock that somebody is still here: every
/// click and every key press is work, so the idle limit starts again (v0.35.0, D-92).
///
/// Why the window and not the pages: a person can read a page without touching a button - and a
/// person who is reading is not the person the rule is for. The rule is for the counter that was
/// left alone with the drawer open.
/// </summary>
public partial class ShellWindow : Window
{
    public ShellWindow()
    {
        InitializeComponent();
        Closing += ConfirmShellClosing;
        Deactivated += OnShellDeactivated;
        Activated += OnShellReactivated;
    }

    /// <summary>
    /// Portion 16: one-shot pass. The app sets it before a close it ordered itself (sign-out,
    /// session expiry) - those are not a person pressing ×, so they never see the prompt.
    /// </summary>
    public bool AllowClose { get; set; }

    private void ConfirmShellClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Windows itself is going down, or the app ordered this close: never stand in the way.
        if (App.IsSystemShutdown || AllowClose)
        {
            return;
        }

        // The screen lock implies a protected close (owner's word, Nov 2026): with the lock on,
        // × asks for the password even when the startup mode itself is not protected.
        if (DataContext is not ShellViewModel shell
            || (!shell.Services.Startup.IsProtected && !shell.Services.Focus.Enabled))
        {
            return;
        }

        e.Cancel = true;

        var dialog = new CloseUnlockWindow(shell.Services) { Owner = this };

        if (dialog.ShowDialog() == true && dialog.Unlocked)
        {
            AllowClose = true;
            Close();
        }
    }

    private string? _leftAt;

    /// <summary>
    /// Repair (owner's v0.60.0 run, Nov 2026): without these two, rapid focus fights (our own
    /// Activate against the browser, repeated Alt+Tab) queued one check per heartbeat and every
    /// check showed its own modal box - a pile of fifteen. Now at most one guard box exists, and a
    /// nag-loop (OK while still outside) waits a few seconds between reminders. The episode stamp
    /// is untouched: every escape is still recorded when the operator comes back.
    /// </summary>
    private bool _guardDialogOpen;

    private DateTime _lastGuardBoxUtc = DateTime.MinValue;

    private static readonly TimeSpan GuardBoxCooldown = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Screen lockdown, first half (owner's word, Nov 2026): while the guard is on, leaving Area11
    /// for another program first shows a small "not allowed" box and pulls the screen back. Our own
    /// boxes (password prompt, message boxes, dialogs) deactivate the frame too, so the check waits
    /// one UI heartbeat and then asks: is ANY Area11 window active? Only a real escape - another
    /// program, the desktop, Task Manager - gets the box. The stamp is what the black-box episode
    /// is measured from when the operator comes back; a run that never comes back (Task Manager
    /// end, power loss) is caught by the next startup instead (FocusGuardService.CheckPreviousRun).
    /// </summary>
    private void OnShellDeactivated(object? sender, EventArgs e)
    {
        // The app ordered this close itself (sign-out, session expiry), or Windows is going down:
        // never stand in the way - same rule as the × prompt above.
        if (App.IsSystemShutdown || AllowClose)
        {
            return;
        }

        if (DataContext is not ShellViewModel shell || !shell.Services.Focus.Enabled)
        {
            return;
        }

        // First stamp wins: without an Activated in between, repeated leavings are one absence.
        _leftAt ??= Clock.Now();

        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                if (App.IsSystemShutdown || AllowClose || !shell.Services.Focus.Enabled)
                {
                    _leftAt = null;
                    return;
                }

                foreach (var window in Application.Current.Windows)
                {
                    if (window is Window ours && ours.IsActive)
                    {
                        _leftAt = null;
                        return;
                    }
                }

                // One box at a time, and no nag-loop: a queued check that arrives while the box
                // is already open - or seconds after the last one - stays silent. The stamp above
                // still stands, so the escape is recorded when the operator comes back.
                if (_guardDialogOpen || DateTime.UtcNow - _lastGuardBoxUtc < GuardBoxCooldown)
                {
                    return;
                }

                // Nobody's home: pull the screen back first, then say why.
                if (WindowState == WindowState.Minimized)
                {
                    WindowState = WindowState.Maximized;
                }

                Activate();

                _guardDialogOpen = true;
                _lastGuardBoxUtc = DateTime.UtcNow;

                try
                {
                    MessageBox.Show(this,
                        "Going back is not allowed on this screen. This escape was written to the black box.",
                        "Area11", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                finally
                {
                    _guardDialogOpen = false;
                }

                Activate();
            }
            catch (Exception)
            {
                // The guard must never crash the till.
            }
        }), DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// Screen lockdown, second half: the operator came back, so the escape gets its black-box
    /// episode - who, when, for how long. Never throws: telemetry.
    /// </summary>
    private void OnShellReactivated(object? sender, EventArgs e)
    {
        if (_leftAt is null || DataContext is not ShellViewModel shell)
        {
            return;
        }

        var leftAt = _leftAt;
        _leftAt = null;

        shell.Services.Focus.RecordAway(shell.CurrentUser, leftAt, Clock.Now());
    }

    private TabControl? _headerTabs;

    public ObservableCollection<HeaderSectionItem> HeaderSections { get; } = new();

    internal void ShowHeaderSections(TabControl tabs)
    {
        _headerTabs = tabs;
        HeaderSections.Clear();
        foreach (var item in tabs.Items)
        {
            if (item is TabItem tab) HeaderSections.Add(new HeaderSectionItem(tab));
        }
    }

    internal void HideHeaderSections(TabControl tabs)
    {
        // An old page can unload after the next page loaded; never clear the new page's controls.
        if (!ReferenceEquals(_headerTabs, tabs)) return;
        _headerTabs = null;
        HeaderSections.Clear();
    }

    private void OnHeaderSectionClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: HeaderSectionItem item } && _headerTabs is not null
            && _headerTabs.Items.Contains(item.Tab) && item.Tab.Visibility == Visibility.Visible
            && item.Tab.IsEnabled)
        {
            _headerTabs.SelectedItem = item.Tab;
        }
    }

    private void OnActivity(object sender, InputEventArgs e) =>
        (DataContext as ShellViewModel)?.TouchSession();
}
