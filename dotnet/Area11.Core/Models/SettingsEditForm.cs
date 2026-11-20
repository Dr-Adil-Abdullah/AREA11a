using System.Globalization;
using System.Text.Json;
using Area11.Core.Data;

namespace Area11.Core.Models;

/// <summary>Immutable detached settings read. Never a shared cache or durable request identity.
/// Raw credentials/recovery hashes are not exposed;only authorized cloud key presence is shown.</summary>
public sealed class SettingsEditForm
{
    private readonly Dictionary<string, string> _values;
    internal Database Database { get; }
    internal IReadOnlyDictionary<string, string> Baselines { get; }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasCloudKey { get; }

    internal SettingsEditForm(Database database, Dictionary<string, string> values,
        Dictionary<string, string> baselines, bool hasCloudKey)
    { Database = database; _values = new(values, StringComparer.Ordinal); Baselines = new Dictionary<string, string>(baselines, StringComparer.Ordinal); HasCloudKey = hasCloudKey; }

    internal SettingsEditForm WithSaved(Dictionary<string, string> values, Dictionary<string, string> baselines, bool? hasCloudKey)
    {
        var nextValues = new Dictionary<string, string>(_values, StringComparer.Ordinal);
        var nextBaselines = Baselines.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        foreach (var pair in values) nextValues[pair.Key] = pair.Value;
        foreach (var pair in baselines) nextBaselines[pair.Key] = pair.Value;
        return new SettingsEditForm(Database, nextValues, nextBaselines, hasCloudKey ?? HasCloudKey);
    }

    public string GetRaw(string key) => _values.TryGetValue(key, out var raw) ? raw : "null";
    public string GetString(string key)
    {
        var raw = GetRaw(key);
        try { return JsonSerializer.Deserialize<string>(raw) ?? string.Empty; }
        catch (JsonException) { return raw.Trim('"'); }
    }
    public long GetLong(string key, long fallback = 0) => long.TryParse(GetRaw(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    public int GetInt(string key, int fallback = 0) => int.TryParse(GetRaw(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    public double GetDouble(string key, double fallback = 0) => double.TryParse(GetRaw(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    public bool GetBool(string key, bool fallback = false) => bool.TryParse(GetRaw(key), out var value) ? value : GetLong(key, fallback ? 1 : 0) != 0;
    public ExpiryLevels ExpiryBands => ExpiryLevels.Read(GetRaw("expiry.levels"));
}
