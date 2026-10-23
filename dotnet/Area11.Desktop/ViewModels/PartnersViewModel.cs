using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

// empty-page: delegated - the sentence belongs to the open tab (Customers, Udhaar recovery or
// Suppliers), so this container does not carry words of its own; see SelectedEmptyHint below.
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
public sealed class PartnersViewModel : ObservableObject, IEmptyPage
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

        // The card under this page belongs to the tab that is open, so this page re-reads it when a
        // tab changes and when that tab changes anything of its own (D-89).
        foreach (var child in new[] { (object?)Customers, Recovery, Suppliers })
        {
            if (child is System.ComponentModel.INotifyPropertyChanged watched)
            {
                watched.PropertyChanged += (_, _) => OnPropertyChanged(nameof(SelectedEmptyHint));
            }
        }
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
        set
        {
            if (SetProperty(ref _selectedTab, Math.Clamp(value, 0, 2)))
            {
                OnPropertyChanged(nameof(SelectedEmptyHint));
            }
        }
    }

    /// <summary>
    /// Why the open tab is empty, in that tab's own words, or null when it has rows (v0.33.0, D-89).
    ///
    /// Asked of the tab, not copied here: the container does not know what a khata is, and a copy
    /// would be the second place to edit the day a tab changes. A tab this person may not see is
    /// not "empty" - it is not there at all, so it answers nothing.
    /// </summary>
    public EmptyHint? EmptyHint => SelectedEmptyHint;

    public EmptyHint? SelectedEmptyHint => SelectedTab switch
    {
        0 => Customers?.EmptyHint,
        1 => Recovery?.EmptyHint,
        2 => Suppliers?.EmptyHint,
        _ => null
    };
}
