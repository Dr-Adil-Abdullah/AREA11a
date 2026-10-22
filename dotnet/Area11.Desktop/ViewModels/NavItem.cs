namespace Area11.Desktop.ViewModels;

/// <summary>
/// One entry of the left menu. Resolve() creates the page only when it is opened.
///
/// An entry is either a **page** (clickable) or a **section heading** such as "PEOPLE" - the heading
/// groups the pages under it and cannot be selected (D-50: Customers and Suppliers belong to one
/// heading, not two separate lives in the menu).
/// </summary>
public sealed class NavItem
{
    private readonly Func<ObservableObject>? _resolve;

    public NavItem(string title, string subtitle, Func<ObservableObject> resolve)
    {
        Title = title;
        Subtitle = subtitle;
        _resolve = resolve;
    }

    private NavItem(string title)
    {
        Title = title;
        Subtitle = string.Empty;
        IsHeader = true;
    }

    /// <summary>A heading over a group of pages. It is not a page and never opens one.</summary>
    public static NavItem Header(string title) => new(title);

    public string Title { get; }

    public string Subtitle { get; }

    /// <summary>True = a section heading, not a page.</summary>
    public bool IsHeader { get; }

    /// <summary>The opposite, written out so a binding or a test can say what it means.</summary>
    public bool IsPage => !IsHeader;

    /// <summary>The small line under a menu row, shown as a tooltip to keep the sidebar narrow.</summary>
    public string Tooltip => IsHeader ? Title : Title + " - " + Subtitle;

    public ObservableObject Resolve() =>
        _resolve?.Invoke() ?? throw new InvalidOperationException("A section heading has no page.");
}
