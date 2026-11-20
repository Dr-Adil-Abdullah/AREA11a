using Area11.Core.Data;
using Area11.Core.Models;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>
/// Current local reader for bounded reports/history/reorder/dashboard snapshots.
/// Desktop-enabled proof is required before current resolution;legacy disabled hosts are not authenticated.
/// </summary>
internal static class SnapshotReadAccess
{
    public static User Require(Database db, SqliteConnection connection, PermissionService permissions,
        User? actor, string module, string? alternateModule = null)
    {
        db.Sessions.Require(actor);
        if (actor is not { Active: true } || actor.Id <= 0)
            throw new ValidationException("Sign in with an active account to read this information.");
        var current = db.QuerySingle<User>(connection,
            "SELECT id, name, role, active FROM users WHERE id = @id;", new { id = actor.Id });
        if (current is not { Active: true } || current.Kind != actor.Kind)
            throw new ValidationException("The account has changed. Sign in again before reading this information.");
        db.Sessions.Carry(actor, current);
        if (!permissions.Can(current, module)
            && (alternateModule is null || !permissions.Can(current, alternateModule)))
            throw new ValidationException("You do not have permission to read this information.");
        return current;
    }
}
