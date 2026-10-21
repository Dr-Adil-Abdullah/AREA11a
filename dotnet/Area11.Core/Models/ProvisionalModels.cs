namespace Area11.Core.Models;

/// <summary>One medicine on a no-bill return: what came back and in which unit it was counted.</summary>
public sealed class ProvisionalLineInput
{
    public int ProductId { get; set; }

    public double QtyEntered { get; set; } = 1;

    /// <summary>box | strip | tablet - the words the cashier uses (5.1.3).</summary>
    public string Unit { get; set; } = "box";
}

/// <summary>
/// A rush-time return with no paper bill (9.2.2, U-33). The cashier refunds the customer at once
/// and the goods go back on the shelf; the bill is attached later, or the entry is cancelled with
/// a reason.
/// </summary>
public sealed class ProvisionalInput
{
    public string? CustomerName { get; set; }

    public string? Phone { get; set; }

    /// <summary>Money handed back to the customer - typed by the cashier, because there is no bill.</summary>
    public long RefundPaisa { get; set; }

    /// <summary>Why it came back (defective, wrong medicine, …). Optional at the counter.</summary>
    public string? Reason { get; set; }

    public string? Notes { get; set; }

    public List<ProvisionalLineInput> Lines { get; set; } = new();
}

/// <summary>A finished no-bill return as the board and the printed slip show it.</summary>
public sealed class ProvisionalRow
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string? Date { get; set; }

    public string? CustomerName { get; set; }

    public string? Phone { get; set; }

    public string? Reason { get; set; }

    public string? Notes { get; set; }

    public long RefundPaisa { get; set; }

    /// <summary>pending | linked | cancelled</summary>
    public string Status { get; set; } = "pending";

    public int? LinkedSaleId { get; set; }

    public string? LinkedCode { get; set; }

    public string? LinkedAt { get; set; }

    public string? CancelReason { get; set; }

    public string? CancelledAt { get; set; }

    public string? CashierName { get; set; }

    public int ItemCount { get; set; }

    public string ItemsLabel { get; set; } = string.Empty;

    public string RefundText => Area11.Core.Utils.Money.Format(RefundPaisa);

    public bool IsPending => string.Equals(Status, "pending", StringComparison.OrdinalIgnoreCase);

    public bool IsLinked => string.Equals(Status, "linked", StringComparison.OrdinalIgnoreCase);

    public bool IsCancelled => string.Equals(Status, "cancelled", StringComparison.OrdinalIgnoreCase);

    /// <summary>What the card says on the right. The blinking red is drawn by the screen.</summary>
    public string StatusLabel => Status switch
    {
        "linked" => "bill linked - waiting for the owner",
        "cancelled" => "cancelled",
        _ => "bill MISSING"
    };

    public string CustomerLabel
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(CustomerName) ? "Unknown customer" : CustomerName!;
            return string.IsNullOrWhiteSpace(Phone) ? name : $"{name} · {Phone}";
        }
    }
}

/// <summary>The result of saving a no-bill return: what the cashier reads out and prints.</summary>
public sealed class ProvisionalResult
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public long RefundPaisa { get; set; }

    public int ItemCount { get; set; }

    public List<string> Warnings { get; init; } = new();
}

/// <summary>One bill found by the smart lookup (9.2.1): a name, a phone or a rough date.</summary>
public sealed class BillSearchRow
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string? Date { get; set; }

    public string? CustomerName { get; set; }

    public string? Phone { get; set; }

    public long TotalPaisa { get; set; }

    public int ItemCount { get; set; }

    public string ItemsLabel { get; set; } = string.Empty;

    public string TotalText => Area11.Core.Utils.Money.Format(TotalPaisa);

    public string DateLabel => string.IsNullOrWhiteSpace(Date) || Date!.Length < 16 ? Date ?? string.Empty : Date![..16];
}
