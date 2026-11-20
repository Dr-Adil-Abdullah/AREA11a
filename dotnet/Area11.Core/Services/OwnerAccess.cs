using Area11.Core.Data;
using Area11.Core.Models;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>Owner-only writes must verify the current account inside their write transaction.
/// Desktop-enabled session proof precedes current-role resolution;legacy disabled hosts are not authenticated.
/// </summary>
internal static class OwnerAccess
{
    public static User Require(Database db, SqliteConnection connection, User? actor)
    {
        db.Sessions.Require(actor);
        if (actor?.IsOwner != true)
            throw new ValidationException("Only the active owner can make this change.");
        var current = db.QuerySingle<User>(connection,
            "SELECT id, name, role, active FROM users WHERE id = @id;", new { id = actor.Id });
        if (current is not { IsOwner: true, Active: true })
            throw new ValidationException("Only the active owner can make this change. Sign in again.");
        db.Sessions.Carry(actor, current);
        return current;
    }
}
