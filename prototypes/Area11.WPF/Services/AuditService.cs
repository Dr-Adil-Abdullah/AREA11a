using System.Text.Json;
using Area11.WPF.Utils;
using Microsoft.Data.Sqlite;

namespace Area11.WPF.Services;

/// <summary>
/// Black box: every change is stored with who / when / module / old value → new value.
/// Rows are never deleted or updated.
/// </summary>
public sealed class AuditService
{
    public void Record(
        SqliteConnection connection,
        string userName,
        string module,
        string action,
        string entity,
        int? entityId,
        object? before,
        object? after,
        string? detail = null)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"INSERT INTO audit_logs
            (at, user_name, module, action, entity, entity_id, old_value, new_value, detail, ip)
            VALUES ($at, $user, $module, $action, $entity, $entityId, $old, $new, $detail, $ip);";

        command.Parameters.AddWithValue("$at", FormatHelper.Stamp());
        command.Parameters.AddWithValue("$user", userName);
        command.Parameters.AddWithValue("$module", module);
        command.Parameters.AddWithValue("$action", action);
        command.Parameters.AddWithValue("$entity", entity);
        command.Parameters.AddWithValue("$entityId", entityId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$old", before is null ? DBNull.Value : JsonSerializer.Serialize(before));
        command.Parameters.AddWithValue("$new", after is null ? DBNull.Value : JsonSerializer.Serialize(after));
        command.Parameters.AddWithValue("$detail", detail ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$ip", "local");
        command.ExecuteNonQuery();
    }
}
