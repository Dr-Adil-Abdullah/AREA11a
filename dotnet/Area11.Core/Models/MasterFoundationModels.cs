namespace Area11.Core.Models;

/// <summary>Storage semantics for a built-in field. Quantity is REAL/double, never integer money.</summary>
public static class NativeFieldDataType
{
    public const string Text = "text";
    public const string Number = "number";
    public const string Quantity = "quantity";
    public const string Amount = "amount";
    public const string Date = "date";
    public const string DateTime = "datetime";
    public const string Select = "select";
    public const string Check = "check";
    public const string Photo = "photo";

    public static readonly string[] All =
    {
        Text, Number, Quantity, Amount, Date, DateTime, Select, Check, Photo
    };
}

/// <summary>The input control is metadata, not a second validation rule owned by a WPF page.</summary>
public static class NativeInputMethod
{
    public const string Text = "text";
    public const string Numeric = "numeric";
    public const string Calendar = "calendar";
    public const string MonthCalendar = "month_calendar";
    public const string Dropdown = "dropdown";
    public const string Checkbox = "checkbox";
    public const string Photo = "photo";
    public const string Readonly = "readonly";

    public static readonly string[] All =
    {
        Text, Numeric, Calendar, MonthCalendar, Dropdown, Checkbox, Photo, Readonly
    };
}

/// <summary>
/// A native form-field definition from migration 030. Owner-created custom fields remain in the
/// existing custom_fields/custom_values tables; the two kinds must not overwrite each other.
/// </summary>
public sealed class FormFieldDefinition
{
    public int Id { get; set; }

    public string FieldKey { get; set; } = string.Empty;

    public string SectionKey { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string DataType { get; set; } = NativeFieldDataType.Text;

    public string InputMethod { get; set; } = NativeInputMethod.Text;

    public string HelpText { get; set; } = string.Empty;

    /// <summary>The shipped safety recommendation, retained when the owner changes Required.</summary>
    public bool SafeRequired { get; set; }

    public bool Required { get; set; }

    public bool Critical { get; set; }

    public string? OverrideAcknowledgedBy { get; set; }

    public string? OverrideAcknowledgedAt { get; set; }

    public int SortOrder { get; set; }

    public string? CreatedAt { get; set; }

    public string? UpdatedAt { get; set; }

    /// <summary>Q1/owner override: this exact transition needs a warning and named confirmation.</summary>
    public bool RequiresOverrideAcknowledgement => Critical && SafeRequired && !Required;
}

public static class DraftStatus
{
    public const string Draft = "draft";
    public const string Posted = "posted";
    public const string Discarded = "discarded";

    public static readonly string[] All = { Draft, Posted, Discarded };
}

public static class DraftType
{
    public const string Sale = "sale";
    public const string Purchase = "purchase";

    public static readonly string[] All = { Sale, Purchase };

    public static bool IsKnown(string? type) =>
        !string.IsNullOrWhiteSpace(type) && All.Contains(type.Trim().ToLowerInvariant());

    public static string Clean(string? type) => (type ?? string.Empty).Trim().ToLowerInvariant();

    public static string ModuleFor(string? type) => Clean(type) switch
    {
        Purchase => "purchases",
        _ => "pos"
    };

    public static string LabelFor(string? type) => Clean(type) switch
    {
        Purchase => "Purchase",
        _ => "Sale"
    };
}

/// <summary>One row of the drafts list: who parked it, when, how many lines, for whom.</summary>
public sealed class DraftSummary
{
    public int Id { get; set; }

    public string DraftKey { get; set; } = string.Empty;

    public string DraftType { get; set; } = string.Empty;

    public string CreatedByName { get; set; } = string.Empty;

    public string? UpdatedAt { get; set; }

    public int Lines { get; set; }

    public string PartnerName { get; set; } = string.Empty;

    /// <summary>True when a previous post was interrupted after the pre-mark; see DraftService.</summary>
    public bool NeedsPostCheck { get; set; }

    public string TypeLabel => DraftType.LabelFor(DraftType);

    public string Headline =>
        DraftKey + " · " + Lines + (Lines == 1 ? " line" : " lines")
        + (PartnerName.Length > 0 ? " · " + PartnerName : string.Empty);
}

/// <summary>
/// Q1=A: a local, unposted draft. A row here is not a sale/purchase and never changes stock, money,
/// balances, numbering or reports. Final posting remains a separate validated transaction.
/// </summary>
public sealed class DocumentDraft
{
    public int Id { get; set; }

