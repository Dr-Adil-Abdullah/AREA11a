using Area11.Core.Data;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>
/// First start only: the owner account and the five example categories (Spec 4.2.3, 8.2.3).
/// The default owner password is area11 and must be changed on day one — the app says so on
/// the login screen until it happens.
/// </summary>
public sealed class SeedService
{
    private readonly Database _db;
    private readonly LogService _log;

    public SeedService(Database db, LogService log)
    {
        _db = db;
        _log = log;
    }

    public bool Seed()
    {
        try
        {
            return _db.Tx(connection =>
            {
                var changed = false;

                var users = Convert.ToInt64(
                    _db.Scalar(connection, "SELECT COUNT(1) FROM users;") ?? 0L);

                if (users == 0)
                {
                    _db.Execute(connection, @"
INSERT INTO users(name, role, password_hash, active, created_at)
VALUES('Owner', 'owner', @hash, 1, @at);",
                        new { hash = PasswordHasher.Hash("area11"), at = Clock.Now() });

                    changed = true;
                    _log.Info("First start: the owner account was created (password must be changed).");
                }

                var seededCategories = new[] { "Medicine", "Surgical", "Cosmetics", "General", "Baby Care" };
                for (var index = 0; index < seededCategories.Length; index++)
                {
                    var name = seededCategories[index];
                    var exists = Convert.ToInt64(_db.Scalar(connection,
                        "SELECT COUNT(1) FROM categories WHERE name = @name;", new { name }) ?? 0L);

                    if (exists > 0)
                    {
                        continue;
                    }

                    _db.Execute(connection,
                        "INSERT INTO categories(name, sort_order, active, created_at) VALUES(@name, @sort, 1, @at);",
                        new { name, sort = index, at = Clock.Now() });

                    changed = true;
                }

                return changed;
            });
        }
        catch (Exception exception)
        {
            _log.Error("The first-start seed failed.", exception);
            throw;
        }
    }

    public bool UsesDefaultOwnerPassword()
    {
        try
        {
            using var connection = _db.Open();
            var hash = _db.Scalar(connection,
                "SELECT password_hash FROM users WHERE role = 'owner' ORDER BY id LIMIT 1;")?.ToString();

            return PasswordHasher.Verify("area11", hash);
        }
        catch (Exception exception)
        {
            _log.Warn($"The owner password could not be checked: {exception.Message}");
            return false;
        }
    }
}
