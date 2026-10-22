using System.Globalization;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>
/// The owner's own fields (F-42, Spec 2.2, 8.4, E-05, S-04).
///
/// **Why the program does not need a rebuild for a new question.** A pharmacy always finds one more
/// thing worth writing down: the customer's CNIC, the salt behind a brand, the shelf a medicine
/// sits on, the distributor's commission. Instead of a new column and a new screen every time, the
/// owner adds a **field** here and it appears on the form by itself (Spec 8.4.3).
///
/// **Nothing is deleted** (D-00b). A field that is no longer wanted is switched off: the form stops
/// asking, the answers stay in `custom_values`, and switching it on again brings them all back. This
/// is the same rule the photos follow, and for the same reason - a shop that loses its history is a
/// shop that cannot explain last year.
///
/// **A field is not a column.** Its answers live in `custom_values` (one row per record and field), so
/// adding fifty fields never rebuilds a table, and an old `area11.db` opens without a conversion.
/// </summary>
public sealed class CustomFieldService
{
    public const int MinLabelLength = 2;

    public const int MaxLabelLength = 40;

    /// <summary>A text answer longer than this is a note, not a field; notes belong on the khata.</summary>
    public const int MaxAnswerLength = 200;

    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    public CustomFieldService(
        Database db,
        AuditService audit,
        PermissionService permissions,
        LogService log)
    {
        _db = db;
        _audit = audit;
        _permissions = permissions;
        _log = log;
    }

    /// <summary>
    /// The fields of one form, in the owner's order. <paramref name="manage"/> asks for the manager
    /// page (switched-off fields included, and the `settings` permission); the forms ask for the
    /// record's own permission instead.
    /// </summary>
    public ServiceResult<List<CustomField>> List(
        User? user,
        string entity,
        bool includeInactive = false,
        bool manage = false)
    {
        if (!CustomFieldEntity.IsKnown(entity))
        {
            return ServiceResult<List<CustomField>>.Fail("That form has no extra fields.");
        }

        var key = CustomFieldEntity.Clean(entity);
        var module = manage ? Modules.Settings : CustomFieldEntity.ModuleFor(key);

        if (!_permissions.Can(user, module))
        {
            return ServiceResult<List<CustomField>>.Fail(
                manage
                    ? "Only the owner or a manager can change the extra fields."
                    : "You do not have permission to see these fields.");
        }

        try
        {
            using var connection = _db.Open();

            var rows = _db.Query<CustomField>(connection, @"
SELECT id AS id, entity AS entity, label AS label, type AS type,
       options_json AS optionsJson, required AS required, active AS active,
       sort AS sort, created_at AS createdAt
FROM custom_fields
WHERE entity = @entity AND (@includeInactive = 1 OR active = 1)
ORDER BY sort, id;",
                new { entity = key, includeInactive = includeInactive ? 1 : 0 });

            return ServiceResult<List<CustomField>>.Success(rows);
        }
        catch (Exception exception)
        {
            _log.Error($"The custom fields of {key} could not be listed.", exception);
            return ServiceResult<List<CustomField>>.Fail(
                "The extra fields could not be loaded. " + exception.Message);
        }
    }

