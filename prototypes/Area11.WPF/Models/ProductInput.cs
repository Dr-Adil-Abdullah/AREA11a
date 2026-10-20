namespace Area11.WPF.Models;

/// <summary>Input for creating or editing a product. Null means "leave the stored value unchanged".</summary>
public sealed class ProductInput
{
    public string Name { get; set; } = string.Empty;

    public string? Barcode { get; set; }

    public string? Generic { get; set; }

    public int? CategoryId { get; set; }

    public string? NewCategoryName { get; set; }

    public int? CompanyId { get; set; }

    public string? NewCompanyName { get; set; }

    public int BoxStrips { get; set; } = 1;

    public int StripUnits { get; set; } = 1;

    public long CostPaisa { get; set; }

    public long RetailPaisa { get; set; }

    public long VipPaisa { get; set; }

    public long DoctorPaisa { get; set; }

    public string? Rack { get; set; }

    public string? Room { get; set; }

    public int ReorderLevel { get; set; }
}
