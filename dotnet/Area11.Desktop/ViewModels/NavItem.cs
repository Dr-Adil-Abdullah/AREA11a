namespace Area11.Desktop.ViewModels;

/// <summary>
/// One entry of the left menu. Resolve() creates the page only when it is opened.
///
/// An entry is either a **page** (clickable) or a **section heading** such as "MONEY". A heading now
/// has a second job (D-51): clicking it puts its pages away, so a shop that never uses, say,
/// Stock-take can keep the menu to the things it does use. Nothing is deleted - the heading stays,
/// one click brings the pages back.
/// </summary>
public sealed class NavItem : ObservableObject
{
    private readonly Func<ObservableObject>? _resolve;

    private bool _isCollapsed;
    private bool _isVisible = true;

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

    /// <summary>Which group this entry belongs to ("sell", "money" ...). Empty for the dashboard.</summary>
    public string GroupKey { get; set; } = string.Empty;

    /// <summary>
    /// A heading is "collapsed" when its pages are put away. Only headings use it; the arrow on the
    /// screen reads it, so a collapse has to tell the binding it changed.
    /// </summary>
    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (SetProperty(ref _isCollapsed, value))
            {
                OnPropertyChanged(nameof(Chevron));
            }
        }
    }

    /// <summary>The little arrow: down when the pages are showing, right when they are away.</summary>
    public string Chevron => _isCollapsed ? "\u25B8" : "\u25BE";

    /// <summary>
    /// False = the row is hidden because its group is collapsed. The menu list is never rebuilt (a
    /// rebuild would drop the selected row), so hiding a row is how a group closes.
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    /// <summary>The small line under a menu row, shown as a tooltip to keep the sidebar narrow.</summary>
    public string Tooltip => IsHeader ? Title : Title + " - " + Subtitle;

    public ObservableObject Resolve() =>
        _resolve?.Invoke() ?? throw new InvalidOperationException("A section heading has no page.");
}