    /// <summary>One new field. It appears on the form immediately - no restart, no rebuild.</summary>
    public ServiceResult<CustomField> Add(
        User? user,
        string entity,
        string? label,
        string? type,
        string? optionsText,
        bool required)
    {
        if (!_permissions.Can(user, Modules.Settings))
        {
            return ServiceResult<CustomField>.Fail(
                "Only the owner or a manager can add a field.");
        }

        if (!CustomFieldEntity.IsKnown(entity))
        {
            return ServiceResult<CustomField>.Fail("Choose the form this field belongs to.");
        }

        var key = CustomFieldEntity.Clean(entity);
        var cleanType = CustomFieldType.Clean(type);
        var cleanLabel = (label ?? string.Empty).Trim();

        var problem = CheckLabel(cleanLabel) ?? CheckType(cleanType, optionsText);

        if (problem is not null)
        {
            return ServiceResult<CustomField>.Fail(problem);
        }

        var optionsJson = CustomFieldType.NeedsOptions(cleanType)
            ? CustomFieldType.BuildOptions(optionsText)
            : null;

        try
        {
            return _db.Tx(connection =>
            {
                if (LabelExists(connection, key, cleanLabel, exceptId: 0))
                {
                    return ServiceResult<CustomField>.Fail(
                        "There is already a field called \"" + cleanLabel + "\" on this form.");
                }

                var sort = Convert.ToInt32(_db.Scalar(connection,
                    "SELECT IFNULL(MAX(sort), 0) + 1 FROM custom_fields WHERE entity = @entity;",
                    new { entity = key }) ?? 1);

                _db.Execute(connection, @"
INSERT INTO custom_fields(entity, label, type, options_json, required, active, sort, created_at)
VALUES(@entity, @label, @type, @optionsJson, @required, 1, @sort, @at);",
                    new
                    {
                        entity = key,
                        label = cleanLabel,
                        type = cleanType,
                        optionsJson,
                        required = required ? 1 : 0,
                        sort,
                        at = Clock.Now()
                    });

                var id = (int)_db.NextId(connection);

                _audit.Record(connection, user, Modules.Settings, "custom_field_add",
                    "custom_fields", id.ToString(), newValue: Describe(cleanLabel, cleanType, optionsJson, required),
                    details: CustomFieldEntity.LabelFor(key));

                return ServiceResult<CustomField>.Success(new CustomField
                {
                    Id = id,
                    Entity = key,
                    Label = cleanLabel,
                    Type = cleanType,
                    OptionsJson = optionsJson,
                    Required = required,
                    Active = true,
                    Sort = sort,
                    CreatedAt = Clock.Now()
                });
            });
        }
        catch (Exception exception)
        {
            _log.Error($"A custom field ({cleanLabel}) could not be added.", exception);
            return ServiceResult<CustomField>.Fail("The field could not be added. " + exception.Message);
        }
    }

    /// <summary>
    /// Changes the wording, the kind or the required tick. The answers already saved stay where they
    /// are - a field that changes from text to amount cannot rewrite old text, so the screen says so
    /// instead of pretending.
    /// </summary>
    public ServiceResult Update(
        User? user,
        int fieldId,
        string? label,
        string? type,
        string? optionsText,
        bool required)
    {
        if (!_permissions.Can(user, Modules.Settings))
        {
            return ServiceResult.Fail("Only the owner or a manager can change a field.");
        }

        var cleanType = CustomFieldType.Clean(type);
        var cleanLabel = (label ?? string.Empty).Trim();
        var problem = CheckLabel(cleanLabel) ?? CheckType(cleanType, optionsText);

        if (problem is not null)
        {
            return ServiceResult.Fail(problem);
        }

        var optionsJson = CustomFieldType.NeedsOptions(cleanType)
            ? CustomFieldType.BuildOptions(optionsText)
            : null;

        try
        {
            return _db.Tx(connection =>
            {
                var field = ReadField(connection, fieldId);

                if (field is null)
                {
                    return ServiceResult.Fail("That field is not in the list any more.");
                }

                if (LabelExists(connection, field.Entity, cleanLabel, fieldId))
                {
                    return ServiceResult.Fail(
                        "There is already a field called \"" + cleanLabel + "\" on this form.");
                }

                var before = Describe(field.Label, field.Type, field.OptionsJson, field.Required);
                var after = Describe(cleanLabel, cleanType, optionsJson, required);

                _db.Execute(connection, @"
UPDATE custom_fields
SET label = @label, type = @type, options_json = @optionsJson, required = @required
WHERE id = @id;",
                    new { label = cleanLabel, type = cleanType, optionsJson, required = required ? 1 : 0, id = fieldId });

                if (!string.Equals(before, after, StringComparison.Ordinal))
                {
                    _audit.Record(connection, user, Modules.Settings, "custom_field_update",
                        "custom_fields", fieldId.ToString(),
                        oldValue: before, newValue: after, details: field.Entity);
                }

                var typeChanged = !string.Equals(field.Type, cleanType, StringComparison.Ordinal);
                var anyValues = Convert.ToInt32(_db.Scalar(connection,
                    "SELECT COUNT(1) FROM custom_values WHERE field_id = @id;",
                    new { id = fieldId }) ?? 0) > 0;

                return typeChanged && anyValues
                    ? ServiceResult.Success(
                        "The kind of field changed. The answers already saved keep their old text - open the records and check them.")
                    : ServiceResult.Success();
            });
        }
        catch (Exception exception)
        {
            _log.Error($"The custom field {fieldId} could not be changed.", exception);
            return ServiceResult.Fail("The field could not be changed. " + exception.Message);
        }
    }

