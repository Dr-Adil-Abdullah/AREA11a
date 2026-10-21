using System.Globalization;
using System.Text.Json;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>
/// The 75 settings of the shop: the 70 from the reference app, the five printer keys (D-23),
/// the two cart/hold numbering keys, the return-restock flag, and the permission defaults.
/// A key that is not in <see cref="Defaults"/> cannot be written - a typo becomes an error, not a
/// silent second setting.
/// Values are stored as JSON so a string stays a string, a number stays a number and a
/// list stays a list (Spec 6.1). Every change is audited with old → new.
/// </summary>
public sealed class SettingsService
{
    private static readonly IReadOnlyDictionary<string, string> Defaults = BuildDefaults();

    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly LogService _log;
    private readonly Dictionary<string, string> _cache = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public SettingsService(Database db, AuditService audit, LogService log)
    {
        _db = db;
        _audit = audit;
        _log = log;
        Reload();
    }

    public static IReadOnlyCollection<string> KnownKeys => Defaults.Keys.ToArray();

    /// <summary>Writes any key that is missing in the database (first start or after an update).</summary>
    public int EnsureDefaults()
    {
        var inserted = 0;

        _db.Tx(connection =>
        {
            foreach (var pair in Defaults)
            {
                var exists = Convert.ToInt64(
                    _db.Scalar(connection, "SELECT COUNT(1) FROM settings WHERE key = @key;", new { key = pair.Key }) ?? 0L);

                if (exists > 0)
                {
                    continue;
                }

                _db.Execute(connection,
                    "INSERT INTO settings(key, value, updated_at) VALUES(@key, @value, @at);",
                    new { key = pair.Key, value = pair.Value, at = Clock.Now() });

                inserted++;
            }
        });

        Reload();
        return inserted;
    }

    public void Reload()
    {
        var rows = new List<SettingRow>();

        try
        {
            using var connection = _db.Open();
            rows = _db.Query<SettingRow>(connection, "SELECT key, value, updated_at FROM settings;");
        }
        catch (SqliteException exception)
        {
            // The very first start runs before the migration; defaults are enough until then.
            _log.Warn($"Settings could not be read yet ({exception.Message}); using defaults.");
        }

        lock (_gate)
        {
            _cache.Clear();

            foreach (var pair in Defaults)
            {
                _cache[pair.Key] = pair.Value;
            }

            foreach (var row in rows)
            {
                if (Defaults.ContainsKey(row.Key))
                {
                    _cache[row.Key] = row.Value;
                }
            }
        }
    }

    public string GetRaw(string key)
    {
        lock (_gate)
        {
            return _cache.TryGetValue(key, out var value) ? value : "null";
        }
    }

    public string GetString(string key)
    {
        var raw = GetRaw(key);

        try
        {
            return JsonSerializer.Deserialize<string>(raw) ?? string.Empty;
        }
        catch (JsonException)
        {
            return raw.Trim('"');
        }
    }

