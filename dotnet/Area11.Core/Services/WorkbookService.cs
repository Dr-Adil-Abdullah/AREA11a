using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>
/// Native .xlsx templates, list downloads and the all-data workbook (T-10, Spec 24).
///
/// New-record imports still use <see cref="ImportService"/>. This service handles the different job
/// of updating rows that came from "Download your data". Empty cells mean no change; __CLEAR__ is
/// explicit. Every row carries a snapshot hash: a changed database row cannot be silently overwritten
/// by an old workbook, while an already-applied workbook still previews as zero changes. Stock,
/// balances, security/secrets, counters and photos are never writable here. Apply verifies every old
/// value again and writes all records and their audit lines in ONE transaction.
/// </summary>
public sealed class WorkbookService
{
    private const string ClearToken = "__CLEAR__";
    private const string KeyAlias = "K";

    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly PermissionService _permissions;
    private readonly SettingsService _settings;
    private readonly LogService _log;
    private readonly ImportService _imports;

    public WorkbookService(
        Database db,
        AuditService audit,
        PermissionService permissions,
        SettingsService settings,
        LogService log,
        ImportService imports)
    {
        _db = db;
        _audit = audit;
        _permissions = permissions;
        _settings = settings;
        _log = log;
        _imports = imports;
    }

    /// <summary>Products, Customers and Suppliers worksheets plus instructions, with no package.</summary>
    public static byte[] Template() => XlsxFile.Write(new XlsxWorkbook
    {
        Sheets =
        {
            HeaderSheet("Products", ImportService.ProductColumns),
            HeaderSheet("Customers", ImportService.CustomerColumns),
            HeaderSheet("Suppliers", ImportService.SupplierColumns),
            InstructionsSheet(isTemplate: true)
        }
    });

    /// <summary>One ordinary list as a real workbook; the existing CSV queries stay unchanged.</summary>
    public ServiceResult<byte[]> ExportList(User user, string kind)
    {
        var exported = kind switch
        {
            "Customers" => _imports.ExportCustomers(user),
            "Suppliers" => _imports.ExportSuppliers(user),
            _ => _imports.ExportProducts(user)
        };

        if (!exported.Ok || exported.Value is null)
        {
            return ServiceResult<byte[]>.Fail(exported.Error ?? "The list could not be exported.");
        }

        try
        {
            var rows = CsvReader.Parse(exported.Value);
            var sheet = new XlsxSheet { Name = kind };
            sheet.Rows.AddRange(rows);
            return ServiceResult<byte[]>.Success(XlsxFile.Write(new XlsxWorkbook
            {
                Sheets = { sheet, InstructionsSheet(isTemplate: false) }
            }));
        }
        catch (Exception exception)
        {
            _log.Error("The Excel list could not be written.", exception);
            return ServiceResult<byte[]>.Fail("The Excel workbook could not be written. " + exception.Message);
        }
    }

    /// <summary>The eight Spec 24.2 sheets: six data sheets, Settings and Instructions; no photos.</summary>
    public ServiceResult<byte[]> ExportAll(User user)
    {
        if (!CanImport(user))
        {
            return ServiceResult<byte[]>.Fail("Only the owner or a manager can download the shop workbook.");
        }

        try
        {
            using var connection = _db.Open();
            var workbook = new XlsxWorkbook();
            foreach (var schema in Schemas)
            {
                workbook.Sheets.Add(ExportSheet(schema, ReadRows(connection, schema)));
            }

            workbook.Sheets.Add(ExportSettings(ReadSettings(connection)));
            workbook.Sheets.Add(InstructionsSheet(isTemplate: false));
            return ServiceResult<byte[]>.Success(XlsxFile.Write(workbook));
        }
        catch (Exception exception)
        {
            _log.Error("The all-data Excel workbook could not be written.", exception);
            return ServiceResult<byte[]>.Fail("The all-data workbook could not be written. " + exception.Message);
        }
    }

