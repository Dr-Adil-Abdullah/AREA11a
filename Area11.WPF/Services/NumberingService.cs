using Area11.WPF.Data;
using Microsoft.Data.Sqlite;

namespace Area11.WPF.Services;

/// <summary>
/// Every document number (INV-0001, PINV-0001, RET-0001, Q-0001, SR-0001, PR-0001, ST-0001)
/// comes from the counters table inside the caller's transaction, so two counters can never
/// receive the same number.
/// </summary>
public sealed class NumberingService
{
    private readonly Database _db;
    private readonly SettingsService _settings;

    public NumberingService(Database db, SettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    public string Take(SqliteConnection connection, string kind)
    {
        var current = _db.Scalar<long>(connection,
            "SELECT value FROM counters WHERE kind = $kind;",
            ("$kind", kind));

        var next = current + 1;

        _db.Exec(connection, @"
            INSERT INTO counters(kind, value) VALUES($kind, $value)
            ON CONFLICT(kind) DO UPDATE SET value = $value;",
            ("$kind", kind),
            ("$value", next));

        var prefix = _settings.Get($"bill.{kind}Prefix");
        var padding = Math.Clamp(_settings.GetInt("bill.numberPadding", 4), 1, 10);

        return prefix + next.ToString(new string('0', padding));
    }
}