    public long GetLong(string key, long fallback = 0) =>
        long.TryParse(GetRaw(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    public int GetInt(string key, int fallback = 0) =>
        int.TryParse(GetRaw(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    public double GetDouble(string key, double fallback = 0) =>
        double.TryParse(GetRaw(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    public bool GetBool(string key, bool fallback = false)
    {
        var raw = GetRaw(key);
        if (bool.TryParse(raw, out var value))
        {
            return value;
        }

        return GetLong(key, fallback ? 1 : 0) != 0;
    }

    public List<T> GetList<T>(string key)
    {
        try
        {
            return JsonSerializer.Deserialize<List<T>>(GetRaw(key), Json.ReadOptions) ?? new List<T>();
        }
        catch (JsonException exception)
        {
            _log.Error($"Setting '{key}' is not a valid list.", exception);
            return new List<T>();
        }
    }

    /// <summary>Owner-visible change: old → new is written into the black box.</summary>
    public ServiceResult Set(string key, object? value, User? actor) =>
        SetMany(new Dictionary<string, object?> { [key] = value }, actor);

    /// <summary>
    /// Saves several settings as ONE transaction: either every value is stored or none is.
    /// The Settings screen edits a dozen fields at once, and half a page of saved values would be
    /// worse than a refusal (the paper width could change while the copy count does not).
    /// </summary>
    public ServiceResult SetMany(IReadOnlyDictionary<string, object?> values, User? actor)
    {
        foreach (var pair in values)
        {
            if (!Defaults.ContainsKey(pair.Key))
            {
                return ServiceResult.Fail($"Unknown setting '{pair.Key}'.");
            }

            var refusal = Guard(pair.Key, pair.Value, values);
            if (refusal is not null)
            {
                return ServiceResult.Fail(refusal);
            }
        }

        try
        {
            _db.Tx(connection =>
            {
                foreach (var pair in values)
                {
                    var json = JsonSerializer.Serialize(pair.Value);

                    var old = _db.Scalar(connection,
                        "SELECT value FROM settings WHERE key = @key;", new { key = pair.Key })?.ToString();

                    _db.Execute(connection, @"
INSERT INTO settings(key, value, updated_at) VALUES(@key, @value, @at)
ON CONFLICT(key) DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at;",
                        new { key = pair.Key, value = json, at = Clock.Now() });

                    _audit.Record(connection, actor, "Settings", "settings_change", "settings", pair.Key, old, json);
                }
            });

            lock (_gate)
            {
                foreach (var pair in values)
                {
                    _cache[pair.Key] = JsonSerializer.Serialize(pair.Value);
                }
            }

            return ServiceResult.Success();
        }
        catch (Exception exception)
        {
            _log.Error($"Settings could not be saved ({string.Join(", ", values.Keys)}).", exception);
            return ServiceResult.Fail("The settings could not be saved.");
        }
    }

    /// <summary>
    /// The few keys where a wrong number breaks the shop. The screen is not the only doorway into
    /// this service, so the rule lives here (D-26: a printed receipt cannot be un-printed).
    /// </summary>
    private string? Guard(string key, object? value, IReadOnlyDictionary<string, object?> incoming)
    {
        double Other(string otherKey, double fallback)
        {
            if (incoming.TryGetValue(otherKey, out var provided) && ToNumber(provided) is { } providedNumber)
            {
                return providedNumber;
            }

            return GetDouble(otherKey, fallback);
        }

        var number = ToNumber(value);

        switch (key)
        {
            case "printer.width":
                return number is 58 or 80 ? null : "The paper width must be 58 or 80 mm.";

            case "printer.copies":
                return number is >= 1 and <= 3 ? null : "The number of copies must be 1, 2 or 3.";

            case "discount.maxPercentCashier":
            case "discount.maxPercentManager":
            {
                if (number is null or < 0 or > 100)
                {
                    return "A discount limit is a percentage between 0 and 100.";
                }

                var cashier = key == "discount.maxPercentCashier" ? number.Value : Other("discount.maxPercentCashier", 5);
                var manager = key == "discount.maxPercentManager" ? number.Value : Other("discount.maxPercentManager", 20);

                return cashier <= manager
                    ? null
                    : "A cashier cannot be allowed a bigger discount than a manager.";
            }

            case "tax.percent":
                return number is >= 0 and <= 100 ? null : "The tax rate must be between 0 and 100 percent.";

            case "security.pinLength":
                return number is >= 3 and <= 8 ? null : "The PIN length must be between 3 and 8 digits.";

            case "bill.numberPadding":
                return number is >= 1 and <= 8 ? null : "The bill number needs 1 to 8 digits.";

            case "bill.roundTo":
                return number is >= 0 and <= 1000 ? null : "The round-off step must be between 0 and 1000 paisa.";

            default:
                return null;
        }
    }

    private static double? ToNumber(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case bool:
                return null;
            case double d:
                return d;
            case float f:
                return f;
            case decimal m:
                return (double)m;
            case int i:
                return i;
            case long l:
                return l;
            case short sh:
                return sh;
        }

        return double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture),
            NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    /// <summary>
    /// Counter advance used by the numbering service. Deliberately NOT audited: one audit row
    /// per printed bill would bury the real events in the black box.
    /// </summary>
    internal void WriteCounter(SqliteConnection connection, string key, long value)
    {
        if (!Defaults.ContainsKey(key))
        {
            throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown counter setting.");
        }

        var json = value.ToString(CultureInfo.InvariantCulture);

        _db.Execute(connection, @"
INSERT INTO settings(key, value, updated_at) VALUES(@key, @value, @at)
ON CONFLICT(key) DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at;",
            new { key, value = json, at = Clock.Now() });

        lock (_gate)
        {
            _cache[key] = json;
        }
    }

    private static Dictionary<string, string> BuildDefaults()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        void Text(string key, string value) => map[key] = JsonSerializer.Serialize(value);
        void Number(string key, double value) => map[key] = value.ToString("0.################", CultureInfo.InvariantCulture);
        void Flag(string key, bool value) => map[key] = value ? "true" : "false";
        void Raw(string key, string json) => map[key] = json;

        // ---- Printer (D-05, D-23). An empty printer.name means "the Windows default printer". ----
        Text("printer.name", string.Empty);
        Number("printer.width", 58);
        Flag("printer.autoCut", true);
        Number("printer.copies", 1);
        Flag("printer.autoPrint", true);

        // ---- Store and branding (E-02, E-03) ----
        Text("store.name", "Area11 Pharmacy");
        Text("store.address", string.Empty);
        Text("store.phone", string.Empty);
        Text("store.footerNote", "Thank you for your visit.");
        Text("store.terms", "Medicines are returnable within 7 days with the original receipt.");
        Text("brand.appName", "Area11");
        Text("brand.shortName", "A11");
        Text("brand.logoDataUrl", string.Empty);
        Text("brand.primaryColor", "#0e7490");

        // ---- Billing and numbering (Spec 12.5.12) ----
        Text("bill.purchasePrefix", "PINV-");
        Text("bill.salePrefix", "INV-");
        Number("bill.numberPadding", 4);
        Number("bill.nextPurchaseNo", 1);
        Number("bill.nextSaleNo", 1);
        Text("bill.provisionalPrefix", "PR-");
        Number("bill.nextProvisionalNo", 1);
        Text("bill.returnPrefix", "RET-");
        Number("bill.nextReturnNo", 1);
        Text("bill.quarantinePrefix", "Q-");
        Number("bill.nextQuarantineNo", 1);
        Text("bill.supplierReturnPrefix", "SR-");
        Number("bill.nextSupplierReturnNo", 1);
        Text("bill.stockTakePrefix", "ST-");
        Number("bill.nextStockTakeNo", 1);
        Text("bill.holdPrefix", "H-");
        Number("bill.nextHoldNo", 1);
        Text("bill.roundMode", "down10");
        Number("bill.roundTo", 10);

        // ---- Tax (Q-09 default off) ----
        Flag("tax.enabled", false);
        Number("tax.percent", 0);
        Text("tax.label", "Sales Tax");

        // ---- Discount (Q-08 margin default) ----
        Flag("discount.enabled", true);
        Text("discount.mode", "margin");
        Number("discount.maxPercentCashier", 5);
        Number("discount.maxPercentManager", 20);
        Flag("discount.blockBelowCost", true);

        // ---- Expiry and reorder ----
        Raw("expiry.levels",
            "[{\"level\":1,\"days\":365,\"color\":\"blue\",\"label\":\"Level 1 - 1 year\"}," +
            "{\"level\":2,\"days\":180,\"color\":\"yellow\",\"label\":\"Level 2 - 6 months\"}," +
            "{\"level\":3,\"days\":90,\"color\":\"red\",\"label\":\"Level 3 - 3 months\"}]");
        Flag("reorder.enabled", true);

        // ---- Loyalty (off until the last phase) ----
        Flag("loyalty.enabled", false);
        Number("loyalty.rupeesPerPoint", 100);
        Number("loyalty.vipThreshold", 500);

        // ---- Receipt layout (the printer itself is declared once, at the top of this method) ----
        Text("receipt.datePosition", "top");
        Flag("receipt.showOriginalPrice", true);
        Flag("receipt.showDiscount", true);
        Flag("receipt.showSavings", false);
        Flag("receipt.showCostColumns", false);

        // ---- Payments ----
        Raw("payment.methods", "[\"cash\",\"credit\"]");
        Text("payment.default", "cash");

        // ---- Stock (U-25, U-30) ----
        Flag("stock.allowNegative", true);
        Flag("stock.warnNegative", true);

        // ---- Returns (U-28, D-02) ----
        Flag("returns.requireInvoice", true);
        Number("returns.maxDays", 0);
        Flag("returns.cashierRestockToShelf", false);

        // ---- Credit ----
        Flag("credit.blockOverLimit", false);
        Flag("credit.managerCanOverride", true);

        // ---- Security ----
        Number("security.pinLength", 4);
        Number("security.autoLockMinutes", 15);
        Number("security.sessionHours", 12);
        Flag("security.requireLogin", true);

        // ---- Backup ----
        Flag("backup.autoEnabled", false);
        Number("backup.everyHours", 24);
        Number("backup.keep", 7);

        // ---- Cloud sync (later phase) ----
        Flag("sync.enabled", false);
        Text("sync.url", string.Empty);
        Text("sync.key", string.Empty);
        Text("sync.table", "area11_sync");
        Text("sync.shopId", "shop-1");
        Text("sync.status", "not_configured");
        Text("sync.lastSyncAt", string.Empty);

        // ---- Owner ----
        Text("owner.name", "Owner");

        return map;
    }
}
