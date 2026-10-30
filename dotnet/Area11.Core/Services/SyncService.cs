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
    public const string EnabledKey = "sync.enabled";
    public const string StatusKey = "sync.status";
    public const string LastSyncKey = "sync.lastSyncAt";

    /// <summary>The days of bills, purchases, payments and expenses that travel (Spec 25.5).</summary>
    public const int RecentDays = 90;

    /// <summary>Above this, the page warns before a push: mobile internet is not free.</summary>
    public const long BigPayloadBytes = 8L * 1024 * 1024;

    /// <summary>How SQLite stores the shop's clock, and the exact shape <see cref="Clock.Now"/> writes.</summary>
    private const string ShopStampFormat = "yyyy-MM-dd HH:mm:ss";

    /// <summary>The shop's clock has no milliseconds; the cloud column is a timestamptz.</summary>
    private const string CloudStampFormat = "yyyy-MM-ddTHH:mm:sszzz";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    /// <summary>
    /// The starting set: six lists whole and each financial document for 90 days, with recent child
    /// lines read in bulk (no one-query-per-bill slowdown). A parent included later only as an older
    /// dependency brings the same complete lines through the single-parent queries below.
    /// </summary>
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
        ["provisional_returns"] = "SELECT * FROM provisional_returns WHERE date >= @since ORDER BY id",
        ["provisional_items"] = "SELECT * FROM provisional_items WHERE provisional_id IN (SELECT id FROM provisional_returns WHERE date >= @since) ORDER BY id",
        ["provisional_return_allocations"] = "SELECT allocation.* FROM provisional_return_allocations allocation JOIN provisional_items item ON item.id = allocation.provisional_item_id WHERE item.provisional_id IN (SELECT id FROM provisional_returns WHERE date >= @since) ORDER BY allocation.id",
        ["payments"] = "SELECT * FROM payments WHERE date >= @since ORDER BY id",
        ["expenses"] = "SELECT * FROM expenses WHERE date >= @since ORDER BY id"
    };

    /// <summary>A referenced parent may be older than 90 days; never cut its foreign key off.</summary>
    private static readonly Dictionary<string, string> ParentById = new(StringComparer.Ordinal)
    {
        ["sales"] = "SELECT * FROM sales WHERE id = @id",
        ["sale_items"] = "SELECT * FROM sale_items WHERE id = @id",
        ["purchases"] = "SELECT * FROM purchases WHERE id = @id",
        ["provisional_returns"] = "SELECT * FROM provisional_returns WHERE id = @id",
        ["provisional_items"] = "SELECT * FROM provisional_items WHERE id = @id"
    };

    private const string SaleLinesForSale =
        "SELECT * FROM sale_items WHERE sale_id = @parent ORDER BY id";

    private const string PurchaseLinesForPurchase =
        "SELECT * FROM purchase_items WHERE purchase_id = @parent ORDER BY id";

    private const string ProvisionalLinesForReturn =
        "SELECT * FROM provisional_items WHERE provisional_id = @parent ORDER BY id";

    private const string AllocationsForReturn = @"
