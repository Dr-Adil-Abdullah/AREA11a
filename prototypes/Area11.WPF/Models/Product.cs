namespace Area11.WPF.Models;

/// <summary>
/// A medicine or retail item. Stock is measured in the lowest unit (tablet / ml / piece),
/// and the pack formula (box → strips → units) converts every sale back to that unit.
/// </summary>
public sealed class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Barcode { get; set; }

    public string? Generic { get; set; }

    public int? CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public int? CompanyId { get; set; }

    public string? CompanyName { get; set; }

    public int BoxStrips { get; set; } = 1;

    public int StripUnits { get; set; } = 1;

    public long CostPaisa { get; set; }

    public long RetailPaisa { get; set; }

    public long VipPaisa { get; set; }

    public long DoctorPaisa { get; set; }

    public string? Rack { get; set; }

    public string? Room { get; set; }

    public int ReorderLevel { get; set; }

    public long StockBase { get; set; }

    public string? PhotoFile { get; set; }

    public bool Active { get; set; } = true;

    public bool IsLowStock => ReorderLevel > 0 && StockBase <= ReorderLevel;
}
