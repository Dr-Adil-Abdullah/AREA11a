using System.ComponentModel;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

// empty-page: delegated - only the currently allowed tab may supply its empty hint.
/// <summary>D-50 container: current own-module tab visibility; business rules stay in child services.</summary>
public sealed class PartnersViewModel : ObservableObject, IEmptyPage
{
    private readonly AppServices _services;
    private readonly User _user;
    private readonly Action? _onChanged;
    private PartnerAccess _access = new();
    private int _selectedTab = -1;
    private CustomersViewModel? _customers;
    private RecoveryViewModel? _recovery;
    private SuppliersViewModel? _suppliers;

    public PartnersViewModel(AppServices services, User user, Action? onChanged = null)
    {
        _services = services;
        _user = user;
        _onChanged = onChanged;
        RefreshAccess();
    }

    public CustomersViewModel? Customers => _customers;
    public RecoveryViewModel? Recovery => _recovery;
    public SuppliersViewModel? Suppliers => _suppliers;
    public bool HasCustomers => _access.Customers && Customers is not null;
    public bool HasRecovery => _access.Customers && Recovery is not null;
    public bool HasSuppliers => _access.Suppliers && Suppliers is not null;

    public int SelectedTab
    {
        get => _selectedTab;
        set
        {
            // Native TabControl may send -1 during binding churn. Never accept a denied tab index.
            if (SetProperty(ref _selectedTab, _access.SelectTab(value)))
            {
                OnPropertyChanged(nameof(SelectedEmptyHint));
                OnPropertyChanged(nameof(EmptyHint));
            }
        }
    }

    public EmptyHint? EmptyHint => SelectedEmptyHint;
    public EmptyHint? SelectedEmptyHint => SelectedTab switch
    {
        0 when HasCustomers => Customers?.EmptyHint,
        1 when HasRecovery => Recovery?.EmptyHint,
        2 when HasSuppliers => Suppliers?.EmptyHint,
        _ => null
    };

    public void RefreshAccess()
    {
        var result = _services.PartnerAccess.Read(_user);
        var next = result.Ok && result.Value is not null ? result.Value : new PartnerAccess();
        // A revoked branch is actively checked before it is detached. Detached objects are not
        // recallable capabilities; their own services still gate subsequent operations.
        if (!next.Customers && (_customers is not null || _recovery is not null))
        {
            _customers?.RefreshAccess();
            _recovery?.RefreshAccess();
            Unwatch(_customers);
            Unwatch(_recovery);
            _customers = null;
            _recovery = null;
        }
        if (!next.Suppliers && _suppliers is not null)
        {
            _suppliers.RefreshAccess();
            Unwatch(_suppliers);
            _suppliers = null;
        }
        _access = next;
        if (next.Customers)
        {
            if (_customers is null)
            {
                _customers = new CustomersViewModel(_services, _user, _onChanged);
                Watch(_customers);
            }
            if (_recovery is null)
            {
                _recovery = new RecoveryViewModel(_services, _user);
                Watch(_recovery);
            }
        }
        if (next.Suppliers && _suppliers is null)
        {
            _suppliers = new SuppliersViewModel(_services, _user, _onChanged);
            Watch(_suppliers);
        }
        OnPropertyChanged(nameof(Customers));
        OnPropertyChanged(nameof(Recovery));
        OnPropertyChanged(nameof(Suppliers));
        OnPropertyChanged(nameof(HasCustomers));
        OnPropertyChanged(nameof(HasRecovery));
        OnPropertyChanged(nameof(HasSuppliers));
        SelectedTab = _selectedTab;
        OnPropertyChanged(nameof(SelectedEmptyHint));
        OnPropertyChanged(nameof(EmptyHint));
    }

    private void Watch(INotifyPropertyChanged child) => child.PropertyChanged += ChildChanged;
    private void Unwatch(INotifyPropertyChanged? child)
    {
        if (child is not null) child.PropertyChanged -= ChildChanged;
    }
    private void ChildChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(SelectedEmptyHint));
        OnPropertyChanged(nameof(EmptyHint));
    }
}
