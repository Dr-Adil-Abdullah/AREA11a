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

/// <summary>One part of a split payment: how much was paid, and by which method.</summary>
public sealed class SalePaymentInput
{
    /// <summary>cash | online | card | credit</summary>
    public string Method { get; set; } = "cash";

    public long AmountPaisa { get; set; }
}

public sealed class SaleInput
{
    public int? CustomerId { get; set; }

    public List<SaleLineInput> Lines { get; set; } = new();

    public decimal DiscountPercent { get; set; }

    /// <summary>cash | credit | online | card | split</summary>
    public string PaymentMethod { get; set; } = "cash";

    /// <summary>
    /// Split payment (Spec 13.3). Empty = one method from <see cref="PaymentMethod"/>. When it has
    /// rows, each row becomes its own receipt line and, for money actually received, its own row in
    /// the `payments` table. A credit row is NOT money in the drawer: it becomes `due_paisa` and the
    /// customer's khata, which is why it is never written as a payment row.
    /// </summary>
    public List<SalePaymentInput> Payments { get; set; } = new();

    /// <summary>Cash handed over by the customer; change is what is left after the cash part.</summary>
    public long TenderedPaisa { get; set; }

    /// <summary>
    /// Loyalty points the customer wants to spend on this bill (F-40). 1 point = Rs 1; the Core caps
    /// the request by his balance, by the bill and (when the below-cost lock is on) by the cost of
    /// the goods - it never refuses at the till.
    /// </summary>
    public int RedeemPoints { get; set; }

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

    /// <summary>Money handed over in cash (split) - the rest is online/card or on credit.</summary>
    public long CashPartPaisa { get; init; }

    /// <summary>The part that goes on the customer's khata.</summary>
    public long CreditPartPaisa { get; init; }

    /// <summary>Points actually spent on this bill (after the caps).</summary>
    public int RedeemPoints { get; init; }

    /// <summary>What those points took off the bill, in paisa.</summary>
    public long RedeemPaisa { get; init; }

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

    /// <summary>The `sale_items.id` of every line, in cart order - a return points at these.</summary>
    public List<int> SaleItemIds { get; init; } = new();

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

/// <summary>One line of a parked cart, exactly as the cashier left it (unit + quantity).</summary>
public sealed class HeldCartLine
{
    public int ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Unit { get; set; } = "base";

    public double Quantity { get; set; }

    public long UnitPricePaisa { get; set; }
}

/// <summary>A parked bill as the counter list shows it - no lines, so the list stays light.</summary>
public sealed class HeldCartSummary
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string? CreatedAt { get; set; }

    public string? CustomerName { get; set; }

    public long TotalPaisa { get; set; }

    public int ItemsCount { get; set; }

    public string? CashierName { get; set; }

    public string WhenLabel => CreatedAt is { Length: >= 16 } ? CreatedAt[..16] : CreatedAt ?? string.Empty;

    public string ItemsLabel => ItemsCount == 1 ? "1 line" : $"{ItemsCount} lines";

    public string TotalLabel => Money.Format(TotalPaisa);

    public string CustomerLabel => string.IsNullOrWhiteSpace(CustomerName) ? "Walk-in customer" : CustomerName!;

    public string CashierLabel => string.IsNullOrWhiteSpace(CashierName) ? "—" : CashierName!;
}

/// <summary>A parked bill brought back to the counter, lines included.</summary>
public sealed class HeldCart
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public int? CustomerId { get; set; }

    public decimal DiscountPercent { get; set; }

    public List<HeldCartLine> Lines { get; set; } = new();
}
