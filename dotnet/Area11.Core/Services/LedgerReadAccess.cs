using Area11.Core.Data;
using Area11.Core.Models;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>
/// Resolve ledger readers from the current local account, not a cached role label. This is NOT
/// authentication of arbitrary in-process callers who can supply somebody else's real account id.
/// Read permissions keep the existing PermissionService role-default/error policy.
/// </summary>
internal static class LedgerReadAccess
{
    public static User Require(Database db, SqliteConnection connection,
        PermissionService permissions, User? actor, string module)
    {
        if (actor is not { Active: true } || actor.Id <= 0)
            throw new ValidationException("Sign in with an active account to read this ledger.");

        var current = db.QuerySingle<User>(connection,
            "SELECT id, name, role, active FROM users WHERE id = @id;", new { id = actor.Id });
        if (current is not { Active: true } || current.Kind != actor.Kind)
            throw new ValidationException("The account has changed. Sign in again before reading this ledger.");
        if (!permissions.Can(current, module))
            throw new ValidationException("You do not have permission to read this ledger.");
        return current;
    }
}
