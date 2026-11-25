using System.Security.Cryptography;
using System.Text;
using Area11.Core.Data;
using Area11.Core.Models;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>Compare complete persisted edit state under BEGIN IMMEDIATE before the first write.
/// No schema/timestamp increments needed;tokens are local,not restart/cross-PC retry identity.</summary>
internal static class EditConcurrency
{
    internal const string Product = "product";
    internal const string Customer = "customer";
    internal const string CustomField = "custom_field";
    internal const string NativeField = "native_field";

    internal static EditVersion Capture(Database db, SqliteConnection connection, string scope, int id)
    {
        var table = scope switch
        {
            Product => "products", Customer => "customers", CustomField => "custom_fields",
            NativeField => "form_field_definitions", "category" => "categories", "company" => "companies",
            "formula" => "generic_formulas", "brand" => "brands", "unit" => "units", "draft" => "document_drafts",
            "pricing" => null,
            "permissions" => null,
            "staff" => null,
            _ when scope.StartsWith("answers:", StringComparison.Ordinal)
                && CustomFieldEntity.IsKnown(scope[8..]) => null,
            _ => throw new InvalidOperationException("Unsupported edit scope.")
        };
        var parts = new List<string>();
        if (table is not null && scope != CustomField)
        {
            var columns = scope == Customer ? "id,name,phone,category,credit_limit_paisa,photo,active" : "*";
            parts.Add(db.Fingerprint(connection, $"SELECT {columns} FROM {table} WHERE id=@id;", id));
        }
        if (scope == "pricing")
        {
            parts.Add(db.Fingerprint(connection, "SELECT id,role,active FROM users WHERE id=@id;", id));
            parts.Add(db.Fingerprint(connection, "SELECT * FROM user_pricing_allowances WHERE user_id=@id;", id));
        }
        if (scope == "permissions")
        {
            parts.Add(db.Fingerprint(connection, "SELECT id,role,active FROM users WHERE id=@id;", id));
            parts.Add(db.Fingerprint(connection, "SELECT module_key,allowed FROM user_permissions WHERE user_id=@id ORDER BY module_key;", id));
        }
        if (scope == "staff")
            parts.Add(db.Fingerprint(connection,
                "SELECT id,name,role,active,password_hash,pin_hash FROM users WHERE id=@id;", id));
        if (scope == Product)
            parts.Add(db.Fingerprint(connection,
                "SELECT category_id FROM product_category_tags WHERE product_id=@id ORDER BY category_id;", id));
        if (scope is Product or Customer || scope.StartsWith("answers:", StringComparison.Ordinal))
        {
            var entity = scope.StartsWith("answers:", StringComparison.Ordinal) ? scope[8..] : scope;
            parts.Add(db.Fingerprint(connection,
                "SELECT * FROM custom_fields WHERE entity=@entity ORDER BY id;", id, entity));
            parts.Add(db.Fingerprint(connection,
                "SELECT field_id,value,updated_at FROM custom_values WHERE entity=@entity AND entity_id=@id ORDER BY field_id;", id, entity));
        }
        if (scope == CustomField)
            parts.Add(db.Fingerprint(connection,
                "SELECT * FROM custom_fields WHERE entity=(SELECT entity FROM custom_fields WHERE id=@id) ORDER BY id;", id));
        return new EditVersion(db, scope, id, Digest(parts));
    }

    private static string Digest(List<string> parts) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(parts))));

    // One entity query per manager list,not one whole-entity query per row on every timer tick.
    internal static void AttachDefinitions(Database db, SqliteConnection connection, string entity, IEnumerable<CustomField> rows)
    {
        var fingerprint = Digest(new List<string> { db.Fingerprint(connection,
            "SELECT * FROM custom_fields WHERE entity=@entity ORDER BY id;", 0, entity) });
        foreach (var row in rows) row.EditVersion = new EditVersion(db, CustomField, row.Id, fingerprint);
    }

    internal static void Require(Database db, SqliteConnection connection, string scope, int id, EditVersion? expected)
    {
        db.RequireWriteTransaction(connection);
        // Existing isolated legacy fixtures need not manufacture UI baselines. Production Desktop
        // enables Sessions before composition;checked callers are enforced in either kind of host.
        if (expected is null && !db.Sessions.Enabled) return;
        if (expected is null || !ReferenceEquals(expected.Database, db) || expected.Scope != scope || expected.Id != id
            || !string.Equals(expected.Fingerprint, Capture(db, connection, scope, id).Fingerprint, StringComparison.Ordinal))
            throw new ValidationException("This record or its fields changed after the form was opened. Nothing from this edit was saved. Reopen/refresh, review the current record and enter the change again.");
    }
}
