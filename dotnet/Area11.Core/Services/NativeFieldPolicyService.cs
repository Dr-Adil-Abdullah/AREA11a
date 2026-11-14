using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>
/// Owner-controlled native field metadata. This does not relax any form or posting rule: consumers
/// must explicitly integrate a field's policy and keep incomplete financial documents unposted.
/// Definitions themselves are provisioned separately; this service never invents a field.
/// </summary>
public sealed class NativeFieldPolicyService
{
    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly LogService _log;

    public NativeFieldPolicyService(Database db, AuditService audit, LogService log)
    {
        _db = db;
        _audit = audit;
        _log = log;
    }

    /// <summary>
    /// Install a deliberately limited catalog of existing posting-critical inputs without overwriting
    /// the owner's prior choices. This is metadata only; final-post guards remain authoritative.
    /// </summary>
    public ServiceResult EnsureDefaults()
    {
        try
        {
            _db.Tx(connection =>
            {
                var definitions = new[]
                {
                    (Key: "sale.product", Section: "Sale", Label: "Sale product", Type: NativeFieldDataType.Select, Input: NativeInputMethod.Dropdown),
                    (Key: "sale.quantity", Section: "Sale", Label: "Sale quantity", Type: NativeFieldDataType.Quantity, Input: NativeInputMethod.Numeric),
                    (Key: "purchase.supplier", Section: "Purchase", Label: "Purchase supplier", Type: NativeFieldDataType.Select, Input: NativeInputMethod.Dropdown),
                    (Key: "purchase.product", Section: "Purchase", Label: "Purchase product", Type: NativeFieldDataType.Select, Input: NativeInputMethod.Dropdown),
                    (Key: "purchase.quantity", Section: "Purchase", Label: "Purchase quantity", Type: NativeFieldDataType.Quantity, Input: NativeInputMethod.Numeric),
                    (Key: "customer.phone", Section: "Customer", Label: "Customer phone", Type: NativeFieldDataType.Text, Input: NativeInputMethod.Text),
                    (Key: "supplier.phone", Section: "Supplier", Label: "Supplier phone", Type: NativeFieldDataType.Text, Input: NativeInputMethod.Text),
                    (Key: "product.generic", Section: "Product", Label: "Generic / salt", Type: NativeFieldDataType.Text, Input: NativeInputMethod.Text),
                    (Key: "product.brand", Section: "Product", Label: "Brand", Type: NativeFieldDataType.Text, Input: NativeInputMethod.Text),
                    (Key: "product.rack", Section: "Product", Label: "Rack / shelf", Type: NativeFieldDataType.Text, Input: NativeInputMethod.Text),
                    (Key: "product.pack", Section: "Product", Label: "Pack description", Type: NativeFieldDataType.Text, Input: NativeInputMethod.Text),
                    (Key: "product.retail", Section: "Product", Label: "Selling price", Type: NativeFieldDataType.Amount, Input: NativeInputMethod.Numeric),
                    (Key: "supplier.agency", Section: "Supplier", Label: "Supplier agency", Type: NativeFieldDataType.Text, Input: NativeInputMethod.Text)
                };
                foreach (var item in definitions)
                {
                    _db.Execute(connection, @"
INSERT INTO form_field_definitions(field_key, section_key, label, data_type, input_method,
    safe_required, required, critical, sort_order)
VALUES(@key, @section, @label, @type, @input, @safe, @safe, @critical, @sort)
ON CONFLICT(field_key) DO NOTHING;", new
                    {
                        key = item.Key, section = item.Section, label = item.Label,
                        type = item.Type, input = item.Input,
                        // Selling price ships necessary (owner's word, v0.70.0): a product without a
                        // price breaks the Counter total, so the owner must relax it deliberately.
                        safe = item.Section is "Sale" or "Purchase" || item.Key == "product.retail" ? 1 : 0,
                        critical = item.Section is "Sale" or "Purchase" ? 1 : 0,
                        sort = Array.IndexOf(definitions, item)
                    });
                }
            });
            return ServiceResult.Success();
        }
        catch (Exception exception)
        {
            _log.Error("Built-in field defaults could not be prepared.", exception);
            return ServiceResult.Fail("Built-in field defaults could not be prepared.");
        }
    }

