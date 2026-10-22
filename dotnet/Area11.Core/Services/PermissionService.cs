using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;

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
    public const string Audit = "audit";
    public const string Import = "import";
    public const string Backup = "backup";
    public const string Settings = "settings";
    public const string Staff = "staff";
    public const string Sync = "sync";

    public static readonly string[] All =
    {
        Pos, Sales, Returns, Customers, Products, Purchases, Suppliers, Stock, StockTake, Reorder,
        Cash, Reports, Profit, Audit, Import, Backup, Settings, Staff, Sync
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
        Profit => "Net profit",
        Audit => "Audit black box",
        Import => "Excel import",
        Backup => "Backup / restore",
        Settings => "Settings",
        Staff => "Staff & permissions",
        Sync => "Cloud sync",
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
        UserRole.Manager => module is not (Modules.Profit or Modules.Audit or Modules.Backup
            or Modules.Staff or Modules.Sync),
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

        if (user.IsOwner)
        {
            return true;
        }

        try
        {
            using var connection = _db.Open();
            var row = _db.QuerySingle<int?>(connection,
                "SELECT allowed FROM user_permissions WHERE user_id = @userId AND module_key = @module;",
                new { userId = user.Id, module });

            return row is null ? RoleDefault(user.Kind, module) : row.Value != 0;
        }
        catch (Exception exception)
        {
            _log.Error($"Permission check failed for user {user.Id}/{module}.", exception);
            return RoleDefault(user.Kind, module);
        }
    }

    public IReadOnlyDictionary<string, bool> MapFor(User user)
    {
        var map = new Dictionary<string, bool>(StringComparer.Ordinal);
        var stored = new Dictionary<string, bool>(StringComparer.Ordinal);

        try
        {
            using var connection = _db.Open();
            var rows = _db.Query<PermissionRow>(connection,
                "SELECT module_key, allowed FROM user_permissions WHERE user_id = @userId;",
                new { userId = user.Id });

            foreach (var row in rows)
            {
                stored[row.ModuleKey] = row.Allowed;
            }
        }
        catch (Exception exception)
        {
            _log.Error($"Permissions for user {user.Id} could not be read.", exception);
        }

        foreach (var module in Modules.All)
        {
            map[module] = user.IsOwner
                || (stored.TryGetValue(module, out var value) ? value : RoleDefault(user.Kind, module));
        }

        return map;
    }

    public ServiceResult Set(User actor, int userId, string module, bool allowed)
    {
        if (!actor.IsOwner)
        {
            return ServiceResult.Fail("Only the owner can change permissions.");
        }

        if (!Modules.All.Contains(module))
        {
            return ServiceResult.Fail($"Unknown module '{module}'.");
        }

        try
        {
            _db.Tx(connection =>
            {
                var target = _db.QuerySingle<User>(connection,
                    "SELECT id, name, role, active FROM users WHERE id = @id;", new { id = userId });

                if (target is null)
                {
                    throw new ValidationException("That person no longer exists.");
                }

                if (target.IsOwner)
                {
                    throw new ValidationException("The owner always has every permission.");
                }

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
                        actor = actor.Name
                    });

                _audit.Record(connection, actor, "Users", "permission_change", "user_permissions",
                    $"{userId}:{module}",
                    oldValue: old?.ToString(),
                    newValue: allowed ? "1" : "0");
            });

            return ServiceResult.Success();
        }
        catch (ValidationException exception)
        {
            return ServiceResult.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error($"Permission {module} for user {userId} could not be saved.", exception);
            return ServiceResult.Fail("The permission could not be saved.");
        }
    }

    public ServiceResult ResetToRoleDefaults(User actor, int userId)
    {
        if (!actor.IsOwner)
        {
            return ServiceResult.Fail("Only the owner can change permissions.");
        }

        try
        {
            _db.Tx(connection =>
            {
                _db.Execute(connection, "DELETE FROM user_permissions WHERE user_id = @userId;", new { userId });
                _audit.Record(connection, actor, "Users", "permission_reset", "user_permissions",
                    userId.ToString(), oldValue: "custom", newValue: "role defaults");
            });

            return ServiceResult.Success();
        }
        catch (Exception exception)
        {
            _log.Error($"Permissions for user {userId} could not be reset.", exception);
            return ServiceResult.Fail("The permissions could not be reset.");
        }
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
