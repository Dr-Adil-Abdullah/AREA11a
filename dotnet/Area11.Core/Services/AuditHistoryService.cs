using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>
/// The reading side of the black box (Spec 22, F-35). `AuditService` writes; nothing here can write,
/// edit or delete a line - the viewer is a window, not a door.
///
/// Why a separate service: the writer runs inside every business transaction in the shop, and it must
/// stay small and impossible to break. Searching, counting and building a CSV are a different job and
/// a different risk, so they live here.
///
/// Who may look: the `audit` permission. By default that is the **owner only** (Spec 12.1.2 - a
/// manager must not read the black box), and the owner can grant it to one person in Settings.
/// </summary>
public sealed class AuditHistoryService
{
    private readonly Database _db;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    public AuditHistoryService(Database db, PermissionService permissions, LogService log)
    {
        _db = db;
        _permissions = permissions;
        _log = log;
    }

    /// <summary>Page sizes the viewer offers. "All" is not offered: the black box grows for ever.</summary>
    public static IReadOnlyList<int> PageSizes { get; } = new[] { 200, 500, 2000, 10000 };

    /// <summary>Reads one page of the black box, newest line first.</summary>
    public ServiceResult<AuditPage> Read(AuditFilter? filter, User? user)
    {
        if (!_permissions.Can(user, Modules.Audit))
        {
            return ServiceResult<AuditPage>.Fail("The black box is for the owner (Spec 12.1.2).");
        }

        var wanted = filter?.Copy() ?? new AuditFilter();
        wanted.Search = (wanted.Search ?? string.Empty).Trim();
        wanted.Module = (wanted.Module ?? string.Empty).Trim();
        wanted.Action = (wanted.Action ?? string.Empty).Trim();
        wanted.Limit = wanted.Limit <= 0 ? 500 : Math.Min(wanted.Limit, 10000);

        try
        {
            using var connection = _db.Open();

            var window = new
            {
                search = wanted.Search,
                pattern = "%" + wanted.Search + "%",
                module = wanted.Module,
                action = wanted.Action,
                from = wanted.FromStamp,
                until = wanted.UntilStamp,
                limit = wanted.Limit
            };

            var rows = _db.Query<AuditRow>(connection, @"
SELECT id AS id, IFNULL(at, '') AS at, IFNULL(user_name, '') AS userName,
       IFNULL(module, '') AS module, action AS action,
       IFNULL(entity, '') AS entity, IFNULL(entity_id, '') AS entityId,
       IFNULL(old_value, '') AS oldValue, IFNULL(new_value, '') AS newValue,
       IFNULL(details, '') AS details
FROM audit_logs
WHERE (@search = ''
       OR action LIKE @pattern
       OR IFNULL(details, '') LIKE @pattern
       OR IFNULL(user_name, '') LIKE @pattern
       OR IFNULL(entity, '') LIKE @pattern
       OR IFNULL(old_value, '') LIKE @pattern
       OR IFNULL(new_value, '') LIKE @pattern)
  AND (@module = '' OR IFNULL(module, '') = @module)
  AND (@action = '' OR action = @action)
  AND (@from = '' OR at >= @from)
  AND (@until = '' OR at < @until)
ORDER BY id DESC
LIMIT @limit;", window);

            var total = Convert.ToInt32(_db.Scalar(connection, @"
SELECT COUNT(1)
FROM audit_logs
WHERE (@search = ''
       OR action LIKE @pattern
       OR IFNULL(details, '') LIKE @pattern
       OR IFNULL(user_name, '') LIKE @pattern
       OR IFNULL(entity, '') LIKE @pattern
       OR IFNULL(old_value, '') LIKE @pattern
       OR IFNULL(new_value, '') LIKE @pattern)
  AND (@module = '' OR IFNULL(module, '') = @module)
  AND (@action = '' OR action = @action)
  AND (@from = '' OR at >= @from)
  AND (@until = '' OR at < @until);", window) ?? 0);

            return ServiceResult<AuditPage>.Success(new AuditPage
            {
                Rows = rows,
                Total = total,
                Filter = wanted,
                Modules = Distinct(connection, "module"),
                Actions = Distinct(connection, "action"),
                Users = Distinct(connection, "user_name")
            });
        }
        catch (Exception exception)
        {
            _log.Error("The black box could not be read.", exception);
            return ServiceResult<AuditPage>.Fail(
                "The black box could not be read. (" + exception.GetType().Name + ": " + exception.Message + ")");
        }
    }

    /// <summary>
    /// The names already in the table, for the two drop-downs. Only what has really been used: an
    /// empty category in a filter is a worse help than no category at all.
    /// </summary>
    private List<string> Distinct(Microsoft.Data.Sqlite.SqliteConnection connection, string column)
    {
        // Three literal statements rather than one built from a column name: a column name pasted
        // into SQL is exactly the habit that turns a report into an injection.
        var sql = column switch
        {
            "module" => "SELECT DISTINCT IFNULL(module, '') AS value FROM audit_logs WHERE IFNULL(module, '') <> '' ORDER BY value;",
            "action" => "SELECT DISTINCT action AS value FROM audit_logs WHERE action <> '' ORDER BY value;",
            _ => "SELECT DISTINCT IFNULL(user_name, '') AS value FROM audit_logs WHERE IFNULL(user_name, '') <> '' ORDER BY value;"
        };

        return _db.Query<string>(connection, sql);
    }
}
