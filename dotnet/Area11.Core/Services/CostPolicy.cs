using Area11.Core.Models;

namespace Area11.Core.Services;

/// <summary>Independent purchase-cost grant; the existing main financial visibility switch gates both.</summary>
public static class CostPolicy
{
    public static bool CanSee(PermissionService permissions, SettingsService settings, User? user) =>
        permissions is not null && settings is not null && user is not null
        && settings.GetBool(MasterFoundationSettings.ProfitShowMargins, true)
        && permissions.Can(user, Modules.PurchaseCost);
}
