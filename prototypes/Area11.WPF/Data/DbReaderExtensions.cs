using Microsoft.Data.Sqlite;

namespace Area11.WPF.Data;

/// <summary>Null-safe column readers so mapping code stays free of repeated IsDBNull checks.</summary>
public static class DbReaderExtensions
{
    public static string Str(this SqliteDataReader reader, string column)
    {
        var index = reader.GetOrdinal(column);
        return reader.IsDBNull(index) ? string.Empty : reader.GetString(index);
    }

    public static string? NullStr(this SqliteDataReader reader, string column)
    {
        var index = reader.GetOrdinal(column);
        return reader.IsDBNull(index) ? null : reader.GetString(index);
    }

    public static int Int(this SqliteDataReader reader, string column)
    {
        var index = reader.GetOrdinal(column);
        return reader.IsDBNull(index) ? 0 : reader.GetInt32(index);
    }

    public static int? NullInt(this SqliteDataReader reader, string column)
    {
        var index = reader.GetOrdinal(column);
        return reader.IsDBNull(index) ? null : reader.GetInt32(index);
    }

    public static long Long(this SqliteDataReader reader, string column)
    {
        var index = reader.GetOrdinal(column);
        return reader.IsDBNull(index) ? 0L : reader.GetInt64(index);
    }

    public static long? NullLong(this SqliteDataReader reader, string column)
    {
        var index = reader.GetOrdinal(column);
        return reader.IsDBNull(index) ? null : reader.GetInt64(index);
    }

    public static bool Bool(this SqliteDataReader reader, string column) => reader.Long(column) != 0;
}
