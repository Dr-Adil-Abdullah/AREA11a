using Area11.WPF.Data;
using Area11.WPF.Utils;

namespace Area11.WPF.Services;

/// <summary>
/// First-run bootstrap: settings, the owner account and the starter categories.
/// Existing data is never overwritten, so this is safe on every launch.
/// </summary>
public static class DatabaseSeed
{
    private static readonly string[] StarterCategories =
    {
        "Medicines",
        "Surgical",
        "Cosmetics",
        "Baby Care",
        "General"
    };

    public static void Apply(Database db, string defaultOwnerPassword)
    {
        using var connection = db.Open();
        using var transaction = connection.BeginTransaction();

        foreach (var (key, value) in SettingsDefaults.All)
        {
            db.Exec(connection, "INSERT OR IGNORE INTO settings(key, value) VALUES($key, $value);",
                ("$key", key),
                ("$value", value));
        }

        var ownerCount = db.Scalar<long>(connection, "SELECT COUNT(1) FROM users WHERE role = 'Owner';");
        if (ownerCount == 0)
        {
            db.Exec(connection, @"
                INSERT INTO users(name, role, secret_hash, active, created_at)
                VALUES('Owner', 'Owner', $hash, 1, $at);",
                ("$hash", PasswordHasher.Hash(defaultOwnerPassword)),
                ("$at", FormatHelper.Stamp()));
        }

        var categoryCount = db.Scalar<long>(connection, "SELECT COUNT(1) FROM categories;");
        if (categoryCount == 0)
        {
            foreach (var name in StarterCategories)
            {
                db.Exec(connection, "INSERT INTO categories(name, active) VALUES($name, 1);", ("$name", name));
            }
        }

        transaction.Commit();
    }
}
