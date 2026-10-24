using Area11.WPF.Data;

namespace Area11.WPF.Services;

/// <summary>
/// Database-backed settings shared by every screen and counter.
/// A change made in the UI is written to the database immediately and audited.
/// </summary>
public sealed class SettingsService
{
    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly LoggerService _log;
    private readonly Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

    public SettingsService(Database db, AuditService audit, LoggerService log)
    {
        _db = db;
        _audit = audit;
        _log = log;

        using var connection = _db.Open();
        foreach (var (key, value) in _db.Query(
                     connection,
                     "SELECT key, value FROM settings;",
                     reader => (reader.Str("key"), reader.Str("value"))))
        {
            _cache[key] = value;
        }
    }

    public string Get(string key)
    {
        if (_cache.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
        {
            return value;
        }

        return SettingsDefaults.All.TryGetValue(key, out var fallback) ? fallback : string.Empty;
    }

    public int GetInt(string key, int fallback = 0) =>
        int.TryParse(Get(key), out var value) ? value : fallback;

    public bool GetBool(string key) =>
        bool.TryParse(Get(key), out var value) && value;

    public IReadOnlyDictionary<string, string> All() => _cache;

    public OperationResult Save(string key, string value, string userName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(key) || !_cache.ContainsKey(key))
            {
                return OperationResult.Fail($"Unknown setting: {key}");
            }

            var oldValue = Get(key);
            _db.Tx(connection =>
            {
                _db.Exec(
                    connection,
                    "UPDATE settings SET value = $value WHERE key = $key;",
                    ("$value", value),
                    ("$key", key));

                _audit.Record(connection, userName, "Settings", "settings_change", "settings", null,
                    new { key, value = oldValue },
                    new { key, value });
            });

            _cache[key] = value;
            return OperationResult.Success();
        }
        catch (Exception exception)
        {
            _log.Error($"Setting '{key}' could not be saved.", exception);
            return OperationResult.Fail($"Setting '{key}' could not be saved. Please try again.");
        }
    }
}
