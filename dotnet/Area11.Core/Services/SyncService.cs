using System.Globalization;
using System.Text;
using System.Text.Json;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>
/// Cloud sync (F-48, Spec 25): the shop's own data copied to a Supabase mirror so the owner can look at
/// the numbers from home. The local database is always the truth; the cloud is a copy of it.
///
/// Why the page has three buttons and not one: a push is safe for the shop (it only reads), but
/// "bring and apply here" writes cloud rows over local ones, and the one rule that must never break is
/// that this cannot happen by accident. So it is: see the cloud (nothing is written) -> tick the box ->
/// apply (automatic backup first, then one transaction) (D-66).
///
/// What never happens here: a local row is never deleted. A shop that sold something today and pulls an
/// older cloud copy keeps today's bills; the pull only adds and replaces the rows it was given.
/// </summary>
public sealed class SyncService
{
    // ---------- the settings the owner fills in on Settings -> Cloud sync (Spec 25.3) ----------
    public const string UrlKey = "sync.url";
    public const string KeyKey = "sync.key";
    public const string TableKey = "sync.table";
    public const string ShopIdKey = "sync.shopId";
    public const string StatusKey = "sync.status";
    public const string LastSyncKey = "sync.lastSyncAt";

    /// <summary>The days of bills, purchases, payments and expenses that travel (Spec 25.5).</summary>
    public const int RecentDays = 90;

    /// <summary>Above this, the page warns before a push: mobile internet is not free.</summary>
    public const long BigPayloadBytes = 8L * 1024 * 1024;

    /// <summary>The shop's clock has no milliseconds; the cloud column is a timestamptz.</summary>
    private const string CloudStampFormat = "yyyy-MM-ddTHH:mm:sszzz";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    /// <summary>One SELECT per table, written out so the repo checker prepares each one against the schema.</summary>
    private static readonly Dictionary<string, string> Selects = new(StringComparer.Ordinal)
    {
        ["categories"] = "SELECT * FROM categories ORDER BY id",
        ["companies"] = "SELECT * FROM companies ORDER BY id",
        ["suppliers"] = "SELECT * FROM suppliers ORDER BY id",
        ["customers"] = "SELECT * FROM customers ORDER BY id",
        ["products"] = "SELECT * FROM products ORDER BY id",
        ["batches"] = "SELECT * FROM batches ORDER BY id",
        ["sales"] = "SELECT * FROM sales WHERE date >= @since ORDER BY id",
        ["sale_items"] = "SELECT * FROM sale_items WHERE sale_id IN (SELECT id FROM sales WHERE date >= @since) ORDER BY id",
        ["purchases"] = "SELECT * FROM purchases WHERE date >= @since ORDER BY id",
        ["purchase_items"] = "SELECT * FROM purchase_items WHERE purchase_id IN (SELECT id FROM purchases WHERE date >= @since) ORDER BY id",
        ["payments"] = "SELECT * FROM payments WHERE date >= @since ORDER BY id",
        ["expenses"] = "SELECT * FROM expenses WHERE date >= @since ORDER BY id"
    };

    /// <summary>The same cut, but counting instead of reading - for the page and the preview.</summary>
    private static readonly Dictionary<string, string> Counts = new(StringComparer.Ordinal)
    {
        ["categories"] = "SELECT COUNT(1) FROM categories",
        ["companies"] = "SELECT COUNT(1) FROM companies",
        ["suppliers"] = "SELECT COUNT(1) FROM suppliers",
        ["customers"] = "SELECT COUNT(1) FROM customers",
        ["products"] = "SELECT COUNT(1) FROM products",
        ["batches"] = "SELECT COUNT(1) FROM batches",
        ["sales"] = "SELECT COUNT(1) FROM sales WHERE date >= @since",
        ["sale_items"] = "SELECT COUNT(1) FROM sale_items WHERE sale_id IN (SELECT id FROM sales WHERE date >= @since)",
        ["purchases"] = "SELECT COUNT(1) FROM purchases WHERE date >= @since",
        ["purchase_items"] = "SELECT COUNT(1) FROM purchase_items WHERE purchase_id IN (SELECT id FROM purchases WHERE date >= @since)",
        ["payments"] = "SELECT COUNT(1) FROM payments WHERE date >= @since",
        ["expenses"] = "SELECT COUNT(1) FROM expenses WHERE date >= @since"
    };

