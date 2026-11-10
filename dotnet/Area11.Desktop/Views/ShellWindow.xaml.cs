using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        if (DataContext is not ShellViewModel shell || !shell.Services.Startup.IsProtected)
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
