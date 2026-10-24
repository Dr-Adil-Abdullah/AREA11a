namespace Area11.Core.Models;

/// <summary>One line the cashier wants back: which bill line, and how much of it.</summary>
public sealed class ReturnLineInput
{
    public int SaleItemId { get; set; }

    /// <summary>Quantity in BASE units (tablets), the unit the stock is kept in.</summary>
    public double QtyBase { get; set; }
}

public sealed class ReturnInput
{
    public int SaleId { get; set; }

    public List<ReturnLineInput> Lines { get; set; } = new();

    /// <summary>defective | expired | changed mind | wrong medicine | other</summary>
    public string Reason { get; set; } = "changed mind";

    public string? Note { get; set; }

    /// <summary>
    /// True = the goods go back on the shelf at once (U-32, the default). False = the goods go to
    /// quarantine with their own Q-number and only come back if someone decides so (U-34).
    /// </summary>
    public bool Restock { get; set; } = true;
}

/// <summary>
/// One bill line as the return screen shows it: what was sold, what already came back, what is
/// still returnable, and what a full return of the remainder would refund.
/// </summary>
/// <remarks>
/// Deliberately NOT sealed. <c>ReturnService.ItemRow</c> derives from it to add the batch id that
/// the refund planner needs, and the screen still receives it as a plain option. Sealing it broke
/// the first real build with CS0509 ("cannot derive from sealed type") - a compile error the static
/// checker cannot see, so <c>tools/check_repo.py</c> now refuses any class that derives from a
/// sealed one.
/// </remarks>
public class ReturnLineOption
{
    public int SaleItemId { get; set; }

    public int ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string UnitSold { get; set; } = "base";

    /// <summary>Sold quantity in the unit the customer bought (2 strips).</summary>
    public double QtyEntered { get; set; }

    /// <summary>Sold quantity in base units (tablets).</summary>
    public double QtyBase { get; set; }

    public double AlreadyReturnedBase { get; set; }

    public long LineTotalPaisa { get; set; }

    public long AlreadyRefundedPaisa { get; set; }

    public double RemainingBase => Math.Max(0, QtyBase - AlreadyReturnedBase);

    public long RemainingPaisa => Math.Max(0, LineTotalPaisa - AlreadyRefundedPaisa);

    /// <summary>How many units the customer can still hand back, in the unit they bought.</summary>
    public double RemainingUnits => QtyBase <= 0 ? 0 : RemainingBase / (QtyBase / Math.Max(QtyEntered, 0.000001));

    public bool CanReturn => RemainingBase > 0.0005;

    public string SoldLabel => $"{Utils.Qty.Format(QtyEntered)} {UnitSold}";

    public string ReturnedLabel => Utils.Qty.Format(AlreadyReturnedBase);

    public string RemainingLabel => $"{Utils.Qty.Format(RemainingUnits)} {UnitSold}";

    public string RemainingMoneyLabel => Utils.Money.Format(RemainingPaisa);
}

/// <summary>What the screen shows before the cashier presses Refund.</summary>
public sealed class ReturnQuote
{
    public long RefundPaisa { get; init; }

    public long CashPaisa { get; init; }

    public long CreditReducedPaisa { get; init; }

    /// <summary>What was collected for this bill and has not been refunded yet.</summary>
    public long RefundablePaisa { get; init; }

    public List<string> Warnings { get; init; } = new();
}

public sealed class ReturnResult
{
    public int ReturnId { get; init; }

    public string Code { get; init; } = string.Empty;

    public long RefundPaisa { get; init; }

    public long CashPaisa { get; init; }

    public long CreditReducedPaisa { get; init; }

    /// <summary>The permanent quarantine numbers created by this return (empty when restocked).</summary>
    public List<string> QuarantineCodes { get; init; } = new();

    public List<string> Warnings { get; init; } = new();
}

/// <summary>One row of the quarantine list / history (U-34).</summary>
public sealed class QuarantineItem
{
    public int ReturnId { get; set; }

    public string? QCode { get; set; }

    public string? ReturnCode { get; set; }

    public string? ProductName { get; set; }

    public double QtyBase { get; set; }

    public string? UnitSold { get; set; }

    public double QtyEntered { get; set; }

    public string? Date { get; set; }

    public string? Reason { get; set; }

    public string Disposition { get; set; } = "quarantine";

    public string? DisposedAt { get; set; }

    public string? DisposedBy { get; set; }

    public string? DisposedNote { get; set; }

    public string QuantityLabel => $"{Utils.Qty.Format(QtyEntered > 0 ? QtyEntered : QtyBase)} {UnitSold ?? "base"}";

    public string DispositionLabel => Disposition switch
    {
        "restocked" => "Back in stock",
        "expired" => "Damaged / expired",
        "sold" => "Sold on",
        _ => "In quarantine"
    };

    public bool IsPending => string.Equals(Disposition, "quarantine", StringComparison.OrdinalIgnoreCase);
}