    public string DraftKey { get; set; } = string.Empty;

    public string DraftType { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = "{}";

    public string Status { get; set; } = DraftStatus.Draft;

    public int? CreatedById { get; set; }

    public string CreatedByName { get; set; } = string.Empty;

    public string? CreatedAt { get; set; }

    public string? UpdatedAt { get; set; }

    public int? PostedDocumentId { get; set; }

    public string? PostedAt { get; set; }

    public string? DiscardReason { get; set; }
}

/// <summary>Central salt/generic formula master; NameKey is normalized by the future write service.</summary>
public sealed class GenericFormula
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NameKey { get; set; } = string.Empty;

    public bool Active { get; set; } = true;

    public int SortOrder { get; set; }

    /// <summary>Current products explicitly linked to this salt (legacy typed text is not counted).</summary>
    public int ProductCount { get; set; }

    public string ActiveLabel => Active ? "Active" : "Put away";

    public string? CreatedAt { get; set; }

    public string? UpdatedAt { get; set; }
}

/// <summary>A managed brand, optionally owned by a company; the legacy products.brand text remains.</summary>
public sealed class BrandDefinition
{
    public int Id { get; set; }

    public int? CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NameKey { get; set; } = string.Empty;

    public bool Active { get; set; } = true;

    public int SortOrder { get; set; }

    /// <summary>Current products explicitly linked to this brand (legacy typed text is not counted).</summary>
    public int ProductCount { get; set; }

    /// <summary>Display name of the linked company, or null when the brand stands alone (P9).</summary>
    public string? CompanyName { get; set; }

    public string ActiveLabel => Active ? "Active" : "Put away";

    public string? CreatedAt { get; set; }

    public string? UpdatedAt { get; set; }
}

/// <summary>Q2=C: one preferred supplier and/or a genuine purchase-history supplier for a product.</summary>
public sealed class ProductSupplierDirectoryRow
{
    public int ProductId { get; set; }

    public int SupplierId { get; set; }

    public string? SupplierName { get; set; }

    public bool IsPreferred { get; set; }

    public string? LastPurchasedAt { get; set; }
}

/// <summary>One supplier offered by the product filter: preferred and/or genuine history (P9).</summary>
public sealed class ProductSupplierOption
{
    public int SupplierId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int ProductCount { get; set; }

    public override string ToString() => Name;
}

public static class DrawerStage
{
    public const string Opening = "opening";
    public const string Closing = "closing";

    public static readonly string[] All = { Opening, Closing };
}

/// <summary>One non-zero opening or closing drawer difference, including its mandatory reason.</summary>
public sealed class ShiftDiscrepancy
{
    public int Id { get; set; }

    public int ShiftId { get; set; }

    public string Stage { get; set; } = DrawerStage.Opening;

    public long ExpectedPaisa { get; set; }

    public long CountedPaisa { get; set; }

    public long DifferencePaisa { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string? AlertedAt { get; set; }

    public string? CreatedAt { get; set; }
}

/// <summary>
/// Receipt for an isolated schema-only rebuild. A first copy has no previous database and therefore
/// an empty TechnicalBackupPath; every replacement names the persistent SQLite snapshot made first.
/// </summary>
public sealed class IsolatedTestRebuildResult
{
    public string DatabasePath { get; set; } = string.Empty;

    public string TechnicalBackupPath { get; set; } = string.Empty;

    public int MigrationsApplied { get; set; }

    public int TableCount { get; set; }

    public bool HadPreviousCopy { get; set; }
}

/// <summary>Approved master-checklist setting keys. Their behavior arrives in the named later phases.</summary>
public static class MasterFoundationSettings
{
    public const string PrinterDefaultBulk = "printer.defaultBulk";
    public const string PrinterBulkFormat = "printer.bulkFormat";
    public const string ProfitShowMargins = "profit.showMargins";
    public const string WhatsAppPreparePdf = "whatsapp.preparePdf";
    public const string WhatsAppExportDirectory = "whatsapp.exportDirectory";
    public const string SyncAutomaticEnabled = "sync.automaticEnabled";
    public const string SyncAutomaticMinutes = "sync.automaticMinutes";

    public const string SeparateReceipts = "separate_receipts";
    public const string A4List = "a4_list";
}