    private readonly Database _db;
    private readonly SettingsService _settings;
    private readonly AuditService _audit;
    private readonly PermissionService _permissions;
    private readonly LogService _log;
    private readonly BackupService _backup;
    private readonly ICloudTransport _cloud;

    public SyncService(
        Database db,
        SettingsService settings,
        AuditService audit,
        PermissionService permissions,
        LogService log,
        BackupService backup,
        ICloudTransport cloud)
    {
        _db = db;
        _settings = settings;
        _audit = audit;
        _permissions = permissions;
        _log = log;
        _backup = backup;
        _cloud = cloud;
    }

    // ============================== what the page asks ==============================

    /// <summary>Cloud sync moves the whole shop's data, so only a person the owner ticked may do it.</summary>
    public bool CanSync(User? user) => _permissions.Can(user, Modules.Sync);

    public string ShopId => _settings.GetString(ShopIdKey).Trim();

    public string ShopName
    {
        get
        {
            // The same three steps ReceiptService uses for the name on a bill, so the cloud mirror is
            // named exactly like the paper the shop hands out.
            var name = FirstNonEmpty(
                _settings.GetString("store.name"),
                _settings.GetString("brand.appName"),
                "Area11");

            return name;
        }
    }

    public string Url => _settings.GetString(UrlKey).Trim();

    public string Table => FirstNonEmpty(_settings.GetString(TableKey), "area11_sync");

    /// <summary>Kept private to this class: the key is a password to the whole shop's mirror.</summary>
    private string Key => _settings.GetString(KeyKey).Trim();

    public bool IsConfigured =>
        Url.Length > 0 && Key.Length > 0 && Table.Length > 0 && ShopId.Length > 0;

    /// <summary>"eyJh...9f2c" - enough to recognise a key, not enough to use it.</summary>
    public string KeyLabel
    {
        get
        {
            var key = Key;

            if (key.Length == 0)
            {
                return "no key";
            }

            return key.Length <= 12
                ? new string('*', key.Length)
                : key[..4] + new string('*', 8) + key[^4..];
        }
    }

    /// <summary>What the page shows on opening: setup, the shop id, and how many rows are here.</summary>
    public ServiceResult<SyncStatus> Status(User? user)
    {
        if (!CanSync(user))
        {
            return ServiceResult<SyncStatus>.Fail(
                "Only the owner can use cloud sync. Ask him to tick it in Settings -> Staff.");
        }

        var status = new SyncStatus
        {
            Configured = IsConfigured,
            Url = Url,
            Table = Table,
            ShopId = ShopId,
            KeyLabel = KeyLabel,
            LastSync = _settings.GetString(LastSyncKey).Trim(),
            StatusLabel = StatusText(_settings.GetString(StatusKey))
        };

        try
        {
            var since = Clock.DateDaysAgo(RecentDays);

            using var connection = _db.Open();

            // Every table is counted the same way the push selects it, so "0 of 12 tables" on the page
            // means the same thing as an empty section in the document.
            foreach (var table in SyncTables.Order)
            {
                status.Counts.Add(new SyncTableCount(table, -1, CountRows(connection, table, since)));
            }

            return ServiceResult<SyncStatus>.Success(status);
        }
        catch (Exception exception)
        {
            _log.Error("The cloud sync page could not count the shop's rows.", exception);

            return ServiceResult<SyncStatus>.Fail(
                "The page could not count the shop's rows. " + exception.Message);
        }
    }

