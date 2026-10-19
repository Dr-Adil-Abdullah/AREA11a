using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>
/// Login with a password (owner) or a PIN (staff), Spec 7.2.
/// • the secret is never logged — only "wrong secret" or "changed: yes/no";
/// • five failures lock that name for two minutes (in memory, like the reference);
/// • every attempt (success and failure) reaches the black box.
/// </summary>
public sealed class AuthService
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(2);
    private static readonly Dictionary<string, AttemptState> Attempts = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object AttemptGate = new();

    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly SettingsService _settings;
    private readonly LogService _log;

    public AuthService(Database db, AuditService audit, SettingsService settings, LogService log)
    {
        _db = db;
        _audit = audit;
        _settings = settings;
        _log = log;
    }

    public ServiceResult<User> Login(string? name, string secret)
    {
        var cleanName = (name ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(secret))
        {
            return ServiceResult<User>.Fail("Enter the password or PIN.");
        }

        var throttleKey = cleanName.Length == 0 ? "(owner)" : cleanName;

        if (IsLocked(throttleKey, out var remaining))
        {
            return ServiceResult<User>.Fail(
                $"Too many attempts. Try again in {Math.Ceiling(remaining.TotalSeconds)} seconds.");
        }

        try
        {
            using var connection = _db.Open();

            var user = cleanName.Length == 0
                ? _db.QuerySingle<User>(connection,
                    "SELECT id, name, role, active FROM users WHERE role = 'owner' AND active = 1 ORDER BY id LIMIT 1;")
                : _db.QuerySingle<User>(connection,
                    "SELECT id, name, role, active FROM users WHERE lower(name) = lower(@name) AND active = 1 LIMIT 1;",
                    new { name = cleanName });

            if (user is null)
            {
                RegisterFailure(throttleKey);
                _audit.Record(connection, null, "Auth", "login_failed", "users", null,
                    details: cleanName.Length == 0 ? "no owner account" : $"unknown user '{cleanName}'");
                return ServiceResult<User>.Fail("No such user, or the account is switched off.");
            }

            var secrets = _db.QuerySingle<SecretRow>(connection,
                "SELECT password_hash, pin_hash FROM users WHERE id = @id;",
                new { id = user.Id });

            var valid = PasswordHasher.Verify(secret, secrets?.PasswordHash)
                        || PasswordHasher.Verify(secret, secrets?.PinHash);

            if (!valid)
            {
                RegisterFailure(throttleKey);
                _audit.Record(connection, user, "Auth", "login_failed", "users", user.Id.ToString(),
                    details: "wrong password/PIN");
                return ServiceResult<User>.Fail("The password or PIN is not correct.");
            }

            ClearFailures(throttleKey);

            _db.Execute(connection, "UPDATE users SET last_login_at = @at WHERE id = @id;",
                new { at = Clock.Now(), id = user.Id });

            _audit.Record(connection, user, "Auth", "login", "users", user.Id.ToString());

            return ServiceResult<User>.Success(user);
        }
        catch (Exception exception)
        {
            _log.Error("Login failed because of a system error.", exception);
            return ServiceResult<User>.Fail("Sign-in is not possible right now. Please try again.");
        }
    }

    public void Logout(User? user)
    {
        try
        {
            using var connection = _db.Open();
            _audit.Record(connection, user, "Auth", "logout", "users", user?.Id.ToString());
        }
        catch (SqliteException exception)
        {
            _log.Warn($"Logout could not be audited: {exception.Message}");
        }
    }

    public ServiceResult<List<User>> ListUsers()
    {
        try
        {
            using var connection = _db.Open();
            var users = _db.Query<User>(connection,
                "SELECT id, name, role, active FROM users ORDER BY CASE role WHEN 'owner' THEN 0 WHEN 'manager' THEN 1 ELSE 2 END, name COLLATE NOCASE;");
            return ServiceResult<List<User>>.Success(users);
        }
        catch (Exception exception)
        {
            _log.Error("The staff list could not be loaded.", exception);
            return ServiceResult<List<User>>.Fail("The staff list could not be loaded.");
        }
    }

    /// <summary>Owner creates staff (Spec 7.1.2). Staff sign in with a PIN.</summary>
    public ServiceResult<int> CreateStaff(User actor, string name, UserRole role, string pin)
    {
        if (!actor.IsOwner)
        {
            return ServiceResult<int>.Fail("Only the owner can create staff accounts.");
        }

        var cleanName = (name ?? string.Empty).Trim();
        if (cleanName.Length < 2)
        {
            return ServiceResult<int>.Fail("Enter the person's name.");
        }

        var pinLength = _settings.GetInt("security.pinLength", 4);
        if (pin.Trim().Length != pinLength || !pin.Trim().All(char.IsDigit))
        {
            return ServiceResult<int>.Fail($"The PIN must be exactly {pinLength} digits.");
        }

        try
        {
            var id = _db.Tx(connection =>
            {
                var duplicate = Convert.ToInt64(_db.Scalar(connection,
                    "SELECT COUNT(1) FROM users WHERE lower(name) = lower(@name);", new { name = cleanName }) ?? 0L);

                if (duplicate > 0)
                {
                    throw new ValidationException("A person with this name already exists.");
                }

                _db.Execute(connection,
                    "INSERT INTO users(name, role, pin_hash, active, created_at) VALUES(@name, @role, @pin, 1, @at);",
                    new
                    {
                        name = cleanName,
                        role = UserRoles.ToDb(role),
                        pin = PasswordHasher.Hash(pin.Trim()),
                        at = Clock.Now()
                    });

                var newId = (int)_db.NextId(connection);

                _audit.Record(connection, actor, "Users", "create", "users", newId.ToString(),
                    newValue: $"{{\"name\":\"{cleanName}\",\"role\":\"{UserRoles.ToDb(role)}\",\"pin\":\"set\"}}");

                return newId;
            });

            return ServiceResult<int>.Success(id);
        }
        catch (ValidationException exception)
        {
            return ServiceResult<int>.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error("A staff account could not be created.", exception);
            return ServiceResult<int>.Fail("The staff account could not be saved.");
        }
    }

    /// <summary>Owner resets a password or PIN. The black box only records that it changed.</summary>
    public ServiceResult SetSecret(User actor, int userId, string newSecret, bool isPassword)
    {
        if (!actor.IsOwner)
        {
            return ServiceResult.Fail("Only the owner can change passwords and PINs.");
        }

        if (newSecret.Trim().Length < (isPassword ? 4 : 3))
        {
            return ServiceResult.Fail(isPassword ? "The password is too short." : "The PIN is too short.");
        }

        try
        {
            _db.Tx(connection =>
            {
                var column = isPassword ? "password_hash" : "pin_hash";

                _db.Execute(connection, $"UPDATE users SET {column} = @hash WHERE id = @id;",
                    new { hash = PasswordHasher.Hash(newSecret.Trim()), id = userId });

                _audit.Record(connection, actor, "Users", "secret_change", "users", userId.ToString(),
                    oldValue: "{\"secret\":\"set\"}", newValue: "{\"secret\":\"changed\"}");
            });

            return ServiceResult.Success();
        }
        catch (Exception exception)
        {
            _log.Error($"The secret of user {userId} could not be changed.", exception);
            return ServiceResult.Fail("The change could not be saved.");
        }
    }

    public ServiceResult SetActive(User actor, int userId, bool active)
    {
        if (!actor.IsOwner)
        {
            return ServiceResult.Fail("Only the owner can switch an account on or off.");
        }

        try
        {
            _db.Tx(connection =>
            {
                _db.Execute(connection, "UPDATE users SET active = @active WHERE id = @id;",
                    new { active = active ? 1 : 0, id = userId });

                _audit.Record(connection, actor, "Users", active ? "activate" : "deactivate", "users",
                    userId.ToString());
            });

            return ServiceResult.Success();
        }
        catch (Exception exception)
        {
            _log.Error($"User {userId} could not be switched.", exception);
            return ServiceResult.Fail("The change could not be saved.");
        }
    }

    private static void RegisterFailure(string key)
    {
        lock (AttemptGate)
        {
            if (!Attempts.TryGetValue(key, out var state))
            {
                state = new AttemptState();
                Attempts[key] = state;
            }

            state.Count++;

            if (state.Count >= MaxAttempts)
            {
                state.LockedUntil = DateTime.UtcNow + LockDuration;
                state.Count = 0;
            }
        }
    }

    private static bool IsLocked(string key, out TimeSpan remaining)
    {
        lock (AttemptGate)
        {
            remaining = TimeSpan.Zero;

            if (!Attempts.TryGetValue(key, out var state) || state.LockedUntil <= DateTime.UtcNow)
            {
                return false;
            }

            remaining = state.LockedUntil - DateTime.UtcNow;
            return true;
        }
    }

    private static void ClearFailures(string key)
    {
        lock (AttemptGate)
        {
            Attempts.Remove(key);
        }
    }

    private sealed class AttemptState
    {
        public int Count { get; set; }

        public DateTime LockedUntil { get; set; }
    }

    private sealed class SecretRow
    {
        public string? PasswordHash { get; set; }

        public string? PinHash { get; set; }
    }
}
