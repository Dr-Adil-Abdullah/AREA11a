using Area11.Core.Utils;

namespace Area11.Core.Models;

/// <summary>One line typed at the counter: a quantity in a chosen unit.</summary>
public sealed class SaleLineInput
{
    public int ProductId { get; set; }

    /// <summary>box | strip | base</summary>
    public string Unit { get; set; } = "base";

    public double Quantity { get; set; }

    /// <summary>Price for one selected unit; 0 = use the customer tier price.</summary>
    public long UnitPricePaisa { get; set; }
}

public sealed class SaleInput
{
    public int? CustomerId { get; set; }

    public List<SaleLineInput> Lines { get; set; } = new();

    public decimal DiscountPercent { get; set; }

    /// <summary>cash | credit</summary>
    public string PaymentMethod { get; set; } = "cash";

    public long TenderedPaisa { get; set; }

    public string? Notes { get; set; }
}

/// <summary>What the counter shows before saving — computed by the same code that saves.</summary>
public sealed class SaleQuote
{
    public long SubtotalPaisa { get; init; }

    public long DiscountPaisa { get; init; }

    public long TaxPaisa { get; init; }

    public long RoundPaisa { get; init; }

    public long TotalPaisa { get; init; }

    public long ChangePaisa { get; init; }

    public double Units { get; init; }

    public bool GoesNegative { get; init; }

    public List<string> Warnings { get; init; } = new();
}

public sealed class SaleResult
{
    public int SaleId { get; init; }

    public string Code { get; init; } = string.Empty;

    public long SubtotalPaisa { get; init; }

    public long DiscountPaisa { get; init; }

    public long TaxPaisa { get; init; }

    public long RoundPaisa { get; init; }

    public long TotalPaisa { get; init; }

    public long PaidPaisa { get; init; }

    public long DuePaisa { get; init; }

    public long ChangePaisa { get; init; }

    public List<string> Warnings { get; init; } = new();
}

/// <summary>A sale line already allocated to one batch (FEFO result).</summary>
public sealed class SaleDraftLine
{
    public int ProductId { get; init; }

    public string Name { get; init; } = string.Empty;

    public int? BatchId { get; init; }

    public string BatchNo { get; init; } = string.Empty;

    public string UnitSold { get; init; } = "base";

    public double QtyEntered { get; init; }

    public double QtyBase { get; init; }

    public long UnitPricePaisa { get; init; }

    public long CostUnitPaisa { get; init; }

    public long GrossPaisa { get; set; }

    public long DiscountPaisa { get; set; }

    public long LineTotalPaisa { get; set; }

    public long LineCostPaisa => Calc.Round(CostUnitPaisa * QtyBase);
}
