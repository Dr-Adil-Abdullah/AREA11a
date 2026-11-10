using Area11.Core.Utils;

namespace Area11.Core.Models;

/// <summary>One purchase of this medicine: when, from whom, how much, at what rate.</summary>
public sealed class ProductPurchaseRow
{
    public string Date { get; set; } = string.Empty;

    public string SupplierName { get; set; } = string.Empty;

    public string BillCode { get; set; } = string.Empty;

    public double QtyBase { get; set; }

    /// <summary>Cost per base unit, as it was on that bill - never today's rate.</summary>
    public long RatePaisa { get; set; }

    public long LineTotalPaisa { get; set; }

    public string DateLabel => Date.Length >= 10 ? Date[..10] : Date;

    public string QtyLabel => Qty.Format(QtyBase);

    public string RateLabel => Money.Format(RatePaisa);

    public string TotalLabel => Money.Format(LineTotalPaisa);
}

/// <summary>One sale of this medicine: when, on which bill, to whom, at what rate.</summary>
public sealed class ProductSaleRow
{
    public string Date { get; set; } = string.Empty;

    public string BillCode { get; set; } = string.Empty;

    /// <summary>Empty for a walk-in customer: the bill itself has no name.</summary>
    public string BuyerName { get; set; } = string.Empty;

    public double QtyBase { get; set; }

    /// <summary>The bill's unit price (for the unit sold - box, strip or loose) - never today's rate.</summary>
    public long RatePaisa { get; set; }

    public long LineTotalPaisa { get; set; }

    /// <summary>Line total minus quantity times the cost snapshot - the approved definition.</summary>
    public long ProfitPaisa { get; set; }

    public string DateLabel => Date.Length >= 10 ? Date[..10] : Date;

    public string BuyerLabel => string.IsNullOrWhiteSpace(BuyerName) ? "walk-in" : BuyerName;

    public string QtyLabel => Qty.Format(QtyBase);

    public string RateLabel => Money.Format(RatePaisa);

    public string TotalLabel => Money.Format(LineTotalPaisa);

    public string ProfitLabel => Money.Format(ProfitPaisa);
}

/// <summary>Everything ever bought and sold of one medicine, oldest first, with honest totals.</summary>
public sealed class ProductHistory
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public List<ProductPurchaseRow> Purchases { get; init; } = new();

    public List<ProductSaleRow> Sales { get; init; } = new();

    /// <summary>False without the `profit` permission or with the global switch off.</summary>
    public bool CanSeeProfit { get; init; }

    public bool HasPurchases => Purchases.Count > 0;

    public bool HasSales => Sales.Count > 0;

    public double BoughtQty => Purchases.Sum(row => row.QtyBase);

    public double SoldQty => Sales.Sum(row => row.QtyBase);

    public long BoughtPaisa => Purchases.Sum(row => row.LineTotalPaisa);

    public long SoldPaisa => Sales.Sum(row => row.LineTotalPaisa);

    public long ProfitPaisa => Sales.Sum(row => row.ProfitPaisa);

    public string BoughtQtyLabel => Qty.Format(BoughtQty);

    public string SoldQtyLabel => Qty.Format(SoldQty);
}
