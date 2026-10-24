using System.Globalization;
using System.Text.Json.Serialization;

namespace Area11.Core.Models;

/// <summary>
/// The names the sync page uses for the tables (F-48). Why a switch and not the raw table name:
/// the page is read by the owner, and "sale_items" is not a word he uses - "bill lines" is.
/// </summary>
public static class SyncTables
{
    /// <summary>The tables that travel, in the order they are written (parents before children).</summary>
    public static readonly string[] Order =
    {
        "categories", "companies", "suppliers", "customers", "products", "batches",
        "sales", "sale_items", "purchases", "purchase_items", "payments", "expenses"
    };

    /// <summary>Tables whose rows are cut to the last 90 days (Spec 25.5).</summary>
    public static readonly string[] Recent =
    {
        "sales", "sale_items", "purchases", "purchase_items", "payments", "expenses"
    };

    public static string Label(string table) => table switch
    {
        "products" => "Medicines",
        "batches" => "Batches",
        "customers" => "Customers",
        "suppliers" => "Suppliers",
        "categories" => "Categories",
        "companies" => "Companies",
        "sales" => "Bills (90 days)",
        "sale_items" => "Bill lines",
        "purchases" => "Purchases (90 days)",
        "purchase_items" => "Purchase lines",
        "payments" => "Payments (90 days)",
        "expenses" => "Expenses (90 days)",
        _ => table
    };

    public static bool IsKnown(string table) => Array.IndexOf(Order, table) >= 0;

    public static bool IsRecent(string table) => Array.IndexOf(Recent, table) >= 0;
}

/// <summary>
/// One line of the comparison: how many rows the cloud holds and how many this computer holds.
///
/// Why the page shows both numbers before they are used: "bring and apply here" replaces rows, and the
/// owner must be able to see that the cloud copy is older or emptier before he presses it (D-66).
/// </summary>
public sealed class SyncTableCount
{
    public SyncTableCount(string table, int cloud, int here)
    {
        Table = table;
        Cloud = cloud;
        Here = here;
    }

    public string Table { get; }

    public string Label => SyncTables.Label(Table);

    /// <summary>Rows in the cloud; -1 when the cloud has not been read yet.</summary>
    public int Cloud { get; }

    public int Here { get; }

    public int Difference => Cloud < 0 ? 0 : Cloud - Here;

    public string CloudLabel => Cloud < 0
        ? "-"
        : Cloud.ToString(CultureInfo.InvariantCulture);

    public string HereLabel => Here.ToString(CultureInfo.InvariantCulture);

    /// <summary>"same", "+12" or "-3" - the sign is the whole message.</summary>
    public string DifferenceLabel => Cloud < 0
        ? "-"
        : Difference switch
        {
            0 => "same",
            > 0 => "+" + Difference.ToString(CultureInfo.InvariantCulture),
            _ => Difference.ToString(CultureInfo.InvariantCulture)
        };

    public bool IsDifferent => Cloud >= 0 && Difference != 0;
}

/// <summary>
/// What leaves the shop: one JSON document with the shop's own name, its id, and the rows of the tables
/// listed in <see cref="SyncTables.Order"/> (F-48, Spec 25.5).
///
/// Why one document and not a row per table: the cloud table has a single row per shop
/// (`area11_sync`), so a push is one write that either lands or does not - half a shop in the cloud
/// would be worse than none (Spec 25.2).
/// </summary>
public sealed class SyncEnvelope
{
    public const int CurrentFormat = 1;

    [JsonPropertyName("format")]
    public int Format { get; set; } = CurrentFormat;

    [JsonPropertyName("shopId")]
    public string ShopId { get; set; } = string.Empty;

    [JsonPropertyName("shopName")]
    public string ShopName { get; set; } = string.Empty;

    /// <summary>When this document was built, in the shop's clock (Spec 5.1.3).</summary>
    [JsonPropertyName("madeAt")]
    public string MadeAt { get; set; } = string.Empty;

