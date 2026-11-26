using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace Area11.Desktop.Views;

/// <summary>Opt-in area labels only. No page navigation, business commands or input-event interception.</summary>
public static class GuideContext
{
    public static readonly DependencyProperty TopicProperty = DependencyProperty.RegisterAttached(
        "Topic", typeof(string), typeof(GuideContext), new PropertyMetadata(null, OnTopicChanged));
    public static readonly DependencyProperty RouteProperty = DependencyProperty.RegisterAttached(
        "Route", typeof(string), typeof(GuideContext), new PropertyMetadata(null, (target, args) =>
        {
            if (args.NewValue is string route) SetTopic(target, route switch
            {
                "sell" => "sales", "counter" => "sales.pos", "bills" => "sales.bills",
                "returns" => "sales.returns", "provisional" => "sales.no-bill", "stock" => "stock",
                "products" => "stock.products", "reorder" => "stock.reorder", "purchases" => "stock.purchases",
                "writeOff" => "stock.writeoff", "stockMinus" => "stock.minus", "stockTake" => "stock.count",
                "people" or "partners" => "customers", "doctors" => "customers.doctors", "money" => "money",
                "shift" => "money.shift", "expenses" => "money.expenses", "reports" => "money.reports", "tax" => "money.tax",
                "system" => "system", "audit" => "system.audit", "importExport" => "system.import",
                "backup" => "system.backup", "lan" => "system.lan", "cloud" => "system.cloud",
                "customFields" => "system.custom", "hardware" => "system.hardware", "settings" => "system.settings",
                "books" => "system.books", _ => "basics.navigation"
            });
        }));
    public static string? GetRoute(DependencyObject target) => (string?)target.GetValue(RouteProperty);
    public static void SetRoute(DependencyObject target, string value) => target.SetValue(RouteProperty, value);
    public static readonly DependencyProperty SettingsSectionProperty = DependencyProperty.RegisterAttached(
        "SettingsSection", typeof(int), typeof(GuideContext), new PropertyMetadata(-1, (target, args) =>
        {
            SetTopic(target, (int)args.NewValue switch
            {
                0 => "system.settings.identity", 1 => "system.settings.shop", 2 => "system.settings.printing",
                3 => "system.settings.rules", 4 => "system.settings.cloud", 5 => "system.settings.staff",
                6 => "system.settings.words", 7 => "system.settings.fields", _ => "system.settings"
            });
        }));
    public static int GetSettingsSection(DependencyObject target) => (int)target.GetValue(SettingsSectionProperty);
    public static void SetSettingsSection(DependencyObject target, int value) => target.SetValue(SettingsSectionProperty, value);

    private static readonly DependencyProperty DocsWindowProperty = DependencyProperty.RegisterAttached(
        "DocsWindow", typeof(GuideWindow), typeof(GuideContext), new PropertyMetadata(null));
    public static string? GetTopic(DependencyObject target) => (string?)target.GetValue(TopicProperty);
    public static void SetTopic(DependencyObject target, string value) => target.SetValue(TopicProperty, value);

    private static void OnTopicChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not FrameworkElement host) return;
        host.ContextMenuOpening -= OnMenuOpening;
        if (args.NewValue is string) host.ContextMenuOpening += OnMenuOpening;
    }

    private static DependencyObject? ParentOf(DependencyObject target) => target switch
    {
        Visual or Visual3D => VisualTreeHelper.GetParent(target) ?? LogicalTreeHelper.GetParent(target),
        FrameworkContentElement content => content.Parent,
        _ => LogicalTreeHelper.GetParent(target)
    };

    private static void OnMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (e.Handled || sender is not FrameworkElement host || e.OriginalSource is not DependencyObject source) return;
        // Preserve TextBox/RichTextBox editing, password menus, grid row actions and pickers.
        // Context help remains on the labelled heading/background; these controls are not hijacked.
        for (var current = source; current is not null; current = ParentOf(current))
        {
            if (current is TextBoxBase or PasswordBox or DataGrid or Selector) return;
            if (current is FrameworkElement item && item.ContextMenu is not null) return;
            if (ReferenceEquals(current, host)) break;
        }
        var topic = GetTopic(host);
        if (string.IsNullOrEmpty(topic)) return;
        e.Handled = true;
        var owner = Window.GetWindow(host);
        var focused = Keyboard.FocusedElement;
        var menu = new ContextMenu { PlacementTarget = host };
        var help = new MenuItem { Header = "Help" };
        help.Click += (_, _) =>
        {
            if (owner is not null) MessageBox.Show(owner, "Help ابھی تیار نہیں۔", "Help", MessageBoxButton.OK, MessageBoxImage.Information);
        };
        var docs = new MenuItem { Header = "Docs" };
        docs.Click += (_, _) => OpenDocs(host, topic, focused);
        menu.Items.Add(help);
        menu.Items.Add(docs);
        menu.IsOpen = true;
    }

    private static void OpenDocs(FrameworkElement host, string topic, IInputElement? focused)
    {
        var owner = Window.GetWindow(host);
        if (owner is null) return;
        // One owned modeless window per original window; owner close also closes its Docs.
        if (owner.GetValue(DocsWindowProperty) is GuideWindow existing)
        {
            existing.ShowTopic(topic);
            existing.Activate();
            return;
        }
        var window = new GuideWindow { Owner = owner };
        owner.SetValue(DocsWindowProperty, window);
        var originalFocus = focused is null ? null : new WeakReference<IInputElement>(focused);
        window.Closed += (_, _) =>
        {
            owner.ClearValue(DocsWindowProperty);
            RestoreFocus(owner, originalFocus);
        };
        window.ShowTopic(topic);
        window.Show(); // Never ShowDialog: the existing editor remains attached and unmodified.
    }

    internal static void RestoreFocus(Window owner, WeakReference<IInputElement>? originalFocus)
    {
        owner.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            // Never activate a background app or revive a detached/disabled security-cleared form.
            if (owner.IsActive && originalFocus is not null && originalFocus.TryGetTarget(out var target)
                && target is UIElement element && element.IsVisible && element.IsEnabled && element.Focusable
                && ReferenceEquals(Window.GetWindow(element), owner)) Keyboard.Focus(element);
        }));
    }
}