    /// <summary>The document that would be sent: the six lists whole, and the last 90 days of money.</summary>
    public ServiceResult<SyncEnvelope> Build(User? user)
    {
        if (!CanSync(user))
        {
            return ServiceResult<SyncEnvelope>.Fail(
                "Only the owner can send the shop's data to the cloud.");
        }

        if (!IsConfigured)
        {
            return ServiceResult<SyncEnvelope>.Fail(
                "Fill the cloud address, the key and the shop id in Settings -> Cloud sync first.");
        }

        try
        {
            var since = Clock.DateDaysAgo(RecentDays);

            var envelope = new SyncEnvelope
            {
                Format = SyncEnvelope.CurrentFormat,
                ShopId = ShopId,
                ShopName = ShopName,
                MadeAt = Clock.Now(),
                Version = BuildInfo.Version
            };

            using var connection = _db.Open();

            foreach (var table in SyncTables.Order)
            {
                var rows = ReadRows(connection, Selects[table], since);

                if (rows.Count == 0)
                {
                    // An empty table is left out instead of sent as "": the document stays small, and the
                    // apply never touches a table it was not given.
                    continue;
                }

                foreach (var row in rows)
                {
                    envelope.Add(table, row);
                }
            }

            envelope.SizeBytes = Encoding.UTF8.GetByteCount(ToJson(envelope));

            return ServiceResult<SyncEnvelope>.Success(envelope);
        }
        catch (Exception exception)
        {
            _log.Error("The shop's data could not be prepared for the cloud.", exception);

            return ServiceResult<SyncEnvelope>.Fail(
                "The shop's data could not be prepared for the cloud. " + exception.Message);
        }
    }

    /// <summary>Send the document. Reads the shop and writes the cloud - never the other way round.</summary>
    public ServiceResult<SyncRun> Push(User? user)
    {
        var built = Build(user);

        if (!built.Ok || built.Value is null)
        {
            return ServiceResult<SyncRun>.Fail(built.Error ?? "The data could not be prepared.");
        }

        var envelope = built.Value;

        if (envelope.RowCount == 0)
        {
            return ServiceResult<SyncRun>.Fail(
                "There is nothing to send yet - this shop has no medicines or bills.");
        }

        var json = ToJson(envelope);
        var answer = _cloud.Send(Url, Key, Table, json);

        if (!answer.Ok)
        {
            Remember("error");

            var message = WireError(answer);

            _log.Error("Cloud push failed: " + message, new InvalidOperationException(message));

            return ServiceResult<SyncRun>.Fail(message);
        }

        var run = new SyncRun
        {
            At = Clock.Now(),
            ShopId = envelope.ShopId,
            Rows = envelope.RowCount,
            Tables = envelope.Tables.Count,
            SizeBytes = envelope.SizeBytes
        };

        Remember("pushed");
        _audit.RecordStandalone(user, Modules.Sync, "sync_push", details: run.SummaryLabel);

        return ServiceResult<SyncRun>.Success(run);
    }

    /// <summary>"See from cloud": reads and compares, writes nothing - not the database, not the cloud.</summary>
    public ServiceResult<SyncPreview> Preview(User? user)
    {
        if (!CanSync(user))
        {
            return ServiceResult<SyncPreview>.Fail(
                "Only the owner can read the cloud copy of the shop.");
        }

        if (!IsConfigured)
        {
            return ServiceResult<SyncPreview>.Fail(
                "Fill the cloud address, the key and the shop id in Settings -> Cloud sync first.");
        }

        var answer = _cloud.Fetch(Url, Key, Table, ShopId);

        if (!answer.Ok)
        {
            return ServiceResult<SyncPreview>.Fail(WireError(answer));
        }

        var here = HereCounts();

        if (here is null)
        {
            return ServiceResult<SyncPreview>.Fail("This computer's rows could not be counted.");
        }

        return ReadPreview(answer.Body, here);
    }

