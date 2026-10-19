namespace Area11.Desktop.ViewModels;

/// <summary>One entry of the left menu. Resolve() creates the page only when it is opened.</summary>
public sealed class NavItem
{
    private readonly Func<ObservableObject> _resolve;

    public NavItem(string title, string subtitle, Func<ObservableObject> resolve)
    {
        Title = title;
        Subtitle = subtitle;
        _resolve = resolve;
    }

    public string Title { get; }

    public string Subtitle { get; }

    public ObservableObject Resolve() => _resolve();
}
