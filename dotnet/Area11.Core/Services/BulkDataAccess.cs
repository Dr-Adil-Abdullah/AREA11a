using Area11.Core.Data;
using Area11.Core.Models;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>Editable files are refused, never projected to an importable masked zero.</summary>
internal static class BulkDataAccess
{
    public static User Require(Database db, SqliteConnection connection, PermissionService permissions,
        SettingsService settings, User? actor, bool cost = false, bool mixed = false)
    {
        var reader = SnapshotReadAccess.Require(db, connection, permissions, actor, Modules.Import);
        if ((cost || mixed) && !CostPolicy.CanSee(permissions, settings, reader))
            throw new ValidationException("Editable cost data requires View Purchase Cost and the main financial switch.");
        if (mixed && !ProfitPolicy.CanSee(permissions, settings, reader))
            throw new ValidationException("The mixed shop workbook also requires View Profit and the main financial switch.");
        return reader;
    }
}
