using Area11.Core.Utils;

namespace Area11.Core.Models;

/// <summary>
/// The kinds of thing a photo can belong to (migration 015). The strings are the values the
/// `photos.entity` column already holds, so an older database needs no conversion.
/// </summary>
public static class PhotoEntity
{
    public const string Product = "product";
    public const string Customer = "customer";
    public const string Supplier = "supplier";

    public static readonly string[] All = { Product, Customer, Supplier };

    public static bool IsKnown(string? entity) =>
        !string.IsNullOrWhiteSpace(entity) && All.Contains(entity.Trim().ToLowerInvariant());

    public static string Clean(string? entity) => (entity ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>"Medicine", "Customer", "Supplier" - the word the window shows.</summary>
    public static string LabelFor(string? entity) => Clean(entity) switch
    {
        Product => "Medicine",
        Customer => "Customer",
        Supplier => "Supplier",
        _ => "Record"
    };

    /// <summary>The permission that governs the photos of this kind of record.</summary>
    public static string ModuleFor(string? entity) => Clean(entity) switch
    {
        Product => "products",
        Supplier => "suppliers",
        _ => "customers"
    };
}

/// <summary>
/// One photo on the shelf: the row in `photos` plus the path it really has on this computer.
///
/// The database stores a **file name**, never a path (rule U-19): the folder is per machine, so a
/// backup restored on another computer must not carry a path from the old one.
/// </summary>
public sealed class PhotoItem
{
    public int Id { get; set; }

    public string Entity { get; set; } = PhotoEntity.Product;

    public int EntityId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public string? CreatedAt { get; set; }

    /// <summary>False for a photograph the owner put away - the file is still on the disk.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Where the file is on this computer. Filled by PhotoService, never by the database.</summary>
    public string FullPath { get; set; } = string.Empty;

    /// <summary>False when the row survived but the file was moved or deleted by hand.</summary>
    public bool Exists { get; set; }

    public string Caption => EntityLabel + " photo " + SortOrder;

    public string EntityLabel => PhotoEntity.LabelFor(Entity);

    public string WhenLabel => string.IsNullOrWhiteSpace(CreatedAt)
        ? string.Empty
        : ("added " + (CreatedAt!.Length >= 10 ? CreatedAt[..10] : CreatedAt!));

    public string StateLabel => IsActive ? WhenLabel : WhenLabel + " - put away";

    /// <summary>"3 photos", "1 photo", "no photos" - the line under the gallery.</summary>
    public static string CountLabel(int count) => count switch
    {
        0 => "no photos",
        1 => "1 photo",
        _ => count + " photos"
    };
}