    /// <summary>
    /// "Bring and apply here": the cloud rows are written over the local ones, after a backup and only
    /// when the owner ticked the box. One transaction: a failure leaves the shop exactly as it was.
    /// </summary>
    public ServiceResult<SyncApplyResult> Apply(User? user, SyncEnvelope? envelope, bool confirmed)
    {
        if (!CanSync(user))
        {
            return ServiceResult<SyncApplyResult>.Fail(
                "Only the owner can bring the cloud copy onto this computer.");
        }

        if (envelope is null || envelope.RowCount == 0)
        {
            return ServiceResult<SyncApplyResult>.Fail(
                "Read the cloud copy first - there is nothing to bring.");
        }

        if (!confirmed)
        {
            return ServiceResult<SyncApplyResult>.Fail(
                "Tick the box that says the cloud rows replace the rows on this computer.");
        }

        if (!string.Equals(envelope.ShopId, ShopId, StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<SyncApplyResult>.Fail(
                "That copy belongs to '" + envelope.ShopId + "', but this computer is '" + ShopId
                + "'. Nothing was written.");
        }

        // A backup is not optional: the cloud copy is older than the till, so the only way back is a copy
        // taken one second before the write (Spec 25.4).
        var backup = _backup.Create(user, includePhotos: false);

        if (!backup.Ok || backup.Value is null)
        {
            return ServiceResult<SyncApplyResult>.Fail(
                "A backup is needed before the cloud data can be written, and it failed: "
                + (backup.Error ?? "unknown reason") + " Nothing was written.");
        }

        var written = Write(envelope);

        if (!written.Ok || written.Value is null)
        {
            return written;
        }

        var result = written.Value;
        result.BackupName = backup.Value.FileName;
        result.At = Clock.Now();

        Remember("applied");
        _audit.RecordStandalone(user, Modules.Sync, "sync_apply", details: result.SummaryLabel);

        return ServiceResult<SyncApplyResult>.Success(result);
    }

    // ============================== the write ==============================

    /// <summary>
    /// Writes the document into the shop's database. Every row is filtered twice before it reaches SQL:
    /// the table must be one of the twelve, and every column must exist in that table on this computer.
    /// Why: the document comes from outside, and a column name is text that ends up in a statement.
    /// </summary>
    private ServiceResult<SyncApplyResult> Write(SyncEnvelope envelope)
    {
        var result = new SyncApplyResult();

        try
        {
            using var connection = _db.Open();
            using var transaction = connection.BeginTransaction();

            var columns = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            foreach (var table in SyncTables.Order)
            {
                if (!envelope.Tables.TryGetValue(table, out var rows) || rows.Count == 0)
                {
                    continue;
                }

                if (!columns.TryGetValue(table, out var allowed))
                {
                    allowed = ColumnsOf(connection, transaction, table);
                    columns[table] = allowed;
                }

                var wrote = 0;

                foreach (var row in rows)
                {
                    if (!Upsert(connection, transaction, table, allowed, row))
                    {
                        result.Skipped++;
                        continue;
                    }

                    wrote++;
                    result.Rows++;
                }

                if (wrote > 0)
                {
                    result.Tables++;
                }
            }

            // Unknown tables in the document are ignored, not written: an older or newer Area11 may
            // know a table this computer does not.
            foreach (var pair in envelope.Tables)
            {
                if (!SyncTables.IsKnown(pair.Key))
                {
                    result.Skipped += pair.Value.Count;
                }
            }

            transaction.Commit();

            return ServiceResult<SyncApplyResult>.Success(result);
        }
        catch (Exception exception)
        {
            _log.Error("The cloud copy could not be written onto this computer.", exception);

            return ServiceResult<SyncApplyResult>.Fail(
                "Nothing was written: the cloud copy did not fit this database ("
                + exception.Message + ").");
        }
    }

    private static bool Upsert(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string table,
        HashSet<string> allowed,
        Dictionary<string, object?> row)
    {
        // The id is the key everything else hangs from; a row without it cannot be placed.
        if (!row.ContainsKey("id"))
        {
            return false;
        }

        var names = row.Keys
            .Where(name => allowed.Contains(name))
            .OrderBy(name => name == "id" ? 0 : 1)
            .ThenBy(name => name, StringComparer.Ordinal)
            .ToList();

        if (names.Count == 0)
        {
            return false;
        }

        var quoted = names.Select(Quote).ToList();
        var parameters = names.Select((_, index) => "@p" + index.ToString(CultureInfo.InvariantCulture));
        var updates = names
            .Where(name => name != "id")
            .Select(name => Quote(name) + " = excluded." + Quote(name))
            .ToList();

        // Interpolated on purpose: the table and the columns only become known at this moment, and every
        // name here is already an identifier this database itself reported (Quote + PRAGMA), never text
        // out of the cloud document.
        var conflict = updates.Count == 0
            ? "DO NOTHING"
            : "DO UPDATE SET " + string.Join(", ", updates);

        var sql = $"INSERT INTO {Quote(table)} ({string.Join(", ", quoted)}) "
                  + $"VALUES ({string.Join(", ", parameters)}) ON CONFLICT(id) {conflict}";

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;

        for (var index = 0; index < names.Count; index++)
        {
            command.Parameters.AddWithValue("@p" + index.ToString(CultureInfo.InvariantCulture),
                row[names[index]] ?? DBNull.Value);
        }

        return command.ExecuteNonQuery() > 0;
    }

    private static HashSet<string> ColumnsOf(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string table)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "PRAGMA table_info(" + Quote(table) + ");";

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    /// <summary>SQLite's own way of writing a name: double quotes, and a quote inside is doubled.</summary>
    private static string Quote(string name) => "\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    // ============================== reading the cloud ==============================

    private ServiceResult<SyncPreview> ReadPreview(string body, Dictionary<string, int> here)
    {
        if (body.Trim().Length == 0)
        {
            return ServiceResult<SyncPreview>.Fail(
                "The cloud answered with nothing. Push from the shop first.");
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() == 0)
            {
                return ServiceResult<SyncPreview>.Fail(
                    "No data for '" + ShopId + "' in the cloud yet - push from the shop first.");
            }

            var row = root.ValueKind == JsonValueKind.Array ? root[0] : root;

            if (row.ValueKind != JsonValueKind.Object)
            {
                return ServiceResult<SyncPreview>.Fail(
                    "The cloud answered in a shape Area11 does not know. Check the table name.");
            }

            var payload = row.TryGetProperty("payload", out var stored) ? stored : row;
            var envelope = ReadEnvelope(payload);

            if (!envelope.Ok || envelope.Value is null)
            {
                return ServiceResult<SyncPreview>.Fail(
                    envelope.Error ?? "The cloud row was not written by Area11.");
            }

            var preview = new SyncPreview
            {
                ShopId = envelope.Value.ShopId,
                ShopName = FirstNonEmpty(envelope.Value.ShopName, ShopName),
                SentAt = TextOf(row, "sent_at"),
                Version = FirstNonEmpty(envelope.Value.Version, TextOf(row, "version")),
                Envelope = envelope.Value,
                HereRows = here.Values.Sum()
            };

            foreach (var table in SyncTables.Order)
            {
                preview.Tables.Add(new SyncTableCount(
                    table,
                    envelope.Value.CountOf(table),
                    here.TryGetValue(table, out var count) ? count : 0));
            }

            return ServiceResult<SyncPreview>.Success(preview);
        }
        catch (JsonException exception)
        {
            return ServiceResult<SyncPreview>.Fail(
                "The cloud answer could not be read as JSON. Check that the table name is right. "
                + exception.Message);
        }
    }

