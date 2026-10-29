namespace Area11.Desktop.ViewModels;

/// <summary>
/// One destination in the shell's dark header. Resolve() creates the page only when it is opened.
///
/// An entry is either a **page** (Counter, Products, Bills ...) or a **module heading** (SELL,
/// STOCK ...). A heading opens its group's current/first page; it never resolves a page itself.
/// Editable labels are presentation only: GroupKey and LabelKey remain the stable identities.
/// </summary>
public sealed class NavItem : ObservableObject
{
    private readonly Func<ObservableObject>? _resolve;

    private string _title;
    private bool _isCurrentModule;
    private bool _isCurrentPage;

    public NavItem(string title, string subtitle, Func<ObservableObject> resolve)
    {
        _title = title;
        Subtitle = subtitle;
        _resolve = resolve;
    }

    private NavItem(string title)
    {
        _title = title;
        Subtitle = string.Empty;
        IsHeader = true;
    }

    /// <summary>A module heading over a group of pages. It is not a page and never opens one itself.</summary>
    public static NavItem Header(string title) => new(title);

    public string Title => _title;

    /// <summary>Stable catalog identity; never use the editable Title for navigation decisions.</summary>
    public string LabelKey { get; set; } = string.Empty;

    /// <summary>Applies a saved label without rebuilding the menu or losing the selected page.</summary>
    public void SetTitle(string title)
    {
        if (SetProperty(ref _title, title, nameof(Title)))
        {
            OnPropertyChanged(nameof(Tooltip));
        }
    }

    public string Subtitle { get; }

    /// <summary>True = a module heading, not a page.</summary>
    public bool IsHeader { get; }

    /// <summary>The opposite, written out so a binding or a test can say what it means.</summary>
    public bool IsPage => !IsHeader;

    /// <summary>Which module this entry belongs to ("sell", "money" ...). Empty for Dashboard.</summary>
    public string GroupKey { get; set; } = string.Empty;

    /// <summary>True on the top-row module that owns the open page.</summary>
    public bool IsCurrentModule
    {
        get => _isCurrentModule;
        set => SetProperty(ref _isCurrentModule, value);
    }

    /// <summary>True only on the exact open page in the contextual second row.</summary>
    public bool IsCurrentPage
    {
        get => _isCurrentPage;
        set => SetProperty(ref _isCurrentPage, value);
    }

    /// <summary>The page explanation stays in a tooltip so the header remains compact.</summary>
    public string Tooltip => IsHeader ? Title : Title + " - " + Subtitle;

    public ObservableObject Resolve() =>
        _resolve?.Invoke() ?? throw new InvalidOperationException("A module heading has no page.");
}
