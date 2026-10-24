namespace Area11.WPF.Models;

public sealed class PurchaseLineInput
{
    public int ProductId { get; set; }

    /// <summary>box, strip or base (tablet / ml / piece).</summary>
    public string Unit { get; set; } = "base";

    public int Quantity { get; set; }

    public string BatchNo { get; set; } = string.Empty;

    public string Expiry { get; set; } = string.Empty;

    public long CostPaisa { get; set; }

    public long RetailPaisa { get; set; }

    public bool IsSample { get; set; }
}

public sealed class PurchaseInput
{
    public int SupplierId { get; set; }

    public string? Note { get; set; }

    public long PaidPaisa { get; set; }

    public List<PurchaseLineInput> Lines { get; set; } = new();
}

public sealed class Supplier
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Agency { get; set; }

    public string? Phone { get; set; }

    public long BalancePaisa { get; set; }

    public override string ToString() => Name;
}

public sealed class Customer
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string Type { get; set; } = "normal";

    public long CreditLimitPaisa { get; set; }

    public long BalancePaisa { get; set; }

    public override string ToString() => Name;
}