    /// <summary>Switches a field off or on. The answers are never touched (D-00b).</summary>
    public ServiceResult SetActive(User? user, int fieldId, bool active)
    {
        if (!_permissions.Can(user, Modules.Settings))
        {
            return ServiceResult.Fail("Only the owner or a manager can switch a field off.");
        }

        try
        {
            return _db.Tx(connection =>
            {
                var field = ReadField(connection, fieldId);

                if (field is null)
                {
                    return ServiceResult.Fail("That field is not in the list any more.");
                }

                if (field.Active == active)
                {
                    return ServiceResult.Success();
                }

                _db.Execute(connection, "UPDATE custom_fields SET active = @active WHERE id = @id;",
                    new { active = active ? 1 : 0, id = fieldId });

                _audit.Record(connection, user, Modules.Settings,
                    active ? "custom_field_on" : "custom_field_off", "custom_fields", fieldId.ToString(),
                    oldValue: field.Active ? "shown" : "put away",
                    newValue: active ? "shown" : "put away",
                    details: field.Label);

                return ServiceResult.Success();
            });
        }
        catch (Exception exception)
        {
            _log.Error($"The custom field {fieldId} could not be switched.", exception);
            return ServiceResult.Fail("The field could not be changed. " + exception.Message);
        }
    }

