using System.Collections.ObjectModel;
using Area11.WPF.Models;
using Area11.WPF.Services;
using Area11.WPF.ViewModels.Base;

namespace Area11.WPF.ViewModels;

public sealed class ShellViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;
    private NavItem? _selectedNav;
    private ObservableObject? _currentViewModel;

    public ShellViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        StoreName = services.Settings.Get("store.name");
        AppName = services.Settings.Get("brand.appName");

        BuildNavigation();
        SelectedNav = NavItems.FirstOrDefault();
    }

    public string AppName { get; }

    public string StoreName { get; }

    public string UserName => _user.Name;

    public string RoleText => _user.Role.ToString();

    public ObservableCollection<NavItem> NavItems { get; } = new();

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

        if (_user.CanManage)
        {
            NavItems.Add(new NavItem("Products", "Medicines and items", () => new ProductsViewModel(_services, _user.Name)));
        }

        if (_user.CanManage)
        {
            NavItems.Add(new NavItem("Settings", "Shop configuration", () => new SettingsViewModel(_services, _user.Name)));
        }
    }
}