    private static ServiceResult<SyncEnvelope> ReadEnvelope(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return ServiceResult<SyncEnvelope>.Fail("The cloud row has no Area11 document inside it.");
        }

        var format = payload.TryGetProperty("format", out var formatElement)
                     && formatElement.TryGetInt32(out var number)
            ? number
            : 0;

        if (format <= 0 || format > SyncEnvelope.CurrentFormat)
        {
            return ServiceResult<SyncEnvelope>.Fail(
                "The cloud copy was written by a different Area11 (format " + format
                + "). Update this computer first.");
        }

        var envelope = new SyncEnvelope
        {
            Format = format,
            ShopId = TextOf(payload, "shopId"),
            ShopName = TextOf(payload, "shopName"),
            MadeAt = TextOf(payload, "madeAt"),
            Version = TextOf(payload, "version")
        };

        if (!payload.TryGetProperty("tables", out var tables) || tables.ValueKind != JsonValueKind.Object)
        {
            return ServiceResult<SyncEnvelope>.Fail("The cloud copy has no tables in it.");
        }

        foreach (var table in tables.EnumerateObject())
        {
            if (table.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var element in table.Value.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var row = new Dictionary<string, object?>(StringComparer.Ordinal);

                foreach (var field in element.EnumerateObject())
                {
                    row[field.Name] = ReadValue(field.Value);
                }

                envelope.Add(table.Name, row);
            }
        }

