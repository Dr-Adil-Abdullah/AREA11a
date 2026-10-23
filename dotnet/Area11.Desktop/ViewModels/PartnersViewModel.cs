using Area11.Core.Models;
using Area11.Core.Services;

namespace Area11.Desktop.ViewModels;

// empty-page: none - a container whose three tabs carry their own sentences, and the page
// always draws the tab strip, so there is nothing blank here to explain.
/// <summary>
/// One place for the people the shop owes or is owed by (D-50).
///
/// The owner's complaint was simple: Customers and Suppliers were two menu lines that opened two
/// screens doing the same job from opposite sides, and udhaar recovery was a third line. They are one
/// book with three chapters, so now there is one menu entry and three tabs:
///   • Customers - the khata, what they bought, money received;
///   • Udhaar recovery - the old money, ageing, the slip to give;
///   • Suppliers - what the shop owes, and goods sent back.
///
/// The sub-pages are the same view models as before, unchanged. Only their address moved, so a fix in
/// the khata still reaches every place the khata is opened.
/// </summary>
public sealed class PartnersViewModel : ObservableObject
{
    private int _selectedTab;

    public PartnersViewModel(AppServices services, User user, Action? onChanged = null)
    {
        // A cashier may take customer money but must not see the suppliers' prices, so each tab is
        // built only when its own permission is granted (Spec 7.2).
        if (services.Permissions.Can(user, Modules.Customers))
        {
            Customers = new CustomersViewModel(services, user, onChanged);
            Recovery = new RecoveryViewModel(services, user);
        }

        if (services.Permissions.Can(user, Modules.Suppliers))
        {
            Suppliers = new SuppliersViewModel(services, user, onChanged);
        }

        // Open the first tab this person may actually see. A hidden TabItem still holds its index,
        // so a page that starts on index 0 would open blank for a supplier-only account.
        _selectedTab = Customers is not null ? 0 : HasSuppliers ? 2 : 0;
    }

    public CustomersViewModel? Customers { get; }

    public RecoveryViewModel? Recovery { get; }

    public SuppliersViewModel? Suppliers { get; }

    public bool HasCustomers => Customers is not null;

    public bool HasRecovery => Recovery is not null;

    public bool HasSuppliers => Suppliers is not null;

    /// <summary>
    /// Which tab is open. Kept on the page rather than in the view so the same tab is still open
    /// after walking to the counter and back.
    /// </summary>
    public int SelectedTab
    {
        get => _selectedTab;
        set => SetProperty(ref _selectedTab, Math.Clamp(value, 0, 2));
    }
}
