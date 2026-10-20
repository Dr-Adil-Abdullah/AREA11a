using Area11.WPF.ViewModels.Base;

namespace Area11.WPF.ViewModels;

/// <summary>A sidebar entry. The page ViewModel is created on first visit and then reused.</summary>
public sealed class NavItem
{
    private readonly Func<ObservableObject> _factory;
    private ObservableObject? _instance;

    public NavItem(string title, string subtitle, Func<ObservableObject> factory)
    {
        Title = title;
        Subtitle = subtitle;
        _factory = factory;
    }

    public string Title { get; }

    public string Subtitle { get; }

    public ObservableObject Resolve() => _instance ??= _factory();
}