SELECT allocation.*
FROM provisional_return_allocations allocation
JOIN provisional_items item ON item.id = allocation.provisional_item_id
WHERE item.provisional_id = @parent
ORDER BY allocation.id;";

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

    /// <summary>The owner's tick in Settings -> Cloud sync (`sync.enabled`). Off means "do not talk to the cloud".</summary>
    public bool Enabled => _settings.GetBool(EnabledKey);

    /// <summary>Is a key saved? The key itself is never shown, so the screen needs this one fact.</summary>
    public bool HasKey => Key.Length > 0;

    /// <summary>
    /// Ready to talk: the switch is on **and** the four details are filled in. Asking both questions
    /// matters because the two failures need different sentences - "switched off" is a choice the owner
    /// made, "not set up yet" is a form he has not finished.
    /// </summary>
    public bool IsConfigured =>
        Enabled && Url.Length > 0 && Key.Length > 0 && Table.Length > 0 && ShopId.Length > 0;

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
            StatusLabel = StatusText(_settings.GetString(StatusKey)),
            Enabled = Enabled
        };

        try
        {
            // Counts use the same dependency-complete selection a push would use, including older
            // parents. A separate COUNT query per table previously made the page and document disagree.
            var envelope = ReadLocal();

            foreach (var table in SyncTables.Order)
            {
                status.Counts.Add(new SyncTableCount(table, -1, envelope.CountOf(table)));
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

    /// <summary>The six lists whole, recent money, and every older parent/line those rows reference.</summary>
    public ServiceResult<SyncEnvelope> Build(User? user)
    {
        if (!CanSync(user))
        {
            return ServiceResult<SyncEnvelope>.Fail(
                "Only the owner can send the shop's data to the cloud.");
        }

        if (!Enabled)
        {
            return ServiceResult<SyncEnvelope>.Fail(
                "Cloud sync is switched off. Turn it on in Settings -> Cloud sync, then press Push again.");
        }

        if (!IsConfigured)
        {
            return ServiceResult<SyncEnvelope>.Fail(
                "Fill the cloud address, the key and the shop id in Settings -> Cloud sync first.");
        }

        try
        {
            var envelope = ReadLocal();

            // The size the owner sees is the size of the ROW that will be posted (the document plus the
            // five columns around it), because that is what his internet pays for.
            envelope.SizeBytes = Encoding.UTF8.GetByteCount(ToJson(RowFor(envelope)));

            return ServiceResult<SyncEnvelope>.Success(envelope);
        }
        catch (Exception exception)
        {
            _log.Error("The shop's data could not be prepared for the cloud.", exception);

            return ServiceResult<SyncEnvelope>.Fail(
                "The shop's data could not be prepared for the cloud. " + exception.Message);
        }
    }

    /// <summary>
    /// Read one local document for Build, Status and Preview. The 90-day cut applies to financial
    /// headers, not to their references: a recent khata collection can belong to a sale from last year,
    /// and a no-bill return can be reconciled against an old bill. Those parents and their complete
    /// lines must travel too, or applying the payment would fail its foreign key (or lose the link).
    /// </summary>
    private SyncEnvelope ReadLocal()
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
            foreach (var row in ReadRows(connection, Selects[table], since))
            {
                envelope.Add(table, row);
            }
        }

        CompleteReferences(connection, envelope);

        // Dependencies are discovered after the recent rows. Restore id order inside every table so
        // two pushes of unchanged data remain diffable regardless of the discovery path.
        foreach (var table in SyncTables.Order)
        {
            if (envelope.Tables.TryGetValue(table, out var rows))
            {
                rows.Sort((left, right) => IdOf(left, "id").GetValueOrDefault()
                    .CompareTo(IdOf(right, "id").GetValueOrDefault()));
            }
        }

        return envelope;
    }

    /// <summary>
    /// Complete the small graph around the 90-day starting set. This is a fixed-point walk because an
    /// allocation can pull in an old sale line, which pulls in its sale, which then pulls in all the
    /// other lines on that bill. Every row comes from this local database; no cloud names reach SQL.
    /// </summary>
    private static void CompleteReferences(SqliteConnection connection, SyncEnvelope envelope)
    {
        var ids = new Dictionary<string, HashSet<long>>(StringComparer.Ordinal);

        foreach (var table in SyncTables.Order)
        {
            ids[table] = new HashSet<long>(RowsOf(envelope, table)
                .Select(row => IdOf(row, "id") ?? 0)
                .Where(id => id > 0));
        }

        // The starting SELECTs already read these recent parents' lines in bulk. Only an older parent
        // discovered below needs its own child query; a busy shop does not pay one query per recent bill.
        var completedSales = new HashSet<long>(ids["sales"]);
        var completedPurchases = new HashSet<long>(ids["purchases"]);
        var completedProvisionals = new HashSet<long>(ids["provisional_returns"]);
        var completedAllocations = new HashSet<long>(ids["provisional_returns"]);

        while (true)
        {
            var added = false;

            foreach (var payment in RowsOf(envelope, "payments"))
            {
                added |= EnsureParent(connection, envelope, ids, payment, "sale_id", "sales");
                added |= EnsureParent(connection, envelope, ids, payment, "purchase_id", "purchases");
                added |= EnsureParent(connection, envelope, ids, payment,
                    "provisional_return_id", "provisional_returns");
            }

            foreach (var provisional in RowsOf(envelope, "provisional_returns"))
            {
                added |= EnsureParent(connection, envelope, ids, provisional, "linked_sale_id", "sales");
            }

            foreach (var item in RowsOf(envelope, "provisional_items"))
            {
                added |= EnsureParent(connection, envelope, ids, item,
                    "provisional_id", "provisional_returns");
            }

            foreach (var allocation in RowsOf(envelope, "provisional_return_allocations"))
            {
                added |= EnsureParent(connection, envelope, ids, allocation,
                    "provisional_item_id", "provisional_items");
                added |= EnsureParent(connection, envelope, ids, allocation,
                    "sale_item_id", "sale_items");
            }

            foreach (var line in RowsOf(envelope, "sale_items"))
            {
                added |= EnsureParent(connection, envelope, ids, line, "sale_id", "sales");
            }

            // Every selected document brings ALL its lines, not only lines that happen to be recent.
            added |= EnsureChildren(connection, envelope, ids, "sales", "sale_items",
                SaleLinesForSale, completedSales);
            added |= EnsureChildren(connection, envelope, ids, "purchases", "purchase_items",
                PurchaseLinesForPurchase, completedPurchases);
            added |= EnsureChildren(connection, envelope, ids, "provisional_returns", "provisional_items",
                ProvisionalLinesForReturn, completedProvisionals);
            added |= EnsureChildren(connection, envelope, ids, "provisional_returns",
                "provisional_return_allocations", AllocationsForReturn, completedAllocations);

            if (!added)
            {
                break;
            }
        }
    }

    private static bool EnsureParent(
        SqliteConnection connection,
        SyncEnvelope envelope,
        Dictionary<string, HashSet<long>> ids,
        Dictionary<string, object?> row,
        string field,
        string parentTable)
    {
        var id = IdOf(row, field);

        return id.HasValue && EnsureRow(connection, envelope, ids, parentTable, id.Value);
    }

    private static bool EnsureRow(
        SqliteConnection connection,
        SyncEnvelope envelope,
        Dictionary<string, HashSet<long>> ids,
        string table,
        long id)
    {
        if (ids[table].Contains(id))
        {
            return false;
        }

        var rows = ReadRowsWithParameter(connection, ParentById[table], "@id", id);

        if (rows.Count != 1)
        {
            // A local foreign key should make this impossible. Stop rather than send a document that
            // looks successful but has silently lost the referenced financial event.
            throw new InvalidOperationException(
                "A local row references missing " + table + " id " + id.ToString(CultureInfo.InvariantCulture)
                + ". Repair the local database before pushing.");
        }

        return AddRow(envelope, ids, table, rows[0]);
    }

    private static bool EnsureChildren(
        SqliteConnection connection,
        SyncEnvelope envelope,
        Dictionary<string, HashSet<long>> ids,
        string parentTable,
        string childTable,
        string sql,
        HashSet<long> completed)
    {
        var added = false;

        foreach (var parent in RowsOf(envelope, parentTable))
        {
            var id = IdOf(parent, "id");

            if (!id.HasValue || !completed.Add(id.Value))
            {
                continue;
            }

            foreach (var child in ReadRowsWithParameter(connection, sql, "@parent", id.Value))
            {
                added |= AddRow(envelope, ids, childTable, child);
            }
        }

        return added;
    }

    private static bool AddRow(
        SyncEnvelope envelope,
        Dictionary<string, HashSet<long>> ids,
        string table,
        Dictionary<string, object?> row)
    {
        var id = IdOf(row, "id");

        if (!id.HasValue || id.Value <= 0)
        {
            throw new InvalidOperationException("A local " + table + " row has no positive integer id.");
        }

        if (!ids[table].Add(id.Value))
        {
            return false;
        }

        envelope.Add(table, row);
        return true;
    }

    private static List<Dictionary<string, object?>> RowsOf(SyncEnvelope envelope, string table) =>
        envelope.Tables.TryGetValue(table, out var rows) ? rows : new List<Dictionary<string, object?>>();

    private static long? IdOf(Dictionary<string, object?> row, string field)
    {
        if (!row.TryGetValue(field, out var value) || value is null || value == DBNull.Value)
        {
            return null;
        }

        return Convert.ToInt64(value, CultureInfo.InvariantCulture);
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

        // The table's row, not the document: snake_case columns, `version` as an int, and the document
        // itself inside `payload` (Spec 25.2). A raw envelope asked for a `format` column and was refused.
        var json = ToJson(RowFor(envelope));
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

    /// <summary>
    /// The cloud table's row for one document: `shop_id`, `shop_name`, `sent_at`, `version`, `payload`.
    ///
    /// The stamp is the shop's own time with its offset, written in <see cref="CloudStampFormat"/> so a
    /// `timestamptz` column keeps the moment the document was made rather than the moment it arrived
    /// (Spec 5.1.3). `version` is the document format as an int; the program's version is text and
    /// travels inside the document.
    /// </summary>
    public static SyncRow RowFor(SyncEnvelope envelope) => new()
    {
        ShopId = envelope.ShopId,
        ShopName = envelope.ShopName,
        SentAt = CloudStamp(envelope.MadeAt),
        Version = envelope.Format,
        Payload = envelope
    };

    /// <summary>
    /// "2026-10-26 14:05:00" (how SQLite stores the shop's clock) -> "2026-10-26T14:05:00+05:00".
    /// A text that is not a stamp falls back to now: a push must never be refused for a clock's format.
    /// </summary>
    private static string CloudStamp(string madeAt)
    {
        var moment = DateTime.TryParseExact(madeAt, ShopStampFormat, CultureInfo.InvariantCulture,
                          DateTimeStyles.AssumeLocal, out var parsed)
            ? parsed
            : TimeSource.Now;

        return moment.ToString(CloudStampFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>"See from cloud": reads and compares, writes nothing - not the database, not the cloud.</summary>
    public ServiceResult<SyncPreview> Preview(User? user)
    {
        if (!CanSync(user))
        {
            return ServiceResult<SyncPreview>.Fail(
                "Only the owner can read the cloud copy of the shop.");
        }

        if (!Enabled)
        {
            return ServiceResult<SyncPreview>.Fail(
                "Cloud sync is switched off. Turn it on in Settings -> Cloud sync, then try again.");
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

        if (!Enabled)
        {
            return ServiceResult<SyncApplyResult>.Fail(
                "Cloud sync is switched off. Turn it on in Settings -> Cloud sync before bringing data here.");
        }

        if (!IsConfigured)
        {
            return ServiceResult<SyncApplyResult>.Fail(
                "Fill the cloud address, the key and the shop id in Settings -> Cloud sync first. Nothing was written.");
        }

        if (envelope is null)
        {
            return ServiceResult<SyncApplyResult>.Fail(
                "Read the cloud copy first - there is nothing to bring.");
        }

        // Apply is a Core boundary, not merely a button. A caller cannot bypass ReadEnvelope and feed
        // a future document straight into the writer after the preview has already validated it.
        if (envelope.Format <= 0 || envelope.Format > SyncEnvelope.CurrentFormat)
        {
            return ServiceResult<SyncApplyResult>.Fail(
                "The cloud copy was written by a different Area11 (format " + envelope.Format
                + "). Update this computer first. Nothing was written.");
        }

        if (envelope.RowCount == 0)
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
    /// the table must be in SyncTables.Order, and every column must exist in that table on this computer.
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
        // The id is the key everything else hangs from; a row without it cannot be placed. A NULL id
        // is not a real key either: SQLite would auto-number an INTEGER PRIMARY KEY and quietly create
        // a different local row, so skip it just like a missing id.
        if (!row.TryGetValue("id", out var id) || id is null || id == DBNull.Value || !allowed.Contains("id"))
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
                SentAt = PrettyStamp(TextOf(row, "sent_at")),

                // The app version is text and lives in the document; the table's own `version` is the
                // format as an int and would read as a meaningless "1" on the page (v0.26.0).
                Version = envelope.Value.Version,
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

    /// <summary>
    /// "2026-10-26T09:05:00+00:00" (what Supabase returns) -> "2026-10-26 17:05:00" in the shop's own
    /// time, so the owner can hold it against the clock on the wall. Text that cannot be read is shown
    /// exactly as it came, never hidden: the moment decides whether the cloud copy is worth bringing back.
    /// </summary>
    private static string PrettyStamp(string raw)
    {
        if (raw.Trim().Length == 0)
        {
            return string.Empty;
        }

        return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var moment)
            ? moment.ToLocalTime().ToString(ShopStampFormat, CultureInfo.InvariantCulture)
            : raw;
    }

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
            var envelope = ReadLocal();
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var table in SyncTables.Order)
            {
                counts[table] = envelope.CountOf(table);
            }

            return counts;
        }
        catch (Exception exception)
        {
            _log.Error("The shop's rows could not be counted for the cloud preview.", exception);
            return null;
        }
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

        return ReadRows(command);
    }

    private static List<Dictionary<string, object?>> ReadRowsWithParameter(
        SqliteConnection connection,
        string sql,
        string name,
        long value)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue(name, value);
        return ReadRows(command);
    }

    private static List<Dictionary<string, object?>> ReadRows(SqliteCommand command)
    {
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

    /// <summary>
    /// The one way this class turns an object into text. Generic on purpose: a push serialises the
    /// table's **row** (<see cref="SyncRow"/>) while the size warning and the tests still serialise the
    /// document, and both must use the same options.
    /// </summary>
    private static string ToJson<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

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
