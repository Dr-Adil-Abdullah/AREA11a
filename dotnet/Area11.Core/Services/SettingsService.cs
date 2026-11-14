using System.Globalization;
using System.Text.Json;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>
/// The shop's settings: business rules, printing, receipt text, menu preferences and the catalog
/// of application section labels. A key that is not in <see cref="Defaults"/> cannot be written -
/// a typo becomes an error, not a
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
    private readonly PermissionService? _permissions;
    private readonly Dictionary<string, string> _cache = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public SettingsService(Database db, AuditService audit, LogService log, PermissionService? permissions = null)
    {
        _db = db;
        _audit = audit;
        _log = log;
        _permissions = permissions;
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

    /// <summary>
    /// The owner's three expiry bands, read fresh from `expiry.levels` (F-29). A broken value is
    /// repaired by <see cref="ExpiryLevels.Read"/> instead of crashing a screen.
    /// </summary>
    public ExpiryLevels ExpiryBands => ExpiryLevels.Read(GetRaw("expiry.levels"));

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

    /// <summary>
    /// A **silent value** (the session heartbeat, a last-seen stamp) is stored like any other setting,
    /// but it is deliberately **not** written into the black box: it changes nothing about money or
    /// stock, and a stamp written every 30 seconds would push the real changes off the page.
    ///
    /// The key must still exist in <see cref="Defaults"/> - an unknown key is a bug in the caller,
    /// not a preference, and is refused rather than written.
    /// </summary>
    public ServiceResult SetUiOnly(string key, object? value)
    {
        if (!Defaults.ContainsKey(key))
        {
            return ServiceResult.Fail($"Unknown setting '{key}'.");
        }

        if (key == AuthService.RecoveryWordKey)
            return ServiceResult.Fail("The recovery word must use the owner-only secret settings path.");
        if (key == FocusGuardService.GuardKey)
            return ServiceResult.Fail("Screen lock must use the audited owner settings path.");
        if (key is "discount.billBasis" or "discount.blockBelowCost")
            return ServiceResult.Fail("Pricing policy must use the audited owner settings path.");

        // Application words are owner-visible settings, not transient UI preferences. Keep them on
        // the validated, permission-checked and audited SetMany/AppLabelService path.
        if (key.StartsWith("labels.", StringComparison.Ordinal))
        {
            return ServiceResult.Fail("Application labels must be saved through the audited settings path.");
        }
        if (key.StartsWith("appearance.", StringComparison.Ordinal))
        {
            return ServiceResult.Fail("Application appearance must be saved through the audited settings path.");
        }
        if (string.Equals(key, "brand.appName", StringComparison.Ordinal))
        {
            return ServiceResult.Fail("The application name must be saved through the audited settings path.");
        }

        try
        {
            var json = JsonSerializer.Serialize(value);

            // A block-bodied lambda on purpose: it can only mean "no result", so the call cannot be
            // read either way by the compiler or by the next reader.
            _db.Tx(connection =>
            {
                _db.Execute(connection, @"
INSERT INTO settings(key, value, updated_at) VALUES(@key, @value, @at)
ON CONFLICT(key) DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at;",
                    new { key, value = json, at = Clock.Now() });
            });

            lock (_gate)
            {
                _cache[key] = json;
            }

            return ServiceResult.Success();
        }
        catch (Exception exception)
        {
            _log.Error($"The preference '{key}' could not be saved.", exception);
            return ServiceResult.Fail("The preference could not be saved.");
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
        if (values.ContainsKey(AuthService.RecoveryWordKey))
            return ServiceResult.Fail("The recovery word must use the owner-only secret settings path.");
        if (values.TryGetValue("discount.billBasis", out var billBasis))
        {
            if (!IsCurrentOwner(actor)) return ServiceResult.Fail("Only the owner can change the default discount basis.");
            if (billBasis is not string basis || basis is not ("profit" or "retail"))
                return ServiceResult.Fail("Choose profit or retail for the bill discount basis.");
        }
        if (values.TryGetValue("discount.blockBelowCost", out var belowCost) && belowCost is not true)
            return ServiceResult.Fail("Selling below purchase cost is not allowed for any role.");

        // Labels can also be saved as part of the Settings screen's larger atomic dictionary. Guard
        // that real path here as well as AppLabelService.Save, so a direct Core caller cannot bypass
        // the Settings permission. A manually constructed service without permissions fails closed.
        if (values.Keys.Any(key => key.StartsWith("labels.", StringComparison.Ordinal))
            && _permissions?.Can(actor, Modules.Settings) != true)
        {
            return ServiceResult.Fail("You are not allowed to change the application labels.");
        }

        if (values.ContainsKey("appearance.productPhotoSize")
            && _permissions?.Can(actor, Modules.Settings) != true)
        {
            return ServiceResult.Fail("You are not allowed to change the product photo size.");
        }

        if (values.ContainsKey("appearance.iconFileName") && actor?.IsOwner != true)
        {
            return ServiceResult.Fail("Only the owner can change the application icon.");
        }

        if (values.ContainsKey("lockdown.focusGuard") && actor?.IsOwner != true)
        {
            return ServiceResult.Fail("Only the owner can change the screen lock.");
        }

        // Owner's word (Nov 2026, reverses D-77): the window title shows his own app name.
        if (values.TryGetValue("brand.appName", out var appName))
        {
            if (actor?.IsOwner != true)
            {
                return ServiceResult.Fail("Only the owner can change the application name.");
            }

            var clean = appName?.ToString()?.Trim() ?? string.Empty;

            if (clean.Length == 0 || clean.Length > 40)
            {
                return ServiceResult.Fail("The application name must be 1 to 40 letters.");
            }
        }

        var ownerOnly = values.Keys.Any(key => key is "discount.billBasis" or "appearance.iconFileName"
            or "brand.appName" or "lockdown.focusGuard");
        if (ownerOnly && !IsCurrentOwner(actor))
            return ServiceResult.Fail("Only the active owner can change these protected settings.");

        var preparation = PrepareBulk(values);
        if (!preparation.Ok || preparation.Value is null)
        {
            return ServiceResult.Fail(preparation.Error ?? "The settings could not be validated.");
        }

        var prepared = preparation.Value;

        try
        {
            _db.Tx(connection =>
            {
                if (ownerOnly) OwnerAccess.Require(_db, connection, actor);
                foreach (var pair in values)
                {
                    var json = prepared[pair.Key];

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
                    _cache[pair.Key] = prepared[pair.Key];
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

    internal ServiceResult SaveRecoveryHash(User actor, string hash)
    {
        try
        {
            var json = JsonSerializer.Serialize(hash);
            _db.Tx(connection =>
            {
                var owner = OwnerAccess.Require(_db, connection, actor);
                _db.Execute(connection, @"
INSERT INTO settings(key, value, updated_at) VALUES(@key, @value, @at)
ON CONFLICT(key) DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at;",
                    new { key = AuthService.RecoveryWordKey, value = json, at = Clock.Now() });
                _audit.Record(connection, owner, "Auth", "recovery_word_changed",
                    details: hash.Length == 0 ? "back to the factory word" : "changed");
            });
            lock (_gate) { _cache[AuthService.RecoveryWordKey] = json; }
            return ServiceResult.Success();
        }
        catch (ValidationException exception) { return ServiceResult.Fail(exception.Message); }
        catch (Exception exception)
        {
            _log.Error("The recovery word could not be saved.", exception);
            return ServiceResult.Fail("The change could not be saved.");
        }
    }

    private bool IsCurrentOwner(User? actor)
    {
        if (actor?.IsOwner != true) return false;
        try
        {
            using var connection = _db.Open();
            return _db.QuerySingle<User>(connection,
                "SELECT id, name, role, active FROM users WHERE id = @id;", new { id = actor.Id })
                is { IsOwner: true, Active: true };
        }
        catch (Exception exception)
        {
            _log.Error("The owner account could not be verified for pricing settings.", exception);
            return false;
        }
    }

    /// <summary>
    /// Validates and serialises settings without opening a transaction. The native workbook preview
    /// uses this exact same guard, then applies the prepared values inside the workbook's ONE database
    /// transaction. It is internal so an arbitrary UI cannot bypass the normal permission path.
    /// </summary>
    internal ServiceResult<IReadOnlyDictionary<string, string>> PrepareBulk(
        IReadOnlyDictionary<string, object?> values)
    {
        var prepared = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var pair in values)
        {
            if (!Defaults.ContainsKey(pair.Key))
            {
                return ServiceResult<IReadOnlyDictionary<string, string>>.Fail($"Unknown setting '{pair.Key}'.");
            }

            if (pair.Key == "discount.billBasis" && pair.Value is not ("profit" or "retail"))
                return ServiceResult<IReadOnlyDictionary<string, string>>.Fail("Choose profit or retail for the bill discount basis.");
            if (pair.Key == "discount.blockBelowCost" && pair.Value is not true)
                return ServiceResult<IReadOnlyDictionary<string, string>>.Fail("Purchase-cost protection cannot be disabled.");
            var refusal = Guard(pair.Key, pair.Value, values);
            if (refusal is not null)
            {
                return ServiceResult<IReadOnlyDictionary<string, string>>.Fail(refusal);
            }

            prepared[pair.Key] = Serialise(pair.Key, pair.Value);
        }

        return ServiceResult<IReadOnlyDictionary<string, string>>.Success(prepared);
    }

    /// <summary>The text of a setting exactly as it was typed, trimmed - never null.</summary>
    private static string TextValue(object? value) => (value as string ?? string.Empty).Trim();

    /// <summary>
    /// A name that will be part of a URL and of the cloud row's key: letters, digits and `_`, plus `-`
    /// for the shop id. An empty name is allowed - it simply means "not set up yet".
    /// </summary>
    private static string? CleanName(string name, string what, bool allowDash, int maxLength)
    {
        if (name.Length > maxLength)
        {
            return what + " cannot be longer than "
                + maxLength.ToString(CultureInfo.InvariantCulture) + " characters.";
        }

        foreach (var character in name)
        {
            var allowed = char.IsLetterOrDigit(character)
                          || character == '_'
                          || (allowDash && character == '-');

            if (!allowed)
            {
                return what + " can only use letters, digits, _ "
                    + (allowDash ? "and - (no spaces)." : "(no spaces).");
            }
        }

        return null;
    }

    /// <summary>
    /// Turns what a caller handed to <see cref="SetMany"/> into the text the settings row keeps.
    ///
    /// Why every key is not simply serialised: `expiry.levels` is a JSON **array**, and `Guard`
    /// deliberately accepts it both as three objects (the Settings page) and as JSON text (an import,
    /// a hand-edited file, an older test). Serialising the text turned it into a *quoted string*
    /// (`"[\{...\}]"`), `ExpiryLevels.Read` could not parse that, and the shop silently went back to
    /// 365 / 180 / 90 - the owner's PC reported it as "expected 0, got 1" on the home screen.
    /// A quiet fallback to a default is the worst kind of bug: nothing looks broken.
    /// </summary>
    private static string Serialise(string key, object? value)
    {
        // AppLabelService already trims its own writes; keep the direct SettingsService route just
        // as strict so the stored value and its old/new audit match the text the shell displays.
        if (key.StartsWith("labels.", StringComparison.Ordinal))
        {
            return JsonSerializer.Serialize(TextValue(value), Json.WriterOptions);
        }

        if (key == "expiry.levels")
        {
            var bands = value switch
            {
                string text => ExpiryLevels.ParseBands(text),
                JsonElement element => ExpiryLevels.ParseBands(element.GetRawText()),
                IEnumerable<ExpiryBand> list => list.ToList(),
                _ => null
            };

            if (bands is not null && bands.Count > 0)
            {
                return JsonSerializer.Serialize(bands, Json.WriterOptions);
            }
        }

        return JsonSerializer.Serialize(value, Json.WriterOptions);
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

        if (key.StartsWith("labels.", StringComparison.Ordinal))
        {
            return AppLabelService.GuardSetting(key, value);
        }

        var number = ToNumber(value);

        switch (key)
        {
            case "appearance.iconFileName":
                var iconName = TextValue(value);
                return iconName.Length == 0 || (iconName.Length == 48
                    && iconName.StartsWith("area11-icon-", StringComparison.Ordinal)
                    && iconName.EndsWith(".ico", StringComparison.Ordinal)
                    && Guid.TryParseExact(iconName.Substring(12, 32), "N", out _))
                    ? null : "The application icon name is invalid.";

            case "appearance.productPhotoSize":
                return TextValue(value) is "medium" or "large"
                    ? null : "Choose medium or large product photos.";

            case "printer.width":
                return number is 58 or 80 ? null : "The paper width must be 58 or 80 mm.";

            case "expiry.levels":
            {
                // The screen sends the three bands as objects, so they are stored exactly like the
                // shipped default (an array of level/days/color/label). Older or hand-edited text
                // still arrives as a JSON string, so both shapes are checked.
                var bands = value switch
                {
                    string json => ExpiryLevels.ParseBands(json),
                    JsonElement element => ExpiryLevels.ParseBands(element.GetRawText()),
                    IEnumerable<ExpiryBand> list => list.ToList(),
                    _ => new List<ExpiryBand>()
                };

                return ExpiryLevels.IsDistinct(bands)
                    ? null
                    : "The three expiry levels must be three different numbers of days, all above zero.";
            }

            case "printer.copies":
                return number is >= 1 and <= 3 ? null : "The number of copies must be 1, 2 or 3.";

            case "printer.bulkFormat":
                var bulkFormat = TextValue(value);
                return bulkFormat is MasterFoundationSettings.SeparateReceipts or MasterFoundationSettings.A4List
                    ? null
                    : "Choose separate receipts or an A4 list for bulk printing.";

            case "whatsapp.exportDirectory":
                var exportDirectory = TextValue(value);
                return exportDirectory.Length <= 260 && !exportDirectory.Any(char.IsControl)
                    ? null
                    : "The WhatsApp PDF folder must be one path of 260 characters or fewer.";

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

            case "supplier.returnCutoffDays":
            case "supplier.returnAlertDays":
            {
                // Two numbers that only make sense together: the reminder must start before the
                // supplier's last day, otherwise the alert can never appear.
                if (number is null or < 1 or > 3650)
                {
                    return "The supplier return window is a number of days between 1 and 3650.";
                }

                var cutoff = key == "supplier.returnCutoffDays"
                    ? number.Value
                    : Other("supplier.returnCutoffDays", 90);

                var alert = key == "supplier.returnAlertDays"
                    ? number.Value
                    : Other("supplier.returnAlertDays", 180);

                return alert > cutoff
                    ? null
                    : "The reminder must start earlier than the supplier's cutoff (for example 180 and 90 days).";
            }

            case "tax.percent":
                return number is >= 0 and <= 100 ? null : "The tax rate must be between 0 and 100 percent.";

            case "security.pinLength":
                return number is >= 3 and <= 8 ? null : "The PIN length must be between 3 and 8 digits.";

            case "bill.numberPadding":
                return number is >= 1 and <= 8 ? null : "The bill number needs 1 to 8 digits.";

            case "bill.roundTo":
                return number is >= 0 and <= 1000 ? null : "The round-off step must be between 0 and 1000 paisa.";

            // Backup (F-36): a zero-hour interval would make a zip on every sign-in, and keeping zero
            // backups would delete today's file. Both are refused here, not only hidden on the page.
            case "backup.everyHours":
                return number is >= 1 and <= 720
                    ? null
                    : "The automatic backup interval must be between 1 and 720 hours (30 days).";

            case "backup.keep":
                return number is >= 1 and <= 500
                    ? null
                    : "Keep between 1 and 500 automatic backups.";

            // Cloud sync (F-48). The address becomes a URL and the table and shop id become parts of a
            // request and of a row key, so they are checked here and not only on the screen: a shop name
            // typed with a space into `sync.shopId` would silently push under a different key every time.
            case "sync.url":
            {
                var address = TextValue(value);

                return address.Length == 0
                       || address.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                       || address.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : "The cloud address must start with https:// (or http:// on your own network).";
            }

            case "sync.table":
                return CleanName(TextValue(value), "The table name", allowDash: false, 63);

            case "sync.shopId":
                return CleanName(TextValue(value), "The shop id", allowDash: true, 64);

            case "sync.automaticMinutes":
                return number is >= 5 and <= 1440 && number.Value == Math.Floor(number.Value)
                    ? null
                    : "Automatic sync must be a whole number of minutes from 5 to 1440.";

            // Session (v0.35.0, D-92). The idle limit must be one of the choices printed on the page:
            // a number typed wrongly (200) would silently become "never", which is the exact fault the
            // rule exists to prevent. 0 is allowed and means "only the 12-hour limit".
            case "session.idleHours":
                return number is 0 or 1 or 2 or 4 or 6 or 12
                    ? null
                    : "The sign-out time must be 1, 2, 4, 6 or 12 hours (or 0 for the 12-hour limit only).";

            // Startup and close (Portion 16). Three words only: anything else would silently become
            // manual on read, and a mode that silently changes itself is worse than a refusal.
            case "startup.mode":
                return TextValue(value) is "manual" or "auto" or "protected"
                    ? null
                    : "The startup mode must be manual, auto or protected.";

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
    /// Reads a counter on the caller's connection and inside its immediate transaction. A second
    /// settings instance must see the committed database value, not the copy it cached at startup;
    /// repeated numbers inside one transaction must also see the earlier uncommitted increment.
    /// </summary>
    internal long ReadCounter(SqliteConnection connection, string key)
    {
        if (!Defaults.ContainsKey(key))
        {
            throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown counter setting.");
        }

        var raw = Convert.ToString(_db.Scalar(connection,
            "SELECT value FROM settings WHERE key = @key;", new { key }), CultureInfo.InvariantCulture);

        return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    /// <summary>
    /// Counter advance used by the numbering service. Deliberately NOT audited: one audit row
    /// per printed bill would bury the real events in the black box. The cache is deliberately not
    /// changed: the caller's transaction may roll back, and a memory write cannot roll back with it.
    /// Every counter read therefore goes through <see cref="ReadCounter"/> on the live connection.
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
        // Master Phase 1 contracts. Phase 5 owns the actual individual/bulk print actions.
        Flag("printer.defaultBulk", false);
        Text("printer.bulkFormat", MasterFoundationSettings.SeparateReceipts);
        Flag("sale.successPopup", true);

        // The hardware test (F-46): the owner's ticks, the date, the person and the paper width they
        // belong to. Five plain settings, so a shop that upgrades reads the same words a new shop does.
        Text("hardware.testedAt", string.Empty);
        Text("hardware.testedBy", string.Empty);
        Text("hardware.note", string.Empty);
        Number("hardware.testedWidth", 0);
        Text("hardware.passed", string.Empty);

        // Medium and large are deliberately bounded so product grids remain usable.
        Text("appearance.productPhotoSize", "medium");
        Text("appearance.iconFileName", string.Empty);

        // ---- Store and branding (E-02, E-03) ----
        Text("store.name", "Area11 Pharmacy");
        Text("store.address", string.Empty);
        Text("store.phone", string.Empty);
        Text("store.footerNote", "Thank you for your visit.");
        Text("store.terms", "Medicines are returnable within 7 days with the original receipt.");
        Text("brand.appName", "Area11");

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

        // ---- Discount (D-107: future profit default, owner-selectable retail, per-person caps) ----
        Flag("discount.enabled", true);
        // Retired keys retained for old databases/tools; active allowances live per person.
        Text("discount.mode", "retail"); // Retired legacy key, not the active basis.
        Text("discount.billBasis", "profit");
        Number("discount.maxPercentCashier", 5);
        Number("discount.maxPercentManager", 20);
        Flag("discount.blockBelowCost", true);

        // ---- Expiry and reorder ----
        Raw("expiry.levels",
            "[{\"level\":1,\"days\":365,\"color\":\"blue\",\"label\":\"Level 1 - 1 year\"}," +
            "{\"level\":2,\"days\":180,\"color\":\"yellow\",\"label\":\"Level 2 - 6 months\"}," +
            "{\"level\":3,\"days\":90,\"color\":\"red\",\"label\":\"Level 3 - 3 months\"}]");
        // Supplier returns (15.2.1, §9.3.6): goods go back only until the cutoff, and the board
        // starts reminding this many days before expiry.
        Number("supplier.returnCutoffDays", 90);
        Number("supplier.returnAlertDays", 180);

        Flag("reorder.enabled", true);

        // ---- Loyalty (off until the last phase) ----
        Flag("loyalty.enabled", false);
        Number("loyalty.rupeesPerPoint", 100);
        Number("loyalty.vipThreshold", 500);

        // ---- Receipt layout (the printer itself is declared once, at the top of this method) ----
        Text("receipt.datePosition", "top");
        Flag("receipt.showDiscount", true);

        // ---- Stock ----
        // Both legacy keys stay readable so an old database needs no destructive migration. D-93 removed
        // the checkbox and no production rule reads either key; historical alerts follow actual stock.
        Flag("stock.allowNegative", true);
        Flag("stock.warnNegative", true);

        // ---- Returns (U-28, D-02) ----
        Number("returns.maxDays", 0);

        // ---- Credit ----
        Flag("credit.blockOverLimit", false);
        Flag("credit.managerCanOverride", true);

        // ---- Security ----
        Number("security.pinLength", 4);
        Number("security.autoLockMinutes", 15);
        Number("security.sessionHours", 12);
        // The "forgot password" door (Phase 2, owner's word): empty means the factory word
        // ("area11") signs the owner in; otherwise this holds the hash of the owner's own word.
        // Locked out of the workbook - a hash must never travel in a spreadsheet.
        Text("security.recoveryWordHash", string.Empty);

        // ---- Backup (F-36) ----
        // On since v0.63.0 (owner's word, Nov 2026: data loss is the biggest fear). A new shop is
        // protected from its first day; an old shop is switched on by migration 032; the Backup
        // page keeps its one-tick switch for any shop that wants it off.
        Flag("backup.autoEnabled", true);
        Number("backup.everyHours", 24);
        Number("backup.keep", 7);
        // Bookkeeping for the daily backup: when it last ran and which file it made. Kept out of the
        // black box (written with SetUiOnly) - the backup itself writes its own audit line.
        Text("backup.lastRunAt", string.Empty);
        Text("backup.lastFile", string.Empty);
        // Recovery remains visible after the automatic restart that follows a restore.
        Text("backup.lastRestoreSummary", string.Empty);

        // ---- Cloud sync (manual is always available; Q4=C also permits configurable automatic sync) ----
        Flag("sync.enabled", false);
        Text("sync.url", string.Empty);
        Text("sync.key", string.Empty);
        Text("sync.table", "area11_sync");
        Text("sync.shopId", "shop-1");
        Text("sync.status", "not_configured");
        Text("sync.lastSyncAt", string.Empty);
        Text("sync.lastError", string.Empty);
        // Portion 15 is the separately approved Phase 7: the background timer is now allowed, and it
        // stays off until the owner ticks it. Minutes are validated (whole, 5 to 1440) on the save path.
        Flag("sync.automaticEnabled", false);
        Number("sync.automaticMinutes", 15);

        // ---- Master Phase 1 communication/reporting contracts (behavior in Phases 5 and 6) ----
        Flag("whatsapp.preparePdf", true);
        Text("whatsapp.exportDirectory", string.Empty);
        Flag("profit.showMargins", true);

        // ---- Session and the drawer (v0.35.0, D-92) ----
        // How long the counter may stand untouched before the person is signed out (hours; 0 = only
        // the 12-hour limit). Two hours by default: a cashier who bills all day never reaches it, one
        // who walks away with the drawer open does.
        Number("session.idleHours", 2);
        // The note a forgotten session leaves (SessionHandover writes it): empty means nothing is
        // pending, so an old database opens with nothing to complain about.
        Text("session.handoverAt", string.Empty);
        Text("session.handoverBy", string.Empty);

        // ---- Startup and close (Portion 16): manual starts and closes by hand; auto starts with
        // Windows; protected also asks for the password on every close. Manual by default: the
        // shop must opt into auto-start and protection on purpose.
        Text("startup.mode", "manual");

        // ---- Screen lockdown (owner's word, Nov 2026): off until the owner opts in; session.*
        // is the run's own silent telemetry (kill detection), never audited per write.
        Flag("lockdown.focusGuard", false);
        Text("session.lastUser", string.Empty);
        Text("session.lastStart", string.Empty);
        Text("session.lastSeen", string.Empty);
        Flag("session.cleanExit", true);

        // ---- Application section labels (E-12/S-05) ----
        // One catalog supplies the defaults, the editor, the Core guard and the WPF resources.
        foreach (var label in AppLabelService.Definitions)
        {
            Text(label.SettingKey, label.DefaultText);
        }

        return map;
    }
}
