using System.Globalization;
using System.Text.Json;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>
/// The 70 reference settings plus the two additions agreed with the owner
/// (returns.cashierRestockToShelf, and the permission defaults live in code).
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
    public ServiceResult Set(string key, object? value, User? actor)
    {
        if (!Defaults.ContainsKey(key))
        {
            return ServiceResult.Fail($"Unknown setting '{key}'.");
        }

        var json = JsonSerializer.Serialize(value);

        try
        {
            _db.Tx(connection =>
            {
                var old = _db.Scalar(connection, "SELECT value FROM settings WHERE key = @key;", new { key })?.ToString();

                _db.Execute(connection, @"
INSERT INTO settings(key, value, updated_at) VALUES(@key, @value, @at)
ON CONFLICT(key) DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at;",
                    new { key, value = json, at = Clock.Now() });

                _audit.Record(connection, actor, "Settings", "settings_change", "settings", key, old, json);
            });

            lock (_gate)
            {
                _cache[key] = json;
            }

            return ServiceResult.Success();
        }
        catch (Exception exception)
        {
            _log.Error($"Setting '{key}' could not be saved.", exception);
            return ServiceResult.Fail("The setting could not be saved.");
        }
    }

    public ServiceResult SetMany(IReadOnlyDictionary<string, object?> values, User? actor)
    {
        foreach (var pair in values)
        {
            var result = Set(pair.Key, pair.Value, actor);
            if (!result.Ok)
            {
                return result;
            }
        }

        return ServiceResult.Success();
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