        return ServiceResult<SyncEnvelope>.Success(envelope);
    }

    /// <summary>
    /// JSON -> the four types SQLite stores. Integers stay integers: money is integer paisa, and a paisa
    /// that comes back as 12345.0 would be a different number to every total in the shop (Spec 5.1.1).
    /// </summary>
    private static object? ReadValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.True => 1L,
        JsonValueKind.False => 0L,
        JsonValueKind.Number => element.TryGetInt64(out var whole) ? whole : element.GetDouble(),
        JsonValueKind.String => element.GetString(),
        _ => element.GetRawText()
    };

    private static string TextOf(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            _ => string.Empty
        };
    }

    // ============================== small helpers ==============================

    private Dictionary<string, int>? HereCounts()
    {
        try
        {
            var since = Clock.DateDaysAgo(RecentDays);
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);

            using var connection = _db.Open();

            foreach (var table in SyncTables.Order)
            {
                counts[table] = CountRows(connection, table, since);
            }

            return counts;
        }
        catch (Exception exception)
        {
            _log.Error("The shop's rows could not be counted for the cloud preview.", exception);
            return null;
        }
    }

    private static int CountRows(SqliteConnection connection, string table, string since)
    {
        using var command = connection.CreateCommand();
        command.CommandText = Counts[table];

        if (Counts[table].Contains("@since", StringComparison.Ordinal))
        {
            command.Parameters.AddWithValue("@since", since);
        }

        return Convert.ToInt32(command.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture);
    }

    private static List<Dictionary<string, object?>> ReadRows(
        SqliteConnection connection,
        string sql,
        string since)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        if (sql.Contains("@since", StringComparison.Ordinal))
        {
            command.Parameters.AddWithValue("@since", since);
        }

        var rows = new List<Dictionary<string, object?>>();

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);

            for (var index = 0; index < reader.FieldCount; index++)
            {
                row[reader.GetName(index)] = reader.IsDBNull(index) ? null : reader.GetValue(index);
            }

            rows.Add(row);
        }

        return rows;
    }

    private static string ToJson(SyncEnvelope envelope) =>
        JsonSerializer.Serialize(envelope, JsonOptions);

    /// <summary>Turns an HTTP answer into a sentence the owner can act on (Spec 25.6).</summary>
    private static string WireError(SyncTransportResult answer)
    {
        if (answer.Error is not null)
        {
            return "The cloud could not be reached. Check the internet, the address and the key. ("
                   + answer.Error + ")";
        }

        return answer.Status switch
        {
            0 => "The cloud could not be reached. Check the internet.",
            401 or 403 => "The key was refused by Supabase (" + answer.Status
                          + "). Copy the anon key again - a key that was rotated stops working.",
            404 => "The address or the table was not found (404). Check the URL and the table name '"
                   + "area11_sync'.",
            409 => "The cloud table is not the one Area11 needs (409). Run the SQL from Spec 25.2 again.",
            429 => "Supabase is asking Area11 to slow down (429). Try again in a minute.",
            _ => "The cloud answered " + answer.Status + ". "
                 + Shorten(answer.Body, 160)
        };
    }

    private static string Shorten(string? text, int length)
    {
        var clean = (text ?? string.Empty).Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal).Trim();

        return clean.Length <= length ? clean : clean[..length] + "...";
    }

    private void Remember(string status)
    {
        // Quiet writes on purpose: two settings of bookkeeping are not shop settings, and they must not
        // fill the black box. The one line that matters is the audit row written beside them.
        _settings.SetUiOnly(LastSyncKey, Clock.Now());
        _settings.SetUiOnly(StatusKey, status);
    }

    private static string StatusText(string raw) => raw.Trim() switch
    {
        "pushed" => "the last push succeeded",
        "applied" => "cloud data was brought here",
        "error" => "the last attempt failed - see the message below",
        "not_configured" => "not set up yet",
        _ => "never used on this computer"
    };

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}
