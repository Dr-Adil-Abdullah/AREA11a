using Area11.Core.Data;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>
/// Reads quantity semantics from the managed unit row. A unit rename changes labels, not whether
/// stock may be fractional; no stock path should infer this rule from the editable unit name.
/// </summary>
internal static class UnitPolicy
{
    public static bool RequiresWhole(Database db, SqliteConnection connection, string? unitName)
    {
        var name = Normalize(unitName);
        if (name.Length == 0)
        {
            return false;
        }

        var value = db.Scalar(connection, @"
SELECT whole_only FROM units
WHERE active = 1 AND name = @name COLLATE NOCASE
LIMIT 1;", new { name });
        return value is not null && Convert.ToInt64(value) != 0;
    }

    public static string Normalize(string? value) =>
        string.Join(" ", (value ?? string.Empty).Trim()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();
}