    /// <summary>The Area11 version that built it - so a cloud copy can be read years later.</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("tables")]
    public Dictionary<string, List<Dictionary<string, object?>>> Tables { get; set; } = new();

    /// <summary>The size of the JSON, filled by the service after it is written (never sent).</summary>
    [JsonIgnore]
    public long SizeBytes { get; set; }

    [JsonIgnore]
    public int RowCount => Tables.Sum(pair => pair.Value.Count);

    [JsonIgnore]
    public string SizeLabel => SizeBytes >= 1024 * 1024
        ? (SizeBytes / 1024.0 / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB"
        : Math.Max(1, SizeBytes / 1024).ToString(CultureInfo.InvariantCulture) + " KB";

    public int CountOf(string table) =>
        Tables.TryGetValue(table, out var rows) ? rows.Count : 0;

    /// <summary>Adds one row, keeping the plan's order so the JSON is diffable between two pushes.</summary>
    public void Add(string table, Dictionary<string, object?> row)
    {
        if (!Tables.TryGetValue(table, out var rows))
        {
            rows = new List<Dictionary<string, object?>>();
            Tables[table] = rows;
        }

        rows.Add(row);
    }
}

/// <summary>
/// One row of the cloud table `area11_sync` (Spec 25.2): `shop_id`, `shop_name`, `sent_at`, `version`,
/// `payload`.
///
/// Why the push sends this and not the document itself: the table's columns are snake_case and
/// `version` is an **int**, while the Area11 document inside `payload` is the shop's own camelCase JSON.
/// Posting the document directly asked PostgREST for a `format` column the table does not have, so a
/// real push was refused with "Could not find the 'format' column" (repaired in v0.26.0).
///
/// `version` here is the **document format** (1 today), not the program's version: the table was made
/// for a schema number, and the program version travels inside the document where it can be any text -
/// "0.26.0" cannot be written into an int column, and a cloud copy must still be readable years later.
/// </summary>
public sealed class SyncRow
{
    [JsonPropertyName("shop_id")]
    public string ShopId { get; set; } = string.Empty;

    [JsonPropertyName("shop_name")]
    public string ShopName { get; set; } = string.Empty;

    /// <summary>Local shop time with its offset - a `timestamptz` Supabase can place on any clock.</summary>
    [JsonPropertyName("sent_at")]
    public string SentAt { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public int Version { get; set; } = SyncEnvelope.CurrentFormat;

    [JsonPropertyName("payload")]
    public SyncEnvelope Payload { get; set; } = new();
}

/// <summary>What a push actually sent, so the page can say a number instead of "done".</summary>
public sealed class SyncRun
{
    public string At { get; set; } = string.Empty;

    public string ShopId { get; set; } = string.Empty;

    public int Rows { get; set; }

    public int Tables { get; set; }

    public long SizeBytes { get; set; }

    public string SummaryLabel =>
        Rows.ToString(CultureInfo.InvariantCulture) + " rows from "
        + Tables.ToString(CultureInfo.InvariantCulture) + " tables ("
        + (SizeBytes >= 1024 * 1024
            ? (SizeBytes / 1024.0 / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB"
            : Math.Max(1, SizeBytes / 1024).ToString(CultureInfo.InvariantCulture) + " KB")
        + ") at " + At + ".";
}

/// <summary>
/// What the cloud holds for this shop, read without writing anything (Spec 25.4 - "see from cloud").
///
/// It also carries the parsed document: "bring and apply here" then writes exactly the rows the owner
/// was looking at, not a second download that could be different.
/// </summary>
public sealed class SyncPreview
{
    public string ShopId { get; set; } = string.Empty;

    public string ShopName { get; set; } = string.Empty;

