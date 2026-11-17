using Area11.Core.Data;
using Area11.Core.Models;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>Owner-only writes must verify the current account inside their write transaction.
/// This does not authenticate arbitrary callers or replace the session; it rejects stale/forged roles.
/// </summary>
internal static class OwnerAccess
{
    public static User Require(Database db, SqliteConnection connection, User? actor)
    {
        if (actor?.IsOwner != true)
            throw new ValidationException("Only the active owner can make this change.");
        var current = db.QuerySingle<User>(connection,
            "SELECT id, name, role, active FROM users WHERE id = @id;", new { id = actor.Id });
        if (current is not { IsOwner: true, Active: true })
            throw new ValidationException("Only the active owner can make this change. Sign in again.");
        return current;
    }
}
