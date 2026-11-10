using System.Windows;
using System.Windows.Controls;

namespace Area11.Desktop.Views;

/// <summary>
/// Connects an existing page's TabControl to the shell header without moving TabItems out of their
/// original control. The page still owns selection, permission visibility and all form state.
/// </summary>
public static class HeaderSectionTabs
{
    public static readonly DependencyProperty InHeaderProperty = DependencyProperty.RegisterAttached(
        "InHeader", typeof(bool), typeof(HeaderSectionTabs), new PropertyMetadata(false, OnChanged));

    private static readonly DependencyProperty OwnerProperty = DependencyProperty.RegisterAttached(
        "Owner", typeof(ShellWindow), typeof(HeaderSectionTabs), new PropertyMetadata(null));

    public static bool GetInHeader(DependencyObject target) => (bool)target.GetValue(InHeaderProperty);

    public static void SetInHeader(DependencyObject target, bool value) => target.SetValue(InHeaderProperty, value);

    private static void OnChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not TabControl tabs) return;
        if ((bool)args.NewValue)
        {
            tabs.Loaded += OnLoaded;
            tabs.Unloaded += OnUnloaded;
            if (tabs.IsLoaded) OnLoaded(tabs, new RoutedEventArgs());
        }
        else
        {
            tabs.Loaded -= OnLoaded;
            tabs.Unloaded -= OnUnloaded;
            Detach(tabs);
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (sender is not TabControl tabs || Window.GetWindow(tabs) is not ShellWindow shell) return;
        tabs.SetValue(OwnerProperty, shell);
        shell.ShowHeaderSections(tabs);
    }

    private static void OnUnloaded(object sender, RoutedEventArgs args)
    {
        if (sender is TabControl tabs) Detach(tabs);
    }

    private static void Detach(TabControl tabs)
    {
        if (tabs.GetValue(OwnerProperty) is ShellWindow shell) shell.HideHeaderSections(tabs);
        tabs.ClearValue(OwnerProperty);
    }
}

/// <summary>A wrapper prevents WPF from reparenting the original tab into the header.</summary>
public sealed class HeaderSectionItem
{
    public HeaderSectionItem(TabItem tab) => Tab = tab;
    public TabItem Tab { get; }
}
