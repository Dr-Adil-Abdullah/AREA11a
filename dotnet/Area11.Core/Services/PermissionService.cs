using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>The module keys used by the permission checkboxes in Settings → Staff (D-03).</summary>
public static class Modules
{
    public const string Pos = "pos";
    public const string Sales = "sales";
    public const string Returns = "returns";
    public const string Customers = "customers";
    public const string Products = "products";
    public const string Purchases = "purchases";
    public const string Suppliers = "suppliers";
    public const string Stock = "stock";
    public const string StockTake = "stocktake";
    public const string Reorder = "reorder";
    public const string Cash = "cash";
    public const string Reports = "reports";
    public const string Profit = "profit";
    public const string PurchaseCost = "purchasecost";
    public const string Audit = "audit";
    public const string Import = "import";
    public const string Backup = "backup";
    public const string Settings = "settings";
    public const string Staff = "staff";
    public const string Sync = "sync";
    public const string CloseApp = "closeapp";

    public static readonly string[] All =
    {
        Pos, Sales, Returns, Customers, Products, Purchases, Suppliers, Stock, StockTake, Reorder,
        Cash, Reports, Profit, PurchaseCost, Audit, Import, Backup, Settings, Staff, Sync, CloseApp
    };

    public static string Label(string module) => module switch
    {
        Pos => "Counter (POS)",
        Sales => "Sales history",
        Returns => "Returns",
        Customers => "Customers",
        Products => "Products",
        Purchases => "Purchases",
        Suppliers => "Suppliers",
        Stock => "Stock write-off",
        StockTake => "Stock-take",
        Reorder => "Reorder pad",
        Cash => "Cash / shifts",
        Reports => "Reports",
        Profit => "View Profit",
        PurchaseCost => "View Purchase Cost",
        Audit => "Audit black box",
        Import => "Allow Excel/CSV Bulk Export & Import",
        Backup => "Backup / restore",
        Settings => "Settings",
        Staff => "Staff & permissions",
        Sync => "Cloud sync",
        CloseApp => "Close the program",
        _ => module
    };
}

/// <summary>
/// Per-person permissions (owner's order, 19 Oct 2026).
/// A stored row always wins; with no row the role default applies. The owner sees everything
/// and can never lock himself out.
/// </summary>
public sealed class PermissionService
{
    private readonly Database _db;
    private readonly LogService _log;
    private readonly AuditService _audit;

    public PermissionService(Database db, LogService log, AuditService audit)
    {
        _db = db;
        _log = log;
        _audit = audit;
    }

    public static bool RoleDefault(UserRole role, string module) => role switch
    {
        UserRole.Owner => true,
        // Cloud sync moves the whole shop out of the building, so a manager does not get it by
        // default - the owner ticks it for one person on purpose (F-48, D-66).
        UserRole.Manager => module is not (Modules.Profit or Modules.PurchaseCost or Modules.Audit or Modules.Backup
            or Modules.Staff or Modules.Sync or Modules.CloseApp or Modules.Import),
        UserRole.Salesman => module is Modules.Pos or Modules.Sales or Modules.Returns
            or Modules.Customers or Modules.Products,
        _ => module is Modules.Pos or Modules.Returns or Modules.Customers
    };

    public bool Can(User? user, string module)
    {
        if (user is null)
        {
            return false;
        }

        try { _db.Sessions.Require(user, closeOnlyAllowed: module == Modules.CloseApp); }
        catch (Exception) { return false; }

        // Visibility is independently revocable even for the owner. Other owner module access
        // remains protected, so hiding financial numbers cannot lock the owner out of Settings.
        if (IsFinancialVisibility(module)) return CanSeeFinancial(user, module);

        if (user.IsOwner)
        {
            return true;
        }

        try
        {
            return _db.Read(connection =>
            {
                var row = _db.QuerySingle<int?>(connection,
                    "SELECT allowed FROM user_permissions WHERE user_id = @userId AND module_key = @module;",
                    new { userId = user.Id, module });

                return row is null ? RoleDefault(user.Kind, module) : row.Value != 0;
            });
        }
        catch (Exception exception)
        {
            _log.Error($"Permission check failed for user {user.Id}/{module}.", exception);
            return false; // a failed grant read is unknown, never an invented role-default allow
        }
    }

    public static bool IsFinancialVisibility(string module) => module is Modules.Profit or Modules.PurchaseCost;