    /// <summary>Moves a field one place up (-1) or down (+1) - the order the form asks its questions.</summary>
    public ServiceResult Move(User? user, int fieldId, int direction)
    {
        if (!_permissions.Can(user, Modules.Settings))
        {
            return ServiceResult.Fail("Only the owner or a manager can reorder the fields.");
        }

        if (direction is not (-1 or 1))
        {
            return ServiceResult.Fail("The field can only move one place.");
        }

        try
        {
            return _db.Tx(connection =>
            {
                var field = ReadField(connection, fieldId);

                if (field is null)
                {
                    return ServiceResult.Fail("That field is not in the list any more.");
                }

                var ordered = _db.Query<CustomField>(connection, @"
SELECT id AS id, entity AS entity, label AS label, type AS type,
       options_json AS optionsJson, required AS required, active AS active,
       sort AS sort, created_at AS createdAt
FROM custom_fields
WHERE entity = @entity
ORDER BY sort, id;", new { entity = field.Entity });

                var index = ordered.FindIndex(row => row.Id == field.Id);
                var target = index + direction;

                if (index < 0 || target < 0 || target >= ordered.Count)
                {
                    return ServiceResult.Fail(direction < 0
                        ? "This field is already first."
                        : "This field is already last.");
                }

                // The whole list is renumbered 1..n with the two neighbours swapped. Swapping only the
                // two numbers would silently do nothing when both rows carry the same sort value.
                (ordered[index], ordered[target]) = (ordered[target], ordered[index]);

                for (var position = 0; position < ordered.Count; position++)
                {
                    _db.Execute(connection, "UPDATE custom_fields SET sort = @sort WHERE id = @id;",
                        new { sort = position + 1, id = ordered[position].Id });
                }

                _audit.Record(connection, user, Modules.Settings, "custom_field_order",
                    "custom_fields", fieldId.ToString(),
                    oldValue: field.Sort.ToString(CultureInfo.InvariantCulture),
                    newValue: (target + 1).ToString(CultureInfo.InvariantCulture),
                    details: field.Label);

                return ServiceResult.Success();
            });
        }
        catch (Exception exception)
        {
            _log.Error($"The custom field {fieldId} could not be moved.", exception);
            return ServiceResult.Fail("The field could not be moved. " + exception.Message);
        }
    }

    /// <summary>The saved answers of one record, by field id. A field with no answer is simply absent.</summary>
    public ServiceResult<Dictionary<int, string>> Values(User? user, string entity, int entityId)
    {
        if (!CustomFieldEntity.IsKnown(entity))
        {
            return ServiceResult<Dictionary<int, string>>.Fail("That form has no extra fields.");
        }

        var key = CustomFieldEntity.Clean(entity);

        if (!_permissions.Can(user, CustomFieldEntity.ModuleFor(key)))
        {
            return ServiceResult<Dictionary<int, string>>.Fail(
                "You do not have permission to see these fields.");
        }

        try
        {
            using var connection = _db.Open();

            var rows = _db.Query<CustomValue>(connection, @"
SELECT id AS id, field_id AS fieldId, entity AS entity, entity_id AS entityId,
       value AS value, updated_at AS updatedAt
FROM custom_values
WHERE entity = @entity AND entity_id = @entityId;",
                new { entity = key, entityId });

            var answers = new Dictionary<int, string>();

            foreach (var row in rows)
            {
                if (!string.IsNullOrEmpty(row.Value))
                {
                    answers[row.FieldId] = row.Value!;
                }
            }

            return ServiceResult<Dictionary<int, string>>.Success(answers);
        }
        catch (Exception exception)
        {
            _log.Error($"The custom values of {key} {entityId} could not be read.", exception);
            return ServiceResult<Dictionary<int, string>>.Fail(
                "The answers could not be loaded. " + exception.Message);
        }
    }

    /// <summary>
    /// Saves every answer of one record in one transaction: **either all of them are written or none
    /// is**. A required field left empty stops the whole save with the field's own name, because
    /// "something is missing" is not a message a shopkeeper can act on.
    /// </summary>
    public ServiceResult SaveValues(
        User? user,
        string entity,
        int entityId,
        IReadOnlyDictionary<int, string?> values)
    {
        if (!CustomFieldEntity.IsKnown(entity))
        {
            return ServiceResult.Fail("That form has no extra fields.");
        }

        var key = CustomFieldEntity.Clean(entity);

        if (!_permissions.Can(user, CustomFieldEntity.ModuleFor(key)))
        {
            return ServiceResult.Fail("You do not have permission to change these fields.");
        }

        try
        {
            return _db.Tx(connection =>
            {
                if (!RecordExists(connection, key, entityId))
                {
                    return ServiceResult.Fail(
                        CustomFieldEntity.LabelFor(key) + " is not in the list any more, so its answers cannot be saved.");
                }

                var fields = _db.Query<CustomField>(connection, @"
SELECT id AS id, entity AS entity, label AS label, type AS type,
       options_json AS optionsJson, required AS required, active AS active,
       sort AS sort, created_at AS createdAt
FROM custom_fields
WHERE entity = @entity AND active = 1
ORDER BY sort, id;", new { entity = key });

                var known = new HashSet<int>(fields.Select(field => field.Id));

                foreach (var fieldId in values.Keys)
                {
                    if (!known.Contains(fieldId))
                    {
                        return ServiceResult.Fail(
                            "One of the fields is not on this form any more. Close the window and open it again.");
                    }
                }

                var photoCount = Convert.ToInt32(_db.Scalar(connection, @"
SELECT COUNT(1) FROM photos
WHERE entity = @entity AND entity_id = @entityId AND IFNULL(active, 1) = 1;",
                    new { entity = key, entityId }) ?? 0);

                // Check everything first: a failure on the fourth field must not leave three answers
                // written and the form half saved.
                var prepared = new List<(CustomField Field, string? Stored)>();

                foreach (var field in fields)
                {
                    // Every active field is checked, even one the caller forgot to send: a missing
                    // required answer must fail here, not on the record that quietly has no CNIC.
                    values.TryGetValue(field.Id, out var raw);

                    if (!Normalize(field, raw, photoCount, out var stored, out var error))
                    {
                        return ServiceResult.Fail(error!);
                    }

                    if (values.ContainsKey(field.Id))
                    {
                        prepared.Add((field, stored));
                    }
                }

                foreach (var (field, stored) in prepared)
                {
                    var before = _db.Scalar(connection, @"
SELECT value FROM custom_values WHERE field_id = @fieldId AND entity_id = @entityId;",
                        new { fieldId = field.Id, entityId })?.ToString();

                    if (string.Equals(before, stored, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (field.IsPhoto)
                    {
                        // A photo field has no value of its own: the gallery is the answer.
                        continue;
                    }

                    _db.Execute(connection, @"
INSERT INTO custom_values(field_id, entity, entity_id, value, updated_at)
VALUES(@fieldId, @entity, @entityId, @value, @at)
ON CONFLICT(field_id, entity_id) DO UPDATE SET value = excluded.value, updated_at = excluded.updated_at;",
                        new
                        {
                            fieldId = field.Id,
                            entity = key,
                            entityId,
                            value = stored,
                            at = Clock.Now()
                        });

                    _audit.Record(connection, user, CustomFieldEntity.ModuleFor(key), "custom_value_set",
                        "custom_values", entityId.ToString(CultureInfo.InvariantCulture),
                        oldValue: before ?? string.Empty,
                        newValue: stored ?? string.Empty,
                        details: field.Label);
                }

                return ServiceResult.Success();
            });
        }
        catch (Exception exception)
        {
            _log.Error($"The custom values of {key} {entityId} could not be saved.", exception);
            return ServiceResult.Fail("The answers could not be saved. " + exception.Message);
        }
    }

    /// <summary>
    /// Turns what the person typed into the text the column keeps, or says exactly what is wrong with
    /// it. Public so the rule can be tested without a window - and so there is only one rule.
    /// </summary>
    public static bool Normalize(
        CustomField field,
        string? raw,
        int photoCount,
        out string? stored,
        out string? error)
    {
        stored = null;
        error = null;

        var text = (raw ?? string.Empty).Trim();
        var type = CustomFieldType.Clean(field.Type);

        if (type == CustomFieldType.Photo)
        {
            // Nothing is stored; "required" means the gallery must hold at least one picture.
            if (field.Required && photoCount <= 0)
            {
                error = "\"" + field.Label + "\" needs at least one photo - use the Photos… button first.";
                return false;
            }

            return true;
        }

        if (text.Length == 0)
        {
            if (field.Required)
            {
                error = type == CustomFieldType.Check
                    ? "\"" + field.Label + "\" must be ticked."
                    : "\"" + field.Label + "\" is required.";
                return false;
            }

            return true;
        }

        switch (type)
        {
            case CustomFieldType.Text:
                if (text.Length > MaxAnswerLength)
                {
                    error = "\"" + field.Label + "\" must be shorter than " + MaxAnswerLength + " letters.";
                    return false;
                }

                stored = text;
                return true;

            case CustomFieldType.Number:
                if (!Qty.TryParse(text, out var number))
                {
                    error = "\"" + field.Label + "\" must be a number, for example 5 or 2.5.";
                    return false;
                }

                stored = Qty.Edit(number);
                return true;

            case CustomFieldType.Amount:
                if (!Money.TryParse(text, out var rupees))
                {
                    error = "\"" + field.Label + "\" must be an amount in rupees, for example 1200.";
                    return false;
                }

                stored = Money.ToPaisa(rupees).ToString(CultureInfo.InvariantCulture);
                return true;

            case CustomFieldType.Date:
                if (!TryReadDate(text, out var date))
                {
                    error = "\"" + field.Label + "\" must be a date, for example 2026-10-22.";
                    return false;
                }

                stored = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                return true;

            case CustomFieldType.Select:
                var match = field.Options.FirstOrDefault(option =>
                    string.Equals(option, text, StringComparison.OrdinalIgnoreCase));

                if (match is null)
                {
                    error = "\"" + field.Label + "\" must be one of: " + string.Join(", ", field.Options) + ".";
                    return false;
                }

                stored = match;
                return true;

            case CustomFieldType.Check:
                var ticked = text is "1" or "true" or "True" or "yes" or "Yes" or "on";

                if (!ticked && text is not ("0" or "false" or "False" or "no" or "No" or "off"))
                {
                    error = "\"" + field.Label + "\" must be yes or no.";
                    return false;
                }

                if (field.Required && !ticked)
                {
                    error = "\"" + field.Label + "\" must be ticked.";
                    return false;
                }

                stored = ticked ? "1" : "0";
                return true;

            default:
                error = "That kind of field is not understood.";
                return false;
        }
    }

    /// <summary>"2026-10-22" or "22/10/2026" - the two ways a shop writes a date.</summary>
    private static bool TryReadDate(string text, out DateTime date)
    {
        var formats = new[] { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy" };

        return DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out date);
    }

    private static string? CheckLabel(string label)
    {
        if (label.Length < MinLabelLength)
        {
            return "The field needs a name of at least " + MinLabelLength + " letters.";
        }

        if (label.Length > MaxLabelLength)
        {
            return "The name is too long - keep it under " + MaxLabelLength + " letters.";
        }

        return null;
    }

    private static string? CheckType(string type, string? optionsText)
    {
        if (!CustomFieldType.IsKnown(type))
        {
            return "Choose what kind of answer this field takes.";
        }

        if (CustomFieldType.NeedsOptions(type))
        {
            var options = CustomFieldType.ParseOptions(CustomFieldType.BuildOptions(optionsText));

            if (options.Count < 2)
            {
                return "A list needs at least two choices, separated by commas.";
            }
        }

        return null;
    }

    /// <summary>The same name twice on one form would make two identical questions.</summary>
    private bool LabelExists(Microsoft.Data.Sqlite.SqliteConnection connection, string entity,
        string label, int exceptId)
    {
        return Convert.ToInt32(_db.Scalar(connection, @"
SELECT COUNT(1) FROM custom_fields
WHERE entity = @entity AND LOWER(label) = LOWER(@label) AND id <> @id;",
            new { entity, label, id = exceptId }) ?? 0) > 0;
    }

    private CustomField? ReadField(Microsoft.Data.Sqlite.SqliteConnection connection, int fieldId)
    {
        return _db.QuerySingle<CustomField>(connection, @"
SELECT id AS id, entity AS entity, label AS label, type AS type,
       options_json AS optionsJson, required AS required, active AS active,
       sort AS sort, created_at AS createdAt
FROM custom_fields WHERE id = @id;", new { id = fieldId });
    }

    private bool RecordExists(Microsoft.Data.Sqlite.SqliteConnection connection,
        string entity, int entityId)
    {
        var table = entity switch
        {
            CustomFieldEntity.Product => "products",
            CustomFieldEntity.Customer => "customers",
            CustomFieldEntity.Supplier => "suppliers",
            _ => string.Empty
        };

        if (table.Length == 0)
        {
            return false;
        }

        // The table name comes from this file's own list, never from the caller; the id is a parameter.
        return Convert.ToInt32(_db.Scalar(connection,
            "SELECT COUNT(1) FROM " + table + " WHERE id = @id AND active = 1;",
            new { id = entityId }) ?? 0) > 0;
    }

    private static string Describe(string label, string type, string? optionsJson, bool required)
    {
        var options = CustomFieldType.ParseOptions(optionsJson);

        return label + " · " + CustomFieldType.LabelFor(type)
               + (options.Count > 0 ? " (" + string.Join(", ", options) + ")" : string.Empty)
               + (required ? " · required" : string.Empty);
    }
}
