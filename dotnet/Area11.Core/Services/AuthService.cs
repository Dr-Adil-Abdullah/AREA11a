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
    /// <summary>
    /// The factory recovery word (Phase 2, owner's word): "area11", the same word as the
    /// first-start owner password. While the owner never sets their own word, this word signs the
    /// owner in through the "forgot password" door. Spaces and case are forgiven ("Area 11" works).
    /// </summary>
    public const string FactoryRecoveryWord = "area11";

    public const string RecoveryWordKey = "security.recoveryWordHash";

    private const string RecoveryThrottleKey = "(recovery)";

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

    public ServiceResult<User> Login(string? name, string secret) =>
        VerifyCore(name, secret, touchLogin: true, failedAction: "login_failed", okAction: "login",
            systemError: "Sign-in is not possible right now. Please try again.");

    /// <summary>
    /// Portion 16: proves a name + password/PIN without signing in, for the protected-close prompt.
    /// The same throttle, hashes and sentences as <see cref="Login"/> - guessing at the × is slowed
    /// exactly like guessing at sign-in - but it touches no last_login and audits as an unlock, so
    /// proving who may close never looks like a sign-in in the black box.
    /// </summary>
    public ServiceResult<User> VerifySecret(string? name, string secret) =>
        VerifyCore(name, secret, touchLogin: false, failedAction: "unlock_failed", okAction: "unlock",
            systemError: "The password could not be checked right now. Please try again.");

    private ServiceResult<User> VerifyCore(
        string? name, string secret, bool touchLogin, string failedAction, string okAction, string systemError)
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
                _audit.Record(connection, null, "Auth", failedAction, "users", null,
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
                _audit.Record(connection, user, "Auth", failedAction, "users", user.Id.ToString(),
                    details: "wrong password/PIN");
                return ServiceResult<User>.Fail("The password or PIN is not correct.");
            }

            ClearFailures(throttleKey);

            if (touchLogin)
            {
                _db.Execute(connection, "UPDATE users SET last_login_at = @at WHERE id = @id;",
                    new { at = Clock.Now(), id = user.Id });
            }

            _audit.Record(connection, user, "Auth", okAction, "users", user.Id.ToString());

            return ServiceResult<User>.Success(user);
        }
        catch (Exception exception)
        {
            _log.Error("Login failed because of a system error.", exception);
            return ServiceResult<User>.Fail(systemError);
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
                var owner = OwnerAccess.Require(_db, connection, actor);
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

                _audit.Record(connection, owner, "Users", "create", "users", newId.ToString(),
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
                var owner = OwnerAccess.Require(_db, connection, actor);
                var target = _db.QuerySingle<User>(connection,
                    "SELECT id, name, role, active FROM users WHERE id = @id;", new { id = userId });
                if (target is null) throw new ValidationException("That person no longer exists.");
                var column = isPassword ? "password_hash" : "pin_hash";
                var otherColumn = isPassword ? "pin_hash" : "password_hash";

                _db.Execute(connection, $"UPDATE users SET {column} = @hash, {otherColumn} = NULL WHERE id = @id;",
                    new { hash = PasswordHasher.Hash(newSecret.Trim()), id = userId });

                _audit.Record(connection, owner, "Users", "secret_change", "users", userId.ToString(),
                    oldValue: "{\"secret\":\"set\"}", newValue: "{\"secret\":\"changed\"}");
            });

            return ServiceResult.Success();
        }
        catch (ValidationException exception)
        {
            return ServiceResult.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error($"The secret of user {userId} could not be changed.", exception);
            return ServiceResult.Fail("The change could not be saved.");
        }
    }

    /// <summary>
    /// The "forgot password" door (Phase 2, owner's word): the recovery word signs the owner in,
    /// with the same throttle as the sign-in so guessing is slowed exactly the same way. The black
    /// box records the recovery - and whether the factory word or the owner's own word opened it.
    /// </summary>
    public ServiceResult<User> RecoverOwner(string? word)
    {
        var clean = NormalizeWord(word);

        if (clean.Length == 0)
        {
            return ServiceResult<User>.Fail("Enter the recovery word.");
        }

        if (IsLocked(RecoveryThrottleKey, out var remaining))
        {
            return ServiceResult<User>.Fail(
                $"Too many attempts. Try again in {Math.Ceiling(remaining.TotalSeconds)} seconds.");
        }

        try
        {
            using var connection = _db.Open();

            var user = _db.QuerySingle<User>(connection,
                "SELECT id, name, role, active FROM users WHERE role = 'owner' AND active = 1 ORDER BY id LIMIT 1;");

            if (user is null)
            {
                RegisterFailure(RecoveryThrottleKey);
                _audit.Record(connection, null, "Auth", "owner_recover_failed", "users", null,
                    details: "no owner account");
                return ServiceResult<User>.Fail("No owner account exists.");
            }

            var stored = _settings.GetString(RecoveryWordKey);
            var factory = stored.Trim().Length == 0;
            var valid = factory
                ? clean == FactoryRecoveryWord
                : PasswordHasher.Verify(clean, stored);

            if (!valid)
            {
                RegisterFailure(RecoveryThrottleKey);
                _audit.Record(connection, user, "Auth", "owner_recover_failed", "users", user.Id.ToString(),
                    details: "wrong recovery word");
                return ServiceResult<User>.Fail("The recovery word is not correct.");
            }

            ClearFailures(RecoveryThrottleKey);

            _db.Execute(connection, "UPDATE users SET last_login_at = @at WHERE id = @id;",
                new { at = Clock.Now(), id = user.Id });

            _audit.Record(connection, user, "Auth", "owner_recovered", "users", user.Id.ToString(),
                details: factory ? "factory word" : "own word");

            return ServiceResult<User>.Success(user);
        }
        catch (Exception exception)
        {
            _log.Error("Owner recovery failed because of a system error.", exception);
            return ServiceResult<User>.Fail("Sign-in is not possible right now. Please try again.");
        }
    }

    /// <summary>
    /// The owner sets their own recovery word - or an empty word, which hands the door back to the
    /// factory word so the escape hatch can never be left without one. Only the hash is stored, and
    /// the black box records that it changed, never the word or its hash.
    /// </summary>
    public ServiceResult SetRecoveryWord(User actor, string? word)
    {
        if (!actor.IsOwner)
        {
            return ServiceResult.Fail("Only the owner can change the recovery word.");
        }

        var clean = NormalizeWord(word);

        if (clean.Length > 0 && (clean.Length < 4 || clean.Length > 40))
        {
            return ServiceResult.Fail("The recovery word must be 4 to 40 letters.");
        }

        try
        {
            var stored = clean.Length == 0 ? string.Empty : PasswordHasher.Hash(clean);

            // Save the hash quietly, but verify the live owner and record a non-secret event
            // in the SAME transaction. The generic settings paths refuse this key.
            return _settings.SaveRecoveryHash(actor, stored);
        }
        catch (Exception exception)
        {
            _log.Error("The recovery word could not be changed.", exception);
            return ServiceResult.Fail("The change could not be saved.");
        }
    }

    /// <summary>" Area 11 " and "area11" open the same door: trimmed, lowercased, spaceless.</summary>
    public static string NormalizeWord(string? word) =>
        (word ?? string.Empty).Trim().ToLowerInvariant().Replace(" ", string.Empty);

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
                var owner = OwnerAccess.Require(_db, connection, actor);
                var target = _db.QuerySingle<User>(connection,
                    "SELECT id, name, role, active FROM users WHERE id = @id;", new { id = userId });
                if (target is null) throw new ValidationException("That person no longer exists.");
                if (target.IsOwner && !active)
                    throw new ValidationException("The owner account cannot be switched off.");
                _db.Execute(connection, "UPDATE users SET active = @active WHERE id = @id;",
                    new { active = active ? 1 : 0, id = userId });

                _audit.Record(connection, owner, "Users", active ? "activate" : "deactivate", "users",
                    userId.ToString());
            });

            return ServiceResult.Success();
        }
        catch (ValidationException exception)
        {
            return ServiceResult.Fail(exception.Message);
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
