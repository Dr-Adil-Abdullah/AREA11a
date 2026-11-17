using Area11.Core.Models;

namespace Area11.Core.Services;

/// <summary>
/// Portion 13: the one gate in front of every profit number. Two keys, both must turn: the person
/// needs the `profit` permission (Spec 12.1.2 - the net profit is the owner's), and the owner must
/// keep the global `profit.showMargins` switch on. Switching it off hides margins everywhere - the
/// dashboard card, the reports, the doctors' table, the customer's page and the product history -
/// without touching a single stored number.
/// </summary>
public static class ProfitPolicy
{
    public static bool CanSee(PermissionService permissions, SettingsService settings, User? user)
    {
        if (permissions is null || settings is null || user is null)
        {
            return false;
        }

        return settings.GetBool(MasterFoundationSettings.ProfitShowMargins, true)
            && permissions.Can(user, Modules.Profit);
    }
}
