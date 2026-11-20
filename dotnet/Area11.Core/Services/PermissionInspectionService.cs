using System.Text.Json;
using Dapper;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>Read-only owner inspector. No secrets, role writes, grant writes or financial posting.</summary>
public sealed class PermissionInspectionService
{
    private readonly Database _db;
    private readonly LogService _log;

    public PermissionInspectionService(Database db, LogService log)
    {
        _db = db;
        _log = log;
    }

    public ServiceResult<PermissionInspection> Inspect(User? actor, string module)
    {
        try
        {
            if (actor is not { Active: true, IsOwner: true } || actor.Id <= 0)
                throw new ValidationException("Only the active owner can inspect staff assignments.");
            if (!Modules.All.Contains(module))
                throw new ValidationException("Choose an existing permission.");

            _db.Sessions.Require(actor);
            using var connection = _db.Open();
            // Local read transaction: one coherent DB snapshot without Database.Tx's write lock
            // or its shared transaction field. All queries explicitly use this transaction.
            using var transaction = connection.BeginTransaction(deferred: true);
            var owner = connection.QueryFirstOrDefault<User>(
                "SELECT id, name, role, active FROM users WHERE id = @id;",
                new { id = actor.Id }, transaction);
            if (owner is not { Active: true, IsOwner: true })
                throw new ValidationException("Only the active owner can inspect staff assignments. Sign in again.");
            var json = connection.QueryFirstOrDefault<string>(
                "SELECT value FROM settings WHERE key = @key;",
                new { key = MasterFoundationSettings.ProfitShowMargins }, transaction);
            var mainOn = json is null || JsonSerializer.Deserialize<bool>(json);
            var rows = connection.Query<AssignmentRow>(@"
SELECT u.id AS userId, u.name AS name, u.role AS role, u.active AS active,
       p.allowed AS savedGrant
FROM users u
LEFT JOIN user_permissions p ON p.user_id = u.id AND p.module_key = @module
ORDER BY u.name COLLATE NOCASE, u.id;", new { module }, transaction).ToList();

            var people = rows.Select(row =>
            {
                var role = UserRoles.Parse(row.Role);
                var automaticOwner = role == UserRole.Owner && !PermissionService.IsFinancialVisibility(module);
                var assigned = automaticOwner || (row.SavedGrant is int saved
                    ? saved != 0 : PermissionService.RoleDefault(role, module));
                return new PermissionAssignment
                {
                    UserId = row.UserId, Name = row.Name, Role = row.Role, Active = row.Active,
                    Assigned = assigned,
                    Source = automaticOwner ? "Owner automatic" : row.SavedGrant is null ? "Role default" : "Explicit override",
                    Access = !row.Active ? "Blocked: inactive account" : !assigned ? "Disabled by grant/default"
                        : PermissionService.IsFinancialVisibility(module) && !mainOn ? "Blocked: main switch OFF"
                        : PermissionService.IsFinancialVisibility(module) ? "Eligible on separately granted pages"
                        : "Module allowed; action-specific checks still apply"
                };
            }).ToList();
            transaction.Commit();
            return ServiceResult<PermissionInspection>.Success(new PermissionInspection
            {
                Definition = PermissionDocumentation.For(module), MainSwitchOn = mainOn, People = people
            });
        }
        catch (ValidationException exception)
        {
            return ServiceResult<PermissionInspection>.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error("Permission assignment inspection failed.", exception);
            return ServiceResult<PermissionInspection>.Fail("Staff assignments could not be read. No cached list is shown; refresh after checking the connection.");
        }
    }

    private sealed class AssignmentRow
    {
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool Active { get; set; }
        public int? SavedGrant { get; set; }
    }
}