    /// <summary>Read within the same transaction as a write; absent optional defaults remain optional.</summary>
    internal static bool IsRequired(Database db, Microsoft.Data.Sqlite.SqliteConnection connection, string fieldKey) =>
        Convert.ToInt64(db.Scalar(connection,
            "SELECT required FROM form_field_definitions WHERE field_key = @key;", new { key = fieldKey }) ?? 0L) == 1;

    internal static string? RequiredTextError(Database db, Microsoft.Data.Sqlite.SqliteConnection connection,
        string key, string label, string? value) =>
        string.IsNullOrWhiteSpace(value) && IsRequired(db, connection, key)
            ? label + " is required by the owner's field settings."
            : null;

    public ServiceResult<List<FormFieldDefinition>> List(User? user)
    {
        if (user is null || !user.IsOwner)
        {
            return ServiceResult<List<FormFieldDefinition>>.Fail("Only the owner can manage built-in fields.");
        }

        try
        {
            using var connection = _db.Open();
            OwnerAccess.Require(_db, connection, user);
            var fields = _db.Query<FormFieldDefinition>(connection,
                "SELECT * FROM form_field_definitions ORDER BY section_key, sort_order, id;");
            return ServiceResult<List<FormFieldDefinition>>.Success(fields);
        }
        catch (Exception exception)
        {
            _log.Error("Built-in fields could not be loaded.", exception);
            return ServiceResult<List<FormFieldDefinition>>.Fail("Built-in fields could not be loaded.");
        }
    }

    /// <summary>
    /// Explicit confirmation applies to this particular requested change. Actor identity comes from
    /// the signed-in owner, not from a name typed into a dialog; the audit and change are atomic.
    /// </summary>
    public ServiceResult SetRequired(User? user, int fieldId, bool required, bool confirmedRisk)
    {
        if (user is null || !user.IsOwner || string.IsNullOrWhiteSpace(user.Name))
        {
            return ServiceResult.Fail("Only a named owner can change built-in field requirements.");
        }

        try
        {
            return _db.Tx(connection =>
            {
                var owner = OwnerAccess.Require(_db, connection, user);
                var field = _db.QuerySingle<FormFieldDefinition>(connection,
                    "SELECT * FROM form_field_definitions WHERE id = @id;", new { id = fieldId });
                if (field is null)
                {
                    return ServiceResult.Fail("That built-in field no longer exists.");
                }

                if (field.Required == required)
                {
                    return ServiceResult.Success();
                }

                var overriding = field.Critical && field.SafeRequired && !required;
                if (overriding && !confirmedRisk)
                {
                    return ServiceResult.Fail("This critical field needs explicit owner risk confirmation before it can be optional.");
                }

                var at = Clock.Now();
                _db.Execute(connection, @"
UPDATE form_field_definitions
SET required = @required, override_acknowledged_by = @by,
    override_acknowledged_at = @ackAt, updated_at = @at
WHERE id = @id;", new
                {
                    required = required ? 1 : 0,
                    by = overriding ? owner.Name.Trim() : null,
                    ackAt = overriding ? at : null,
                    at,
                    id = fieldId
                });
                _audit.Record(connection, owner, Modules.Settings, "native_field_required_changed",
                    "form_field_definitions", field.Id.ToString(),
                    oldValue: field.Required ? "required" : "optional",
                    newValue: required ? "required" : "optional",
                    details: field.FieldKey + (overriding ? "; critical risk acknowledged" : string.Empty));
                return ServiceResult.Success();
            });
        }
        catch (Exception exception)
        {
            _log.Error("Built-in field requirement could not be changed.", exception);
            return ServiceResult.Fail("Built-in field requirement could not be changed.");
        }
    }
}