    private bool CanSeeFinancial(User actor, string module)
    {
        if (!actor.Active || actor.Id <= 0) return false;
        try
        {
            return _db.Read(connection =>
            {
                var current = _db.QuerySingle<User>(connection,
                    "SELECT id, name, role, active FROM users WHERE id = @id;", new { id = actor.Id });
                if (current is not { Active: true } || current.Kind != actor.Kind) return false;
                var grant = _db.QuerySingle<int?>(connection,
                    "SELECT allowed FROM user_permissions WHERE user_id = @userId AND module_key = @module;",
                    new { userId = current.Id, module });
                return grant is null ? RoleDefault(current.Kind, module) : grant.Value != 0;
            });
        }
        catch (Exception exception)
        {
            _log.Error($"Financial visibility check failed for {actor.Id}/{module}.", exception);
            return false; // never fall back to role visibility on a failed read
        }
    }

    public IReadOnlyDictionary<string, bool> MapFor(User user)
    {
        if (_db.Sessions.Enabled) return Modules.All.ToDictionary(module => module, _ => false);
        return ReadMap(user);
    }

    public IReadOnlyDictionary<string, bool> MapFor(User actor, User target)
    {
        var result = LoadEditForm(actor, target.Id);
        return result.Ok && result.Value is not null ? result.Value.Values : Modules.All.ToDictionary(module => module, _ => false);
    }

    public ServiceResult<PermissionEditForm> LoadEditForm(User actor, int userId)
    {
        try
        {
            return _db.Snapshot(connection =>
            {
                OwnerAccess.Require(_db, connection, actor);
                var target = _db.QuerySingle<User>(connection,
                    "SELECT id, name, role, active FROM users WHERE id = @id;", new { id = userId });
                if (target is null) throw new ValidationException("That person no longer exists.");
                return ServiceResult<PermissionEditForm>.Success(CaptureEdit(connection, target));
            });
        }
        catch (Exception exception)
        {
            _log.Error("Staff permission editor could not be loaded.", exception);
            return ServiceResult<PermissionEditForm>.Fail(exception is ValidationException ? exception.Message
                : "Permissions could not be read. Reload the selected person before editing.");
        }
    }

    private IReadOnlyDictionary<string, bool> ReadMap(User user)
    {
        try
        {
            return _db.Snapshot(connection =>
            {
                var target = _db.QuerySingle<User>(connection,
                    "SELECT id, name, role, active FROM users WHERE id = @id;", new { id = user.Id });
                if (target is null) throw new ValidationException("That person no longer exists.");
                return CaptureEdit(connection, target).Values;
            });
        }
        catch (Exception exception)
        {
            _log.Error($"Permissions for user {user.Id} could not be read.", exception);
            return Modules.All.ToDictionary(module => module, _ => false);
        }
    }

    private PermissionEditForm CaptureEdit(SqliteConnection connection, User target)
    {
        var stored = _db.Query<PermissionRow>(connection,
            "SELECT module_key, allowed FROM user_permissions WHERE user_id = @userId;", new { userId = target.Id })
            .ToDictionary(row => row.ModuleKey, row => row.Allowed, StringComparer.Ordinal);
        var map = Modules.All.ToDictionary(module => module, module =>
            (target.IsOwner && !IsFinancialVisibility(module))
                || (stored.TryGetValue(module, out var allowed) ? allowed : RoleDefault(target.Kind, module)), StringComparer.Ordinal);
        return new PermissionEditForm(target.Id, target.Kind, map, EditConcurrency.Capture(_db, connection, "permissions", target.Id));
    }

    // Explicit imperative callers remain separate from a loaded editor. Desktop uses the checked overload.
    public ServiceResult Set(User actor, int userId, string module, bool allowed) =>
        SetCore(actor, userId, module, allowed, null, checkedEdit: false);

    public ServiceResult Set(User actor, int userId, string module, bool allowed, EditVersion? expected) =>
        SetCore(actor, userId, module, allowed, expected, checkedEdit: true);

