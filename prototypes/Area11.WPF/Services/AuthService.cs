using Area11.WPF.Data;
using Area11.WPF.Models;
using Area11.WPF.Utils;

namespace Area11.WPF.Services;

/// <summary>
/// Owner signs in with a password, staff with name + PIN (reference decision Q-13).
/// Repeated failures are throttled in memory so the till cannot be brute-forced.
/// </summary>
public sealed class AuthService
{
    private readonly Database _db;
    private readonly LoggerService _log;
    private readonly AuditService _audit;
    private readonly int _maxAttempts;
    private readonly int _lockoutSeconds;

    private int _failedAttempts;
    private DateTime _lockedUntil = DateTime.MinValue;

    public AuthService(Database db, LoggerService log, AuditService audit, int maxAttempts, int lockoutSeconds)
    {
        _db = db;
        _log = log;
        _audit = audit;
        _maxAttempts = Math.Max(1, maxAttempts);
        _lockoutSeconds = Math.Max(1, lockoutSeconds);
    }

    public OperationResult<User> Login(string userName, string secret)
    {
        try
        {
            if (DateTime.Now < _lockedUntil)
            {
                var seconds = Math.Max(1, (int)(_lockedUntil - DateTime.Now).TotalSeconds);
                return OperationResult<User>.Fail($"Too many attempts. Try again in {seconds} second(s).");
            }

            if (string.IsNullOrWhiteSpace(secret))
            {
                return OperationResult<User>.Fail("Password or PIN is required.");
            }

            var requestedName = userName.Trim();
            using var connection = _db.Open();

            var user = string.IsNullOrEmpty(requestedName)
                ? _db.Query(connection,
                        "SELECT id, name, role FROM users WHERE role = 'Owner' AND active = 1 ORDER BY id LIMIT 1;",
                        MapUser)
                    .FirstOrDefault()
                : _db.Query(connection,
                        "SELECT id, name, role FROM users WHERE name = $name COLLATE NOCASE AND active = 1 LIMIT 1;",
                        MapUser,
                        ("$name", requestedName))
                    .FirstOrDefault();

            if (user is null)
            {
                RegisterFailure();
                return OperationResult<User>.Fail("No active user with that name.");
            }

            var hash = _db.Scalar<string>(connection, "SELECT secret_hash FROM users WHERE id = $id;", ("$id", user.Id));
            if (string.IsNullOrEmpty(hash) || !PasswordHasher.Verify(secret, hash))
            {
                RegisterFailure();
                _log.Warn($"Failed login for '{user.Name}'.");
                return OperationResult<User>.Fail("Incorrect password or PIN.");
            }

            _failedAttempts = 0;

            _db.Tx(cn => _audit.Record(cn, user.Name, "Auth", "login", "users", user.Id, null,
                new { role = user.Role.ToString() }));

            _log.Info($"'{user.Name}' signed in as {user.Role}.");
            return OperationResult<User>.Success(user);
        }
        catch (Exception exception)
        {
            _log.Error("Login could not be completed.", exception);
            return OperationResult<User>.Fail("Login could not be completed. Please try again.");
        }
    }

    public OperationResult ChangeSecret(int userId, string newSecret, string changedBy)
    {
        try
        {
            if (newSecret.Trim().Length < 4)
            {
                throw new ValidationException("The password or PIN must be at least 4 characters.");
            }

            _db.Tx(connection =>
            {
                _db.Exec(connection, "UPDATE users SET secret_hash = $hash WHERE id = $id;",
                    ("$hash", PasswordHasher.Hash(newSecret)),
                    ("$id", userId));

                _audit.Record(connection, changedBy, "Users", "secret_change", "users", userId, null,
                    new { changed = true }, "PIN/password is never logged");
            });

            return OperationResult.Success();
        }
        catch (ValidationException exception)
        {
            return OperationResult.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error("Secret change failed.", exception);
            return OperationResult.Fail("The password could not be changed.");
        }
    }

    private void RegisterFailure()
    {
        _failedAttempts++;
        if (_failedAttempts >= _maxAttempts)
        {
            _lockedUntil = DateTime.Now.AddSeconds(_lockoutSeconds);
            _failedAttempts = 0;
        }
    }

    private static User MapUser(Microsoft.Data.Sqlite.SqliteDataReader reader) => new()
    {
        Id = reader.Int("id"),
        Name = reader.Str("name"),
        Role = Enum.TryParse<UserRole>(reader.Str("role"), ignoreCase: true, out var role) ? role : UserRole.Cashier
    };
}
