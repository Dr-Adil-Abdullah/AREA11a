using System.Globalization;
using Area11.Core.Utils;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>
/// One place issues every permanent document number (Spec 5.1.12). The counter is advanced
/// inside the caller's transaction, so two counters can never receive the same number.
/// </summary>
public sealed class NumberingService
{
    private static readonly Dictionary<string, (string PrefixKey, string CounterKey)> Kinds =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["sale"] = ("bill.salePrefix", "bill.nextSaleNo"),
            ["purchase"] = ("bill.purchasePrefix", "bill.nextPurchaseNo"),
            ["return"] = ("bill.returnPrefix", "bill.nextReturnNo"),
            ["provisional"] = ("bill.provisionalPrefix", "bill.nextProvisionalNo"),
            ["supplierReturn"] = ("bill.supplierReturnPrefix", "bill.nextSupplierReturnNo"),
            ["stockTake"] = ("bill.stockTakePrefix", "bill.nextStockTakeNo"),
            ["quarantine"] = ("bill.quarantinePrefix", "bill.nextQuarantineNo")
        };

    private readonly SettingsService _settings;

    public NumberingService(SettingsService settings)
    {
        _settings = settings;
    }

    public string Take(SqliteConnection connection, string kind)
    {
        if (!Kinds.TryGetValue(kind, out var keys))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown document kind.");
        }

        var next = _settings.GetInt(keys.CounterKey, 1);
        if (next < 1)
        {
            next = 1;
        }

        var prefix = _settings.GetString(keys.PrefixKey);
        var padding = Math.Clamp(_settings.GetInt("bill.numberPadding", 4), 1, 10);

        _settings.WriteCounter(connection, keys.CounterKey, next + 1);

        return prefix + next.ToString("D" + padding.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }
}
