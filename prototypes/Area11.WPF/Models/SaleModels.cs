namespace Area11.WPF.Models;

public sealed class SaleLineInput
{
    public int ProductId { get; set; }

    /// <summary>box, strip or base (tablet / ml / piece).</summary>
    public string Unit { get; set; } = "base";

    public int Quantity { get; set; }

    /// <summary>Price for one selected unit. Zero means "use the price for the customer type".</summary>
    public long UnitPricePaisa { get; set; }
}

public sealed class PaymentSplitInput
{
    public string Method { get; set; } = "cash";

    public long AmountPaisa { get; set; }
}

public sealed class SaleInput
{
    public int? CustomerId { get; set; }

    public List<SaleLineInput> Lines { get; set; } = new();

    public decimal DiscountPercent { get; set; }

    public string PaymentMethod { get; set; } = "cash";

    public long TenderedPaisa { get; set; }

    public List<PaymentSplitInput> Splits { get; set; } = new();

    public string? Note { get; set; }
}

public sealed class SaleResult
{
    public string Code { get; init; } = string.Empty;

    public long TotalPaisa { get; init; }

    public long ChangePaisa { get; init; }

    public long DuePaisa { get; init; }

    public int ItemLines { get; init; }
}

/// <summary>One bill line already allocated to a batch (FEFO result, before saving).</summary>
public sealed class SaleLineDraft
{
    public int ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public int? BatchId { get; init; }

    public string BatchNo { get; init; } = string.Empty;

    public string Unit { get; init; } = "base";

    public int QtyBase { get; init; }

    public long UnitPricePaisa { get; init; }

    public long CostPaisa { get; init; }

    public long DiscountPaisa { get; set; }

    public long LineTotalPaisa { get; set; }
}
