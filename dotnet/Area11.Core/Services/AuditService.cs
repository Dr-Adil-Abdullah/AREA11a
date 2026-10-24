using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>
/// The permanent black box (Spec 22). Rows are only ever inserted; nothing in the whole
/// program deletes or updates audit_logs. Secrets never arrive here — callers pass
/// "changed: yes/no" instead of a password or PIN.
/// </summary>
public sealed class AuditService
{
    private readonly Database _db;
    private readonly LogService _log;

    public AuditService(Database db, LogService log)
    {
        _db = db;
        _log = log;
    }

    public void Record(
        SqliteConnection connection,
        User? actor,
        string module,
        string action,
        string? entity = null,
        string? entityId = null,
        string? oldValue = null,
        string? newValue = null,
        string? details = null)
    {
        _db.Execute(connection, @"
INSERT INTO audit_logs(at, user_id, user_name, action, module, entity, entity_id, old_value, new_value, details)
VALUES(@at, @userId, @userName, @action, @module, @entity, @entityId, @oldValue, @newValue, @details);",
            new
            {
                at = Clock.Now(),
                userId = actor?.Id,
                userName = actor?.Name ?? "system",
                action,
                module,
                entity,
                entityId,
                oldValue,
                newValue,
                details
            });
    }

    /// <summary>Records only the fields that changed (Spec 22.4). No change → no row.</summary>
    public void RecordChange(
        SqliteConnection connection,
        User? actor,
        string module,
        string action,
        string entity,
        string? entityId,
        object? before,
        object? after,
        string? details = null)
    {
        var (oldValue, newValue) = AuditDiff.Compare(before, after);
        if (oldValue is null && newValue is null)
        {
            return;
        }

        Record(connection, actor, module, action, entity, entityId, oldValue, newValue, details);
    }

    /// <summary>Convenience for events that do not belong to a caller transaction.</summary>
    public void RecordStandalone(User? actor, string module, string action, string? details = null)
    {
        try
        {
            using var connection = _db.Open();
            Record(connection, actor, module, action, details: details);
        }
        catch (Exception exception)
        {
            _log.Error($"Audit row could not be written ({module}/{action}).", exception);
        }
    }
}
