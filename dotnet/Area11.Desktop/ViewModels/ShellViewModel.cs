using System.Collections.ObjectModel;
using Area11.Core.Models;
using Area11.Core.Services;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// The window frame: menu on the left, current page on the right. The menu is built from the
/// permissions of the signed-in person, but hiding a menu is never the security — every service
/// checks the permission again (Spec 7.2.5).
/// </summary>
public sealed class ShellViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;
    private NavItem? _selectedNav;
    private ObservableObject? _currentViewModel;
    private string _storeName = string.Empty;
    private string _appName = string.Empty;
    private SettingsViewModel? _settingsPage;

    public ShellViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        StoreName = services.Settings.GetString("store.name");
        AppName = services.Settings.GetString("brand.appName");

        LogoutCommand = new RelayCommand(Logout);

        BuildNavigation();
        SelectedNav = NavItems.FirstOrDefault();
    }

    public event Action? LoggedOut;

    public ObservableCollection<NavItem> NavItems { get; } = new();

    public RelayCommand LogoutCommand { get; }

    public string StoreName
    {
        get => _storeName;
        private set => SetProperty(ref _storeName, value);
    }

    public string AppName
    {
        get => _appName;
        private set => SetProperty(ref _appName, value);
    }

    public string UserName => _user.Name;

    public string RoleText => _user.RoleLabel;

    public string Initials => string.IsNullOrWhiteSpace(_user.Name)
        ? "A"
        : _user.Name.Trim()[..1].ToUpperInvariant();

    public NavItem? SelectedNav
    {
        get => _selectedNav;
        set
        {
            if (SetProperty(ref _selectedNav, value))
            {
                CurrentViewModel = value?.Resolve();
            }
        }
    }

    public ObservableObject? CurrentViewModel
    {
        get => _currentViewModel;
        private set => SetProperty(ref _currentViewModel, value);
    }

    private void BuildNavigation()
    {
        NavItems.Add(new NavItem("Dashboard", "Today at a glance", () => new DashboardViewModel(_services)));

        if (_services.Permissions.Can(_user, Modules.Pos))
        {
            NavItems.Add(new NavItem("Counter (POS)", "Make a bill", () => new PosViewModel(_services, _user)));
        }

        if (_services.Permissions.Can(_user, Modules.Purchases))
        {
            NavItems.Add(new NavItem("Purchases", "Stock-in (PINV)", () => new PurchasesViewModel(_services, _user)));
        }

        if (_services.Permissions.Can(_user, Modules.Products))
        {
            NavItems.Add(new NavItem("Products", "Medicines and items", () => new ProductsViewModel(_services, _user)));
        }

        if (_services.Permissions.Can(_user, Modules.Returns))
        {
            NavItems.Add(new NavItem("Returns", "Money back, quarantine", () => new ReturnsViewModel(_services, _user)));
        }

        if (_services.Permissions.Can(_user, Modules.Settings))
        {
            NavItems.Add(new NavItem("Settings", "Shop, printing, staff", CreateSettingsPage));
        }
    }

    /// <summary>
    /// One Settings page for the whole session: it keeps the staff selection and raises Saved, so a
    /// new shop name appears in the sidebar the moment the owner presses Save.
    /// </summary>
    private ObservableObject CreateSettingsPage()
    {
        if (_settingsPage is null)
        {
            _settingsPage = new SettingsViewModel(_services, _user, RefreshHeader);
        }

        _settingsPage.Refresh();
        return _settingsPage;
    }

    private void RefreshHeader()
    {
        StoreName = _services.Settings.GetString("store.name");
        AppName = _services.Settings.GetString("brand.appName");
    }

    private void Logout()
    {
        _services.Auth.Logout(_user);
        _services.SignOut();
        LoggedOut?.Invoke();
    }
}