    private ServiceResult SetCore(User actor, int userId, string module, bool allowed, EditVersion? expected, bool checkedEdit)
    {
        if (!actor.IsOwner)
        {
            return ServiceResult.Fail("Only the owner can change permissions.");
        }

        if (!Modules.All.Contains(module))
        {
            return ServiceResult.Fail($"Unknown module '{module}'.");
        }

        var outcome = new TransactionOutcome();
        try
        {
            return _db.Tx(connection =>
            {
                var owner = OwnerAccess.Require(_db, connection, actor);
                var target = _db.QuerySingle<User>(connection,
                    "SELECT id, name, role, active FROM users WHERE id = @id;", new { id = userId });

                if (target is null)
                {
                    throw new ValidationException("That person no longer exists.");
                }

                if (target.IsOwner && !IsFinancialVisibility(module))
                {
                    throw new ValidationException("The owner always has every permission.");
                }

                if (checkedEdit) RequireEditor(connection, userId, expected);
                var old = _db.QuerySingle<int?>(connection,
                    "SELECT allowed FROM user_permissions WHERE user_id = @userId AND module_key = @module;",
                    new { userId, module });

                _db.Execute(connection, @"
INSERT INTO user_permissions(user_id, module_key, allowed, updated_at, updated_by)
VALUES(@userId, @module, @allowed, @at, @actor)
ON CONFLICT(user_id, module_key) DO UPDATE SET
    allowed = excluded.allowed,
    updated_at = excluded.updated_at,
    updated_by = excluded.updated_by;",
                    new
                    {
                        userId,
                        module,
                        allowed = allowed ? 1 : 0,
                        at = Clock.Now(),
                        actor = owner.Name
                    });

                _audit.Record(connection, owner, "Users", "permission_change", "user_permissions",
                    $"{userId}:{module}",
                    oldValue: old?.ToString(),
                    newValue: allowed ? "1" : "0");
                var form = CaptureEdit(connection, target);
                return new ServiceResult { Ok = true, EditVersion = form.EditVersion, PermissionEdit = form };
            }, outcome);
        }
        catch (ValidationException exception)
        {
            return new ServiceResult { Error = outcome.NoCommitConfirmed ? exception.Message
                : "Permission saving is unconfirmed. Reload and verify before retrying.", NoCommitConfirmed = outcome.NoCommitConfirmed };
        }
        catch (Exception exception)
        {
            _log.Error($"Permission {module} for user {userId} could not be saved.", exception);
            return new ServiceResult { Error = outcome.NoCommitConfirmed ? "The permission was not saved; no commit was made."
                : "Permission saving is unconfirmed. Reload and verify before retrying.", NoCommitConfirmed = outcome.NoCommitConfirmed };
        }
    }

    public ServiceResult ResetToRoleDefaults(User actor, int userId) =>
        ResetCore(actor, userId, null, checkedEdit: false);

    public ServiceResult ResetToRoleDefaults(User actor, int userId, EditVersion? expected) =>
        ResetCore(actor, userId, expected, checkedEdit: true);

    private ServiceResult ResetCore(User actor, int userId, EditVersion? expected, bool checkedEdit)
    {
        if (!actor.IsOwner)
        {
            return ServiceResult.Fail("Only the owner can change permissions.");
        }

        var outcome = new TransactionOutcome();
        try
        {
            return _db.Tx(connection =>
            {
                var owner = OwnerAccess.Require(_db, connection, actor);
                var target = _db.QuerySingle<User>(connection,
                    "SELECT id, name, role, active FROM users WHERE id = @id;", new { id = userId });
                if (target is null) throw new ValidationException("That person no longer exists.");
                if (target.IsOwner) throw new ValidationException("The owner always has every permission.");
                if (checkedEdit) RequireEditor(connection, userId, expected);
                _db.Execute(connection, "DELETE FROM user_permissions WHERE user_id = @userId;", new { userId });
                _audit.Record(connection, owner, "Users", "permission_reset", "user_permissions",
                    userId.ToString(), oldValue: "custom", newValue: "role defaults");
                var form = CaptureEdit(connection, target);
                return new ServiceResult { Ok = true, EditVersion = form.EditVersion, PermissionEdit = form };
            }, outcome);
        }
        catch (ValidationException exception)
        {
            return new ServiceResult { Error = outcome.NoCommitConfirmed ? exception.Message
                : "Permission saving is unconfirmed. Reload and verify before retrying.", NoCommitConfirmed = outcome.NoCommitConfirmed };
        }
        catch (Exception exception)
        {
            _log.Error($"Permissions for user {userId} could not be reset.", exception);
            return new ServiceResult { Error = outcome.NoCommitConfirmed ? "The permissions were not reset; no commit was made."
                : "Permission reset is unconfirmed. Reload and verify before retrying.", NoCommitConfirmed = outcome.NoCommitConfirmed };
        }
    }

    private void RequireEditor(SqliteConnection connection, int userId, EditVersion? expected)
    {
        // Explicit checked overloads fail closed even in sessions-disabled hosts.
        if (expected is null) throw new ValidationException("The permission editor changed or was not loaded. Reload and review before saving.");
        EditConcurrency.Require(_db, connection, "permissions", userId, expected);
    }

    private sealed class PermissionRow
    {
        public string ModuleKey { get; set; } = string.Empty;

        public bool Allowed { get; set; }
    }
}

/// <summary>A business rule the user can read (shown as the service result message).</summary>
public sealed class ValidationException : Exception
{
    public ValidationException(string message) : base(message)
    {
    }
}