    /// <summary>Reads and checks the whole edited workbook. This method never writes the database.</summary>
    public ServiceResult<BulkUpdatePreview> PreviewBulk(User user, byte[] bytes, bool includeSettings)
    {
        if (!CanImport(user))
        {
            return ServiceResult<BulkUpdatePreview>.Fail("Only the owner or a manager can preview a bulk update.");
        }

        if (includeSettings && !_permissions.Can(user, Modules.Settings))
        {
            return ServiceResult<BulkUpdatePreview>.Fail("Settings changes need Settings permission as well as the separate tick.");
        }

        var preview = new BulkUpdatePreview { IncludeSettings = includeSettings };
        try
        {
            var workbook = XlsxFile.Read(bytes);
            using var connection = _db.Open();
            var recognised = false;

            foreach (var sheet in workbook.Sheets)
            {
                if (string.Equals(sheet.Name, "Instructions", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.Equals(sheet.Name, "Settings", StringComparison.OrdinalIgnoreCase))
                {
                    recognised = true;
                    PreviewSettings(connection, sheet, preview);
                    continue;
                }

                var schema = Schemas.FirstOrDefault(candidate =>
                    string.Equals(candidate.Name, sheet.Name, StringComparison.OrdinalIgnoreCase));
                if (schema is null)
                {
                    preview.Rows.Add(ErrorRow(sheet.Name, 1, "headings", "This worksheet is not part of the Area11 all-data workbook."));
                    continue;
                }

                recognised = true;
                PreviewSheet(connection, schema, sheet, preview);
            }

            if (!recognised)
            {
                return ServiceResult<BulkUpdatePreview>.Fail("Choose the workbook made by Download your data.");
            }

            ValidateAcrossRows(connection, preview);
            return ServiceResult<BulkUpdatePreview>.Success(preview);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or ArgumentException)
        {
            return ServiceResult<BulkUpdatePreview>.Fail("The workbook could not be read. " + exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error("The all-data workbook preview failed.", exception);
            return ServiceResult<BulkUpdatePreview>.Fail("The workbook preview failed. " + exception.Message);
        }
    }

    /// <summary>Applies exactly the approved plan, or rolls everything back including its audit rows.</summary>
    public ServiceResult<BulkUpdateOutcome> ApplyBulk(User user, BulkUpdatePreview preview)
    {
        if (!CanImport(user))
        {
            return ServiceResult<BulkUpdateOutcome>.Fail("Only the owner or a manager can apply a bulk update.");
        }

        if (!preview.CanApply)
        {
            return ServiceResult<BulkUpdateOutcome>.Fail(preview.ErrorCount > 0
                ? "The workbook still has errors. Nothing was changed."
                : "There are no changes to apply.");
        }

        var hasSettings = preview.Pending.Any(record => record.Table == "settings");
        if (hasSettings && (!preview.IncludeSettings || !_permissions.Can(user, Modules.Settings)))
        {
            return ServiceResult<BulkUpdateOutcome>.Fail("Settings need their own tick and Settings permission.");
        }

        try
        {
            IReadOnlyDictionary<string, string> preparedSettings = new Dictionary<string, string>();
            if (preview.SettingValues.Count > 0)
            {
                _settings.Reload();
                var preparation = _settings.PrepareBulk(preview.SettingValues);
                if (!preparation.Ok || preparation.Value is null)
                {
                    return ServiceResult<BulkUpdateOutcome>.Fail(preparation.Error ?? "The settings are invalid.");
                }

                preparedSettings = preparation.Value;
            }

            var outcome = _db.Tx(connection =>
            {
                var result = new BulkUpdateOutcome();

                // Validate EVERY record before the first UPDATE. A late stale row or a cross-record
                // category/name conflict therefore cannot leave the earlier records half applied.
                foreach (var record in preview.Pending)
                {
                    VerifyPlan(record);
                    VerifyCurrentRow(connection, record);
                    if (record.Table == "products")
                    {
                        var unit = record.Fields.FirstOrDefault(field => field.SqlName == "base_unit");
                        if (unit is not null && !ActiveUnitExists(connection,
                                NormalizeUnitName(Convert.ToString(unit.NewValue, CultureInfo.InvariantCulture))))
                        {
                            throw new ValidationException(
                                "A base unit in this workbook is no longer active. Preview a fresh workbook; nothing was changed.");
                        }
                    }
                }

                var oldErrorCount = preview.ErrorCount;
                ValidateAcrossRows(connection, preview);
                if (preview.ErrorCount != oldErrorCount)
                {
                    throw new ValidationException("The workbook's combined records conflict with the current shop. Preview a fresh workbook.");
                }

                foreach (var record in preview.Pending)
                {
                    var parameters = new DynamicParameters();
                    var assignments = new List<string>();
                    for (var index = 0; index < record.Fields.Count; index++)
                    {
                        var field = record.Fields[index];
                        var value = field.NewValue;
                        if (record.Table == "settings" && field.SqlName == "value")
                        {
                            var key = Convert.ToString(record.KeyValue, CultureInfo.InvariantCulture) ?? string.Empty;
                            value = preparedSettings[key];
                        }

                        parameters.Add("p" + index.ToString(CultureInfo.InvariantCulture), value);
                        assignments.Add(field.SqlName + " = @p" + index.ToString(CultureInfo.InvariantCulture));
                    }

                    if (record.Table == "batches")
                    {
                        var expiry = record.Fields.FirstOrDefault(field => field.SqlName == "expiry_date");
                        if (expiry is not null)
                        {
                            var text = Convert.ToString(expiry.NewValue, CultureInfo.InvariantCulture) ?? string.Empty;
                            parameters.Add("expiryYm", text.Length >= 7 ? text[..7] : null);
                            assignments.Add("expiry_ym = @expiryYm");
                        }
                    }

                    if (record.HasUpdatedAt)
                    {
                        parameters.Add("at", Clock.Now());
                        assignments.Add("updated_at = @at");
                    }

                    parameters.Add("key", record.KeyValue);
                    var sql = $"UPDATE {record.Table} SET {string.Join(", ", assignments)} WHERE {record.KeyColumn} = @key;";
                    _db.Execute(connection, sql, parameters);

                    _audit.RecordChange(connection, user, record.Module, "workbook_update",
                        record.Table, Convert.ToString(record.KeyValue, CultureInfo.InvariantCulture),
                        record.Before, record.After,
                        "Native .xlsx bulk update from " + record.Sheet + "; approved before → after preview.");

                    result.ChangedRecords++;
                    result.ChangedFields += record.Fields.Count;
                }

                return result;
            });

            if (hasSettings)
            {
                _settings.Reload();
            }

            _log.Info($"Native workbook update: {outcome.ChangedRecords} record(s), {outcome.ChangedFields} field(s).");
            return ServiceResult<BulkUpdateOutcome>.Success(outcome);
        }
        catch (Exception exception)
        {
            _log.Error("The native workbook update was rolled back.", exception);
            return ServiceResult<BulkUpdateOutcome>.Fail(
                "The workbook was not applied; every change was rolled back. " + exception.Message);
        }
    }

    // ============================== Preview: data sheets ==============================

    private void PreviewSheet(
        SqliteConnection connection,
        SheetSchema schema,
        XlsxSheet sheet,
        BulkUpdatePreview preview)
    {
        if (sheet.Rows.Count == 0)
        {
            preview.Rows.Add(ErrorRow(schema.Name, 1, "headings", "The worksheet is empty."));
            return;
        }

        var map = CsvReader.HeaderMap(sheet.Rows[0]);
        if (!map.ContainsKey("id") || !map.ContainsKey("snapshot"))
        {
            preview.Rows.Add(ErrorRow(schema.Name, 1, "headings",
                "Use the unchanged Id and Snapshot columns from Download your data. New records belong on the Import tab."));
            return;
        }

        var currentRows = ReadRows(connection, schema)
            .ToDictionary(row => KeyText(row[KeyAlias]), StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 1; index < sheet.Rows.Count; index++)
        {
            var source = sheet.Rows[index];
            var line = index + 1;
            if (source.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            string Field(string heading) => map.TryGetValue(heading, out var position)
                ? CsvReader.At(source, position)
                : string.Empty;

            var id = Field("id");
            if (!int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericId)
                || numericId <= 0 || !seen.Add(numericId.ToString(CultureInfo.InvariantCulture)))
            {
                preview.Rows.Add(ErrorRow(schema.Name, line, id, "The Id is invalid, duplicated, or not a positive number."));
                continue;
            }

            if (!currentRows.TryGetValue(numericId.ToString(CultureInfo.InvariantCulture), out var current))
            {
                preview.Rows.Add(ErrorRow(schema.Name, line, id,
                    "That Id is no longer in the shop. New records must use the Import tab, not bulk update."));
                continue;
            }

            var key = RecordKey(current, numericId);
            var parsed = new Dictionary<ColumnSchema, ParsedCell>();
            var errors = new List<string>();
            var matchesFile = true;

            foreach (var column in schema.Columns)
            {
                var heading = CsvReader.Normalise(column.Heading);
                if (!map.ContainsKey(heading))
                {
                    continue;
                }

                var text = Field(heading);
                if (text.Length == 0)
                {
                    continue; // Spec 24.2.3: empty means no change, not erase.
                }

                ParsedCell value;
                try
                {
                    value = ParseCell(column, text);
                }
                catch (ValidationException exception)
                {
                    errors.Add(column.Heading + ": " + exception.Message);
                    matchesFile = false;
                    continue;
                }

                parsed[column] = value;
                var same = SameValue(column, current[column.SqlName], value.Value);
                if (!same)
                {
                    matchesFile = false;
                    if (!column.Editable)
                    {
                        errors.Add(column.Heading + " is read-only; stock, balances and record links cannot be changed here.");
                    }
                }
            }

            if (matchesFile)
            {
                // This also makes an already-applied workbook idempotent even though its old
                // snapshot no longer matches the new database value.
                preview.Rows.Add(StateRow(schema.Name, line, key, BulkUpdateState.Same));
                continue;
            }

            var currentFingerprint = Fingerprint(schema, current);
            var snapshot = Field("snapshot");
            if (!string.Equals(snapshot, currentFingerprint, StringComparison.OrdinalIgnoreCase))
            {
                // Check staleness before calling a changed read-only value an edit. A sale after
                // download changes Stock on shelf too; that is a stale file, not a stock edit.
                preview.Rows.Add(ErrorRow(schema.Name, line, key,
                    "This record changed after the workbook was downloaded (or its Snapshot was edited). Download a fresh workbook and preview again."));
                continue;
            }

            if (errors.Count > 0)
            {
                preview.Rows.Add(ErrorRow(schema.Name, line, key, string.Join(" ", errors)));
                continue;
            }

            var changed = parsed.Where(pair => pair.Key.Editable
                && !SameValue(pair.Key, current[pair.Key.SqlName], pair.Value.Value)).ToList();
            if (changed.Count == 0)
            {
                preview.Rows.Add(StateRow(schema.Name, line, key, BulkUpdateState.Same));
                continue;
            }

            var after = new Dictionary<string, object?>(current, StringComparer.OrdinalIgnoreCase);
            foreach (var pair in changed)
            {
                after[pair.Key.SqlName] = pair.Value.Value;
            }

            var changedNames = changed.Select(pair => pair.Key.SqlName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var validation = ValidateRecord(connection, schema, after, changedNames);
            if (validation is not null)
            {
                preview.Rows.Add(ErrorRow(schema.Name, line, key, validation));
                continue;
            }

            var row = StateRow(schema.Name, line, key, BulkUpdateState.Ready);
            var pending = new PendingBulkRecord
            {
                Sheet = schema.Name + " line " + line.ToString(CultureInfo.InvariantCulture),
                Table = schema.Table,
                KeyColumn = schema.KeyColumn,
                KeyValue = numericId,
                Module = schema.Module,
                HasUpdatedAt = schema.HasUpdatedAt,
                Snapshot = currentFingerprint
            };

            foreach (var pair in changed)
            {
                var oldDisplay = Display(pair.Key, current[pair.Key.SqlName]);
                row.Changes.Add(new BulkFieldChange
                {
                    Field = pair.Key.Heading,
                    Before = oldDisplay,
                    After = pair.Value.Display
                });
                pending.Fields.Add(new PendingBulkField
                {
                    SqlName = pair.Key.SqlName,
                    OldValue = current[pair.Key.SqlName],
                    NewValue = pair.Value.Value
                });
                pending.Before[pair.Key.SqlName] = current[pair.Key.SqlName];
                pending.After[pair.Key.SqlName] = pair.Value.Value;
            }

            foreach (var pair in after.Where(pair => pair.Key != KeyAlias))
            {
                pending.RowAfter[pair.Key] = pair.Value;
            }

            preview.Rows.Add(row);
            preview.Pending.Add(pending);
        }
    }

    private string? ValidateRecord(
        SqliteConnection connection,
        SheetSchema schema,
        IReadOnlyDictionary<string, object?> after,
        IReadOnlySet<string> changed)
    {
        if (schema.Name == "Products")
        {
            var cost = Convert.ToInt64(after["cost_paisa"], CultureInfo.InvariantCulture);
            var retail = Convert.ToInt64(after["retail_paisa"], CultureInfo.InvariantCulture);
            if (retail < cost)
            {
                return "Retail cannot be below cost.";
            }

            if (changed.Contains("barcode") && after["barcode"] is not null)
            {
                var barcode = Convert.ToString(after["barcode"], CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
                if (barcode.Length > 0 && Convert.ToInt64(_db.Scalar(connection, @"
SELECT COUNT(1) FROM product_barcodes
WHERE active = 1 AND product_id <> @id AND LOWER(TRIM(barcode)) = LOWER(@code);",
                    new { id = after[KeyAlias], code = barcode }) ?? 0L) > 0)
                {
                    return "That barcode already belongs to another medicine's active pack barcodes.";
                }
            }

            if (changed.Contains("category_id") && after["category_id"] is not null
                && !ReferenceExists(connection, "categories", after["category_id"], activeOnly: true))
            {
                return "Choose a category that exists and is active.";
            }

            if (changed.Contains("company_id") && after["company_id"] is not null
                && !ReferenceExists(connection, "companies", after["company_id"], activeOnly: true))
            {
                return "Choose a company that exists and is active.";
            }

            if (changed.Contains("base_unit"))
            {
                var unit = NormalizeUnitName(Convert.ToString(after["base_unit"], CultureInfo.InvariantCulture));
                if (!ActiveUnitExists(connection, unit))
                {
                    return "Base unit '" + unit + "' is not on the active managed list. Add or restore it in Manage units first.";
                }
            }
        }
        else if (schema.Name == "Customers")
        {
            var category = Convert.ToString(after["category"], CultureInfo.InvariantCulture)?.ToLowerInvariant();
            if (category is not ("normal" or "vip" or "doctor"))
            {
                return "Customer Category must be normal, vip or doctor.";
            }
        }
        else if (schema.Name == "Categories" && after["parent_id"] is not null)
        {
            if (SqlValuesEqual(after["parent_id"], after[KeyAlias]))
            {
                return "A category cannot be its own parent.";
            }

            if (!ReferenceExists(connection, "categories", after["parent_id"], activeOnly: changed.Contains("parent_id")))
            {
                return "Choose a parent category that exists and is active.";
            }
        }
        else if (schema.Name == "Batches" && after["supplier_id"] is not null
            && changed.Contains("supplier_id")
            && !ReferenceExists(connection, "suppliers", after["supplier_id"], activeOnly: true))
        {
            return "Choose a supplier that exists and is active.";
        }

        return null;
    }

    private bool ReferenceExists(SqliteConnection connection, string table, object? value, bool activeOnly)
    {
        // Only the four fixed callers above reach this method; no workbook text becomes an identifier.
        var sql = table switch
        {
            "categories" => "SELECT COUNT(1) FROM categories WHERE id = @id" + (activeOnly ? " AND active = 1" : string.Empty),
            "companies" => "SELECT COUNT(1) FROM companies WHERE id = @id" + (activeOnly ? " AND active = 1" : string.Empty),
            "suppliers" => "SELECT COUNT(1) FROM suppliers WHERE id = @id" + (activeOnly ? " AND active = 1" : string.Empty),
            _ => throw new InvalidOperationException("Unknown reference table.")
        };
        return Convert.ToInt64(_db.Scalar(connection, sql, new { id = value }) ?? 0L) > 0;
    }

    private bool ActiveUnitExists(SqliteConnection connection, string name) =>
        name.Length > 0 && Convert.ToInt64(_db.Scalar(connection,
            "SELECT COUNT(1) FROM units WHERE active = 1 AND name = @name COLLATE NOCASE;",
            new { name }) ?? 0L) > 0;

    private static string NormalizeUnitName(string? value) =>
        string.Join(" ", (value ?? string.Empty).Trim()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();

    private void ValidateAcrossRows(SqliteConnection connection, BulkUpdatePreview preview)
    {
        foreach (var schema in Schemas)
        {
            var pending = preview.Pending.Where(record => record.Table == schema.Table).ToList();
            if (pending.Count == 0)
            {
                continue;
            }

            var finalRows = ReadRows(connection, schema).ToDictionary(row => KeyText(row[KeyAlias]), StringComparer.Ordinal);
            foreach (var record in pending)
            {
                var key = KeyText(record.KeyValue);
                var row = finalRows[key];
                foreach (var pair in record.RowAfter)
                {
                    row[pair.Key] = pair.Value;
                }
            }

            if (schema.Name is "Categories" or "Companies")
            {
                var duplicate = finalRows.Values
                    .GroupBy(row => Convert.ToString(row["name"], CultureInfo.InvariantCulture)?.Trim() ?? string.Empty,
                        StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault(group => group.Key.Length > 0 && group.Count() > 1);
                if (duplicate is not null)
                {
                    preview.Rows.Add(ErrorRow(schema.Name, 0, duplicate.Key,
                        "This name would be used by more than one record, including case-only duplicates. Nothing can be applied."));
                }
            }

            if (schema.Name == "Products")
            {
                var duplicate = finalRows.Values
                    .Select(row => Convert.ToString(row["barcode"], CultureInfo.InvariantCulture)?.Trim() ?? string.Empty)
                    .Where(code => code.Length > 0)
                    .GroupBy(code => code, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault(group => group.Count() > 1);
                if (duplicate is not null)
                {
                    preview.Rows.Add(ErrorRow(schema.Name, 0, duplicate.Key,
                        "This barcode would belong to more than one medicine. Nothing can be applied."));
                }
            }

            if (schema.Name == "Categories")
            {
                var parents = finalRows.ToDictionary(pair => int.Parse(pair.Key, CultureInfo.InvariantCulture),
                    pair => pair.Value["parent_id"] is null
                        ? (int?)null
                        : Convert.ToInt32(pair.Value["parent_id"], CultureInfo.InvariantCulture));
                foreach (var id in parents.Keys)
                {
                    var seen = new HashSet<int>();
                    var current = id;
                    var depth = 0;
                    while (parents[current] is { } parent)
                    {
                        if (!seen.Add(current) || !parents.ContainsKey(parent) || ++depth > 1)
                        {
                            preview.Rows.Add(ErrorRow(schema.Name, 0, id.ToString(CultureInfo.InvariantCulture),
                                "The category changes would make a cycle or more than two levels. Nothing can be applied."));
                            return;
                        }

                        current = parent;
                    }
                }
            }
        }
    }

    // ============================== Preview: Settings ==============================

    private void PreviewSettings(SqliteConnection connection, XlsxSheet sheet, BulkUpdatePreview preview)
    {
        if (sheet.Rows.Count == 0)
        {
            preview.Rows.Add(ErrorRow("Settings", 1, "headings", "The Settings worksheet is empty."));
            return;
        }

        var map = CsvReader.HeaderMap(sheet.Rows[0]);
        if (!map.ContainsKey("key"))
        {
            preview.Rows.Add(ErrorRow("Settings", 1, "headings", "The Settings sheet needs its Key column."));
            return;
        }

        var current = ReadSettings(connection);
        var candidates = new List<SettingCandidate>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 1; index < sheet.Rows.Count; index++)
        {
            var source = sheet.Rows[index];
            var line = index + 1;
            if (source.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            string Field(string heading) => map.TryGetValue(heading, out var position)
                ? CsvReader.At(source, position)
                : string.Empty;
            var key = Field("key");
            if (key.Length == 0 || !seen.Add(key) || !SettingsService.KnownKeys.Contains(key))
            {
                preview.Rows.Add(ErrorRow("Settings", line, key, "The setting key is empty, duplicated, or unknown."));
                continue;
            }

            if (IsLockedSetting(key))
            {
                preview.Rows.Add(ErrorRow("Settings", line, key,
                    "Security, secrets, numbering counters, retired stock flags and operational state are locked."));
                continue;
            }

            if (!preview.IncludeSettings)
            {
                var skipped = StateRow("Settings", line, key, BulkUpdateState.Skipped);
                skipped.Message = "Settings were not included. Tick the separate Settings box and preview again if you intended to change them.";
                preview.Rows.Add(skipped);
                continue;
            }

            if (!current.TryGetValue(key, out var oldRaw))
            {
                preview.Rows.Add(ErrorRow("Settings", line, key, "That setting is not stored in this shop."));
                continue;
            }

            var type = SettingType(oldRaw);
            if (map.ContainsKey("type") && Field("type").Length > 0
                && !string.Equals(Field("type"), type, StringComparison.OrdinalIgnoreCase))
            {
                preview.Rows.Add(ErrorRow("Settings", line, key, "The Type column is read-only; the setting's JSON type cannot change."));
                continue;
            }

            var text = Field("value");
            if (text.Length == 0)
            {
                preview.Rows.Add(StateRow("Settings", line, key, BulkUpdateState.Same));
                continue;
            }

            try
            {
                var typed = ParseSetting(oldRaw, text);
                candidates.Add(new SettingCandidate
                {
                    Key = key,
                    Line = line,
                    OldRaw = oldRaw,
                    NewText = text,
                    Snapshot = Field("snapshot"),
                    TypedValue = typed
                });
            }
            catch (Exception exception) when (exception is ValidationException or JsonException)
            {
                preview.Rows.Add(ErrorRow("Settings", line, key, exception.Message));
            }
        }

        if (candidates.Count == 0)
        {
            return;
        }

        if (preview.IncludeSettings && !map.ContainsKey("snapshot"))
        {
            preview.Rows.Add(ErrorRow("Settings", 1, "headings", "The Settings Snapshot column is required for a bulk update."));
            return;
        }

        _settings.Reload();
        var values = candidates.ToDictionary(candidate => candidate.Key,
            candidate => (object?)candidate.TypedValue, StringComparer.Ordinal);
        var preparation = _settings.PrepareBulk(values);
        if (!preparation.Ok || preparation.Value is null)
        {
            preview.Rows.Add(ErrorRow("Settings", 0, "settings", preparation.Error ?? "The settings are invalid."));
            return;
        }

        foreach (var candidate in candidates)
        {
            var newRaw = preparation.Value[candidate.Key];
            if (JsonEquivalent(candidate.OldRaw, newRaw))
            {
                preview.Rows.Add(StateRow("Settings", candidate.Line, candidate.Key, BulkUpdateState.Same));
                continue;
            }

            if (!string.Equals(candidate.Snapshot, SettingsFingerprint(candidate.Key, candidate.OldRaw), StringComparison.OrdinalIgnoreCase))
            {
                preview.Rows.Add(ErrorRow("Settings", candidate.Line, candidate.Key,
                    "This setting changed after the download (or its Snapshot was edited). Download a fresh workbook."));
                continue;
            }

            var row = StateRow("Settings", candidate.Line, candidate.Key, BulkUpdateState.Ready);
            row.Changes.Add(new BulkFieldChange
            {
                Field = "Value",
                Before = SettingDisplay(candidate.OldRaw),
                After = SettingDisplay(newRaw)
            });
            preview.Rows.Add(row);
            preview.SettingValues[candidate.Key] = candidate.TypedValue;
            preview.Pending.Add(new PendingBulkRecord
            {
                Sheet = "Settings line " + candidate.Line.ToString(CultureInfo.InvariantCulture),
                Table = "settings",
                KeyColumn = "key",
                KeyValue = candidate.Key,
                Module = "Settings",
                HasUpdatedAt = true,
                Fields =
                {
                    new PendingBulkField { SqlName = "value", OldValue = candidate.OldRaw, NewValue = newRaw }
                },
                Before = { ["value"] = candidate.OldRaw },
                After = { ["value"] = newRaw }
            });
        }
    }

    private static object ParseSetting(string oldRaw, string text)
    {
        using var oldDocument = JsonDocument.Parse(oldRaw);
        var kind = oldDocument.RootElement.ValueKind;
        if (text == ClearToken && kind != JsonValueKind.String)
        {
            throw new ValidationException("__CLEAR__ is only for a text setting; arrays, objects, numbers and booleans need an explicit value.");
        }

        switch (kind)
        {
            case JsonValueKind.String:
                return text == ClearToken ? string.Empty : text;
            case JsonValueKind.Number:
                if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                {
                    throw new ValidationException("The setting must keep its number type.");
                }

                return number;
            case JsonValueKind.True:
            case JsonValueKind.False:
                return ParseBoolean(text);
            case JsonValueKind.Array:
            case JsonValueKind.Object:
            {
                using var newDocument = JsonDocument.Parse(text);
                if (newDocument.RootElement.ValueKind != kind)
                {
                    throw new ValidationException("The setting must keep its JSON array/object type.");
                }

                return newDocument.RootElement.Clone();
            }
            default:
                throw new ValidationException("This JSON setting type cannot be edited in the workbook.");
        }
    }

    // ============================== Apply safety ==============================

    private bool CanImport(User? user) =>
        user is not null && user.CanManage && _permissions.Can(user, Modules.Import);

    private static void VerifyPlan(PendingBulkRecord record)
    {
        if (record.Table == "settings")
        {
            if (record.KeyColumn != "key" || record.Fields.Count != 1 || record.Fields[0].SqlName != "value")
            {
                throw new ValidationException("The internal settings apply plan is not valid.");
            }

            return;
        }

        // Table, key and every SQL column must exactly match the static schema; nothing is accepted
        // from the workbook as a SQL identifier.
        var schema = Schemas.FirstOrDefault(candidate => candidate.Table == record.Table && candidate.KeyColumn == record.KeyColumn)
            ?? throw new ValidationException("The internal worksheet apply plan is not valid.");
        foreach (var field in record.Fields)
        {
            if (!schema.Columns.Any(column => column.Editable && column.SqlName == field.SqlName))
            {
                throw new ValidationException("The internal apply plan contains a protected or unknown column.");
            }
        }
    }

    private void VerifyCurrentRow(SqliteConnection connection, PendingBulkRecord record)
    {
        if (record.Table != "settings")
        {
            var schema = Schemas.Single(candidate =>
                candidate.Table == record.Table && candidate.KeyColumn == record.KeyColumn);
            var current = ReadRows(connection, schema, record.KeyValue).SingleOrDefault()
                ?? throw new ValidationException(record.Sheet + " no longer exists. Preview a fresh workbook.");

            // A changed field that was NOT in the plan can make the approved change unsafe (for
            // example Cost rising above the proposed Retail). Compare the whole exported row and
            // then rerun the record guard, rather than checking only the edited columns.
            if (record.Snapshot.Length == 0
                || !string.Equals(record.Snapshot, Fingerprint(schema, current), StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException(record.Sheet + " changed after its preview. Nothing was applied; preview a fresh workbook.");
            }

            var changed = record.Fields.Select(field => field.SqlName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var validation = ValidateRecord(connection, schema, record.RowAfter, changed);
            if (validation is not null)
            {
                throw new ValidationException(record.Sheet + ": " + validation);
            }

            return;
        }

        var exists = Convert.ToInt64(_db.Scalar(connection,
            "SELECT COUNT(1) FROM settings WHERE key = @key;", new { key = record.KeyValue }) ?? 0L);
        if (exists != 1)
        {
            throw new ValidationException(record.Sheet + " no longer exists. Preview a fresh workbook.");
        }

        var currentValue = _db.Scalar(connection,
            "SELECT value FROM settings WHERE key = @key;", new { key = record.KeyValue });
        if (!SqlValuesEqual(currentValue, record.Fields[0].OldValue))
        {
            throw new ValidationException(record.Sheet + " changed after its preview. Nothing was applied; preview a fresh workbook.");
        }
    }

    // ============================== Workbook construction ==============================

    private static XlsxSheet HeaderSheet(string name, IEnumerable<string> headings)
    {
        var sheet = new XlsxSheet { Name = name };
        sheet.Rows.Add(headings.ToList());
        return sheet;
    }

    private static XlsxSheet InstructionsSheet(bool isTemplate)
    {
        var sheet = new XlsxSheet { Name = "Instructions" };
        var lines = isTemplate
            ? new[]
            {
                "Area11 native Excel template - fill Products, Customers and/or Suppliers, then use the Import tab.",
                "The first row of each data sheet is its headings. Do not rename a heading you need.",
                "Rates are rupees. Opening stock needs Batch no and Expiry (yyyy-mm-dd). Duplicates are skipped.",
                "CSV UTF-8 files are still accepted for new-record imports. Nothing is written before the preview."
            }
            : new[]
            {
                "Area11 native .xlsx workbook. Use Download your data, edit, then preview on the Bulk update tab.",
                "Empty cell = no change. To clear a nullable text, reference or date value, type __CLEAR__.",
                "Do not edit Id or Snapshot. A stale or edited snapshot refuses that record instead of overwriting newer work.",
                "Stock quantities, account balances, opening balances, record links, security, secrets, counters and photos are protected.",
                "Settings change only with the separate Settings tick. Strings, numbers, booleans, arrays and objects keep their types.",
                "New records belong on the Import tab. Every accepted change has a before → after preview and one atomic, audited apply.",
                "CSV remains for report/black-box exports and new-record imports; this workbook needs no Excel installation or package."
            };
        foreach (var line in lines)
        {
            sheet.Rows.Add(new List<string> { line });
        }

        return sheet;
    }

    private XlsxSheet ExportSheet(SheetSchema schema, IReadOnlyList<Dictionary<string, object?>> rows)
    {
        var sheet = new XlsxSheet { Name = schema.Name };
        var headings = new List<string> { "Id" };
        headings.AddRange(schema.Columns.Select(column => column.Heading));
        headings.Add("Snapshot (do not edit)");
        sheet.Rows.Add(headings);

        foreach (var row in rows)
        {
            var output = new List<string> { KeyText(row[KeyAlias]) };
            output.AddRange(schema.Columns.Select(column => Display(column, row[column.SqlName])));
            output.Add(Fingerprint(schema, row));
            sheet.Rows.Add(output);
        }

        return sheet;
    }

    private XlsxSheet ExportSettings(IReadOnlyDictionary<string, string> settings)
    {
        var sheet = new XlsxSheet { Name = "Settings" };
        sheet.Rows.Add(new List<string> { "Key", "Value", "Type", "Snapshot (do not edit)" });
        foreach (var key in SettingsService.KnownKeys.Where(key => !IsLockedSetting(key)).OrderBy(key => key, StringComparer.Ordinal))
        {
            if (!settings.TryGetValue(key, out var raw))
            {
                continue;
            }

            sheet.Rows.Add(new List<string> { key, SettingDisplay(raw), SettingType(raw), SettingsFingerprint(key, raw) });
        }

        return sheet;
    }

    private List<Dictionary<string, object?>> ReadRows(
        SqliteConnection connection, SheetSchema schema, object? key = null)
    {
        var result = new List<Dictionary<string, object?>>();
        var rows = key is null
            ? _db.Query<dynamic>(connection, schema.SelectSql)
            : _db.Query<dynamic>(connection, schema.SelectOneSql, new { key });
        foreach (var dynamicRow in rows)
        {
            var source = (IDictionary<string, object>)dynamicRow;
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                [KeyAlias] = source[KeyAlias]
            };
            for (var index = 0; index < schema.Columns.Count; index++)
            {
                row[schema.Columns[index].SqlName] = source["C" + index.ToString(CultureInfo.InvariantCulture)];
            }

            result.Add(row);
        }

        return result;
    }

    private Dictionary<string, string> ReadSettings(SqliteConnection connection) =>
        _db.Query<WorkbookSettingRow>(connection, "SELECT key, value FROM settings ORDER BY key;")
            .ToDictionary(row => row.Key, row => row.Value, StringComparer.Ordinal);

    // ============================== Values and fingerprints ==============================

    private static ParsedCell ParseCell(ColumnSchema column, string source)
    {
        var text = source.Trim();
        if (text == ClearToken)
        {
            if (!column.Clearable)
            {
                throw new ValidationException("__CLEAR__ is not allowed for this column; type an explicit value instead.");
            }

            return new ParsedCell(null, string.Empty);
        }

        if (column.SqlName == "base_unit")
        {
            text = NormalizeUnitName(text);
        }

        switch (column.Kind)
        {
            case CellKind.Text:
            case CellKind.RequiredText:
            {
                if (column.Kind == CellKind.RequiredText && text.Length == 0)
                {
                    throw new ValidationException("A value is required.");
                }

                if (text.Length > column.MaxLength)
                {
                    throw new ValidationException("Keep the text under " + column.MaxLength + " characters.");
                }

                return new ParsedCell(text, text);
            }
            case CellKind.Money:
            {
                if (!Money.TryParse(text, out var rupees) || (!column.AllowNegative && rupees < 0))
                {
                    throw new ValidationException(column.AllowNegative
                        ? "Enter a valid rupee amount."
                        : "Enter a non-negative rupee amount.");
                }

                var paisa = Money.ToPaisa(rupees);
                return new ParsedCell(paisa, Money.Edit(paisa));
            }
            case CellKind.Quantity:
            {
                if (!Qty.TryParse(text, out var quantity) || (!column.AllowNegative && quantity < 0) || !double.IsFinite(quantity))
                {
                    throw new ValidationException(column.AllowNegative
                        ? "Enter a finite quantity."
                        : "Enter a finite, non-negative quantity.");
                }

                return new ParsedCell(quantity, Qty.Edit(quantity));
            }
            case CellKind.Boolean:
            {
                var value = ParseBoolean(text);
                return new ParsedCell(value ? 1L : 0L, value ? "yes" : "no");
            }
            case CellKind.Date:
            {
                if (!Expiry.TryNormalize(text, out var expiry, out _))
                {
                    throw new ValidationException("Use a real date such as 2027-06-30.");
                }

                return new ParsedCell(expiry, expiry);
            }
            case CellKind.Reference:
            case CellKind.Integer:
            case CellKind.NullableInteger:
            {
                if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                    || value < column.Minimum || value > column.Maximum)
                {
                    throw new ValidationException("Enter a whole number from " + column.Minimum + " to " + column.Maximum + ".");
                }

                return new ParsedCell(value, value.ToString(CultureInfo.InvariantCulture));
            }
            default:
                throw new ValidationException("This column type is not supported.");
        }
    }

    private static bool ParseBoolean(string text) => text.Trim().ToLowerInvariant() switch
    {
        "1" or "true" or "yes" or "y" => true,
        "0" or "false" or "no" or "n" => false,
        _ => throw new ValidationException("Use yes/no (or true/false) so the boolean type is preserved.")
    };

    private static string Display(ColumnSchema column, object? value)
    {
        if (value is null or DBNull)
        {
            return string.Empty;
        }

        return column.Kind switch
        {
            CellKind.Money => Money.Edit(Convert.ToInt64(value, CultureInfo.InvariantCulture)),
            CellKind.Quantity => Qty.Edit(Convert.ToDouble(value, CultureInfo.InvariantCulture)),
            CellKind.Boolean => Convert.ToInt64(value, CultureInfo.InvariantCulture) != 0 ? "yes" : "no",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    private static bool SameValue(ColumnSchema column, object? left, object? right) =>
        string.Equals(Canonical(column, left), Canonical(column, right), StringComparison.Ordinal);

    private static string Canonical(ColumnSchema column, object? value)
    {
        if (value is null or DBNull)
        {
            return "<null>";
        }

        return column.Kind switch
        {
            CellKind.Money or CellKind.Integer or CellKind.NullableInteger or CellKind.Reference =>
                Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
            CellKind.Quantity => Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture),
            CellKind.Boolean => Convert.ToInt64(value, CultureInfo.InvariantCulture) != 0 ? "1" : "0",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    private static bool SqlValuesEqual(object? left, object? right)
    {
        if (left is null or DBNull)
        {
            return right is null or DBNull;
        }

        if (right is null or DBNull)
        {
            return false;
        }

        if (left is string || right is string)
        {
            return string.Equals(Convert.ToString(left, CultureInfo.InvariantCulture),
                Convert.ToString(right, CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }

        if (left is double or float || right is double or float)
        {
            return Convert.ToDouble(left, CultureInfo.InvariantCulture)
                .Equals(Convert.ToDouble(right, CultureInfo.InvariantCulture));
        }

        return Convert.ToInt64(left, CultureInfo.InvariantCulture)
            == Convert.ToInt64(right, CultureInfo.InvariantCulture);
    }

    private static string Fingerprint(SheetSchema schema, IReadOnlyDictionary<string, object?> row)
    {
        var builder = new StringBuilder(schema.Name).Append('|').Append(KeyText(row[KeyAlias]));
        foreach (var column in schema.Columns)
        {
            var value = Canonical(column, row[column.SqlName]);
            builder.Append('|').Append(column.SqlName).Append('=').Append(value.Length).Append(':').Append(value);
        }

        return Hash(builder.ToString());
    }

    private static string SettingsFingerprint(string key, string raw) => Hash("Settings|" + key + "|" + raw);

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static string SettingDisplay(string raw)
    {
        using var document = JsonDocument.Parse(raw);
        return document.RootElement.ValueKind == JsonValueKind.String
            ? document.RootElement.GetString() ?? string.Empty
            : document.RootElement.GetRawText();
    }

    private static string SettingType(string raw)
    {
        using var document = JsonDocument.Parse(raw);
        return document.RootElement.ValueKind.ToString().ToLowerInvariant();
    }

    private static bool JsonEquivalent(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            return true;
        }

        using var leftDocument = JsonDocument.Parse(left);
        using var rightDocument = JsonDocument.Parse(right);
        return string.Equals(JsonSerializer.Serialize(leftDocument.RootElement),
            JsonSerializer.Serialize(rightDocument.RootElement), StringComparison.Ordinal);
    }

    private static bool IsLockedSetting(string key) =>
        key.StartsWith("security.", StringComparison.Ordinal)
        || key.StartsWith("bill.next", StringComparison.Ordinal)
        || key.StartsWith("sync.", StringComparison.Ordinal)
        || key.StartsWith("hardware.", StringComparison.Ordinal)
        || key is "brand.logoDataUrl" or "stock.allowNegative" or "stock.warnNegative"
            or "backup.lastRunAt" or "backup.lastFile" or "session.handoverAt" or "session.handoverBy"
            or "menu.collapsedGroups";

    private static string KeyText(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

    private static string RecordKey(IReadOnlyDictionary<string, object?> row, int fallback)
    {
        foreach (var column in new[] { "name", "batch_no", "key" })
        {
            if (row.TryGetValue(column, out var value) && value is not null)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback.ToString(CultureInfo.InvariantCulture);
            }
        }

        return fallback.ToString(CultureInfo.InvariantCulture);
    }

    private static BulkUpdateRow StateRow(string sheet, int line, string key, BulkUpdateState state) => new()
    {
        Sheet = sheet,
        Line = line,
        Key = key,
        State = state
    };

    private static BulkUpdateRow ErrorRow(string sheet, int line, string key, string message)
    {
        var row = StateRow(sheet, line, key, BulkUpdateState.Error);
        row.Message = message;
        return row;
    }

    // ============================== Static sheet schemas ==============================

    private enum CellKind
    {
        Text,
        RequiredText,
        Money,
        Quantity,
        Boolean,
        Date,
        Reference,
        Integer,
        NullableInteger
    }

    private sealed class ColumnSchema
    {
        public string Heading { get; init; } = string.Empty;
        public string SqlName { get; init; } = string.Empty;
        public string? SelectExpression { get; init; }
        public CellKind Kind { get; init; }
        public bool Editable { get; init; } = true;
        public bool Clearable { get; init; }
        public bool AllowNegative { get; init; }
        public int MaxLength { get; init; } = 500;
        public long Minimum { get; init; }
        public long Maximum { get; init; } = int.MaxValue;
    }

    private sealed class SheetSchema
    {
        public string Name { get; init; } = string.Empty;
        public string Table { get; init; } = string.Empty;
        public string KeyColumn { get; init; } = "id";
        public string From { get; init; } = string.Empty;
        public string KeyExpression { get; init; } = "id";
        public string Module { get; init; } = "Import";
        public bool HasUpdatedAt { get; init; }
        public IReadOnlyList<ColumnSchema> Columns { get; init; } = Array.Empty<ColumnSchema>();

        private string SelectedColumns => $"{KeyExpression} AS {KeyAlias}, "
            + string.Join(", ", Columns.Select((column, index) =>
                (column.SelectExpression ?? column.SqlName) + " AS C" + index.ToString(CultureInfo.InvariantCulture)));

        public string SelectSql => $"SELECT {SelectedColumns} FROM {From} ORDER BY {KeyExpression};";

        public string SelectOneSql => $"SELECT {SelectedColumns} FROM {From} WHERE {KeyExpression} = @key;";
    }

    private sealed class ParsedCell
    {
        public ParsedCell(object? value, string display)
        {
            Value = value;
            Display = display;
        }

        public object? Value { get; }
        public string Display { get; }
    }

    private sealed class SettingCandidate
    {
        public string Key { get; set; } = string.Empty;
        public int Line { get; set; }
        public string OldRaw { get; set; } = string.Empty;
        public string NewText { get; set; } = string.Empty;
        public string Snapshot { get; set; } = string.Empty;
        public object TypedValue { get; set; } = string.Empty;
    }

    private sealed class WorkbookSettingRow
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    private static ColumnSchema Col(
        string heading,
        string sql,
        CellKind kind = CellKind.Text,
        bool editable = true,
        bool clearable = false,
        string? select = null,
        int maxLength = 500,
        long minimum = 0,
        long maximum = int.MaxValue,
        bool allowNegative = false) => new()
    {
        Heading = heading,
        SqlName = sql,
        Kind = kind,
        Editable = editable,
        Clearable = clearable,
        AllowNegative = allowNegative,
        SelectExpression = select,
        MaxLength = maxLength,
        Minimum = minimum,
        Maximum = maximum
    };

    private static readonly IReadOnlyList<SheetSchema> Schemas = new[]
    {
        new SheetSchema
        {
            Name = "Products", Table = "products", From = "products p", KeyExpression = "p.id", HasUpdatedAt = true,
            Columns = new[]
            {
                Col("Name", "name", CellKind.RequiredText, maxLength: 200),
                Col("Generic", "generic", clearable: true, select: "p.generic"),
                Col("Brand", "brand", clearable: true, select: "p.brand"),
                Col("Barcode", "barcode", clearable: true, select: "p.barcode", maxLength: 100),
                Col("Category Id", "category_id", CellKind.Reference, clearable: true, select: "p.category_id", minimum: 1),
                Col("Company Id", "company_id", CellKind.Reference, clearable: true, select: "p.company_id", minimum: 1),
                Col("Rack", "rack_no", clearable: true, select: "p.rack_no", maxLength: 100),
                Col("Room", "room", clearable: true, select: "p.room", maxLength: 100),
                Col("Pack", "pack_size_label", clearable: true, select: "p.pack_size_label", maxLength: 100),
                Col("Base unit", "base_unit", CellKind.RequiredText, select: "p.base_unit", maxLength: 40),
                Col("Box strips", "box_strips", CellKind.Integer, select: "p.box_strips"),
                Col("Strip tablets", "strip_tablets", CellKind.Integer, select: "p.strip_tablets"),
                Col("Cost", "cost_paisa", CellKind.Money, select: "p.cost_paisa"),
                Col("Retail", "retail_paisa", CellKind.Money, select: "p.retail_paisa"),
                Col("VIP price", "vip_paisa", CellKind.Money, select: "p.vip_paisa"),
                Col("Doctor price", "doctor_paisa", CellKind.Money, select: "p.doctor_paisa"),
                Col("Reorder level", "reorder_level", CellKind.Quantity, select: "p.reorder_level"),
                Col("Track expiry", "track_expiry", CellKind.Boolean, select: "p.track_expiry"),
                Col("Stock on shelf", "stock_base", CellKind.Quantity, editable: false, allowNegative: true,
                    select: "(SELECT CAST(IFNULL(SUM(b.qty_base), 0) AS REAL) FROM batches b WHERE b.product_id = p.id AND b.active = 1)"),
                Col("Active", "active", CellKind.Boolean, editable: false, select: "p.active")
            }
        },
        new SheetSchema
        {
            Name = "Customers", Table = "customers", From = "customers",
            Columns = new[]
            {
                Col("Name", "name", CellKind.RequiredText, maxLength: 200),
                Col("Phone", "phone", clearable: true, maxLength: 60),
                Col("Category", "category", CellKind.RequiredText, maxLength: 20),
                Col("Credit limit", "credit_limit_paisa", CellKind.Money),
                Col("Notes", "notes", clearable: true, maxLength: 2000),
                Col("Opening udhaar", "opening_balance_paisa", CellKind.Money, editable: false),
                Col("Udhaar now", "balance_paisa", CellKind.Money, editable: false, allowNegative: true),
                Col("Loyalty points", "loyalty_points", CellKind.Integer, editable: false),
                Col("Stars", "stars", CellKind.Integer, editable: false),
                Col("Active", "active", CellKind.Boolean, editable: false)
            }
        },
        new SheetSchema
        {
            Name = "Suppliers", Table = "suppliers", From = "suppliers",
            Columns = new[]
            {
                Col("Name", "name", CellKind.RequiredText, maxLength: 200),
                Col("Phone", "phone", clearable: true, maxLength: 60),
                Col("Agency", "agency", clearable: true, maxLength: 200),
                Col("Address", "address", clearable: true, maxLength: 2000),
                Col("Notes", "notes", clearable: true, maxLength: 2000),
                Col("Opening payable", "opening_balance_paisa", CellKind.Money, editable: false),
                Col("Payable now", "balance_paisa", CellKind.Money, editable: false, allowNegative: true),
                Col("Active", "active", CellKind.Boolean, editable: false)
            }
        },
        new SheetSchema
        {
            Name = "Categories", Table = "categories", From = "categories", Module = "Inventory",
            Columns = new[]
            {
                Col("Name", "name", CellKind.RequiredText, maxLength: 120),
                Col("Parent Id", "parent_id", CellKind.Reference, clearable: true, minimum: 1),
                Col("Sort order", "sort_order", CellKind.Integer, maximum: 1_000_000),
                Col("Active", "active", CellKind.Boolean, editable: false)
            }
        },
        new SheetSchema
        {
            Name = "Companies", Table = "companies", From = "companies", Module = "Inventory",
            Columns = new[]
            {
                Col("Name", "name", CellKind.RequiredText, maxLength: 120),
                Col("Phone", "phone", clearable: true, maxLength: 60),
                Col("Notes", "notes", clearable: true, maxLength: 2000),
                Col("Rep name", "rep_name", clearable: true, maxLength: 200),
                Col("Rep phone", "rep_phone", clearable: true, maxLength: 60),
                Col("Visit day", "visit_day", CellKind.NullableInteger, clearable: true, maximum: 6),
                Col("Sort order", "sort_order", CellKind.Integer, maximum: 1_000_000),
                Col("Active", "active", CellKind.Boolean, editable: false)
            }
        },
        new SheetSchema
        {
            Name = "Batches", Table = "batches", From = "batches",
            Columns = new[]
            {
                Col("Product Id", "product_id", CellKind.Reference, editable: false, minimum: 1),
                Col("Batch no", "batch_no", CellKind.RequiredText, maxLength: 100),
                Col("Expiry", "expiry_date", CellKind.Date, clearable: true),
                Col("Quantity", "qty_base", CellKind.Quantity, editable: false, allowNegative: true),
                Col("Cost", "cost_paisa", CellKind.Money),
                Col("Retail", "retail_paisa", CellKind.Money),
                Col("VIP price", "vip_paisa", CellKind.Money),
                Col("Doctor price", "doctor_paisa", CellKind.Money),
                Col("Sample", "is_sample", CellKind.Boolean),
                Col("Supplier Id", "supplier_id", CellKind.Reference, clearable: true, minimum: 1),
                Col("Purchase Id", "purchase_id", CellKind.NullableInteger, editable: false),
                Col("Active", "active", CellKind.Boolean, editable: false)
            }
        }
    };
}