    public string SentAt { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public List<SyncTableCount> Tables { get; } = new();

    /// <summary>The document that was read; never null once the preview succeeded.</summary>
    [JsonIgnore]
    public SyncEnvelope? Envelope { get; set; }

    /// <summary>Rows on this computer when the preview was made - the other half of the comparison.</summary>
    public int HereRows { get; set; }

    public string HereLabel => HereRows.ToString(CultureInfo.InvariantCulture) + " rows here";

    public int Rows => Envelope?.RowCount ?? 0;

    public bool IsEmpty => Rows == 0;

    public bool HasDifferences => Tables.Any(table => table.IsDifferent);

    public int DifferentTables => Tables.Count(table => table.IsDifferent);

    public string SentLabel => SentAt.Length == 0 ? "the cloud did not say when" : "sent " + SentAt;

    public string SummaryLabel => IsEmpty
        ? "The cloud has no rows for " + ShopId + " yet."
        : Rows.ToString(CultureInfo.InvariantCulture) + " rows in the cloud (" + SentLabel + ", version "
          + (Version.Length == 0 ? "?" : Version) + "), " + HereLabel + ".";
}

/// <summary>What "bring and apply here" wrote, and the backup that was taken first (Spec 25.4).</summary>
public sealed class SyncApplyResult
{
    public int Tables { get; set; }

    public int Rows { get; set; }

    public int Skipped { get; set; }

    public string BackupName { get; set; } = string.Empty;

    public string At { get; set; } = string.Empty;

    public string SummaryLabel =>
        "Brought " + Rows.ToString(CultureInfo.InvariantCulture) + " rows from "
        + Tables.ToString(CultureInfo.InvariantCulture) + " tables"
        + (Skipped > 0 ? " (" + Skipped.ToString(CultureInfo.InvariantCulture) + " skipped)" : string.Empty)
        + ". Backup first: " + (BackupName.Length == 0 ? "none" : BackupName) + ".";
}

/// <summary>
/// The page as it opens: is the cloud set up, what is this computer holding, and when did it last
/// speak to the cloud (F-48).
/// </summary>
public sealed class SyncStatus
{
    public bool Configured { get; set; }

    public string Url { get; set; } = string.Empty;

    public string Table { get; set; } = "area11_sync";

    public string ShopId { get; set; } = string.Empty;

    /// <summary>The key as it may be shown: first and last four characters, the rest stars.</summary>
    public string KeyLabel { get; set; } = string.Empty;

    public string LastSync { get; set; } = string.Empty;

    public string StatusLabel { get; set; } = string.Empty;

    public List<SyncTableCount> Counts { get; } = new();

    public int Rows => Counts.Sum(count => count.Here);

    public string RowsLabel => Rows.ToString(CultureInfo.InvariantCulture) + " rows on this computer";

    public string LastSyncLabel => LastSync.Length == 0
        ? "never sent from this computer"
        : "last sync " + LastSync;

    /// <summary>The owner's tick in Settings -> Cloud sync (`sync.enabled`).</summary>
    public bool Enabled { get; set; }

    public string ConfigLabel => Configured
        ? "Cloud sync is set up"
        : Enabled
            ? "Cloud sync is not set up yet"
            : "Cloud sync is switched off";
}

/// <summary>
/// The answer of the wire, kept free of HTTP types so the service can be tested without a network.
/// <see cref="Status"/> is the HTTP code, or 0 when the request never left the computer.
/// </summary>
public sealed class SyncTransportResult
{
    public bool Ok { get; init; }

    public int Status { get; init; }

    public string Body { get; init; } = string.Empty;

    /// <summary>Set when the request never reached the cloud (no internet, bad address, timeout).</summary>
    public string? Error { get; init; }

    public static SyncTransportResult Success(int status, string body) =>
        new() { Ok = true, Status = status, Body = body };

    public static SyncTransportResult Fail(int status, string body, string? error = null) =>
        new() { Ok = false, Status = status, Body = body, Error = error };
}
