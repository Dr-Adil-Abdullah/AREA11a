using Area11.Core.Utils;

namespace Area11.Core.Models;

/// <summary>The numbers at the top of one supplier's page.</summary>
public sealed class SupplierSummary
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Agency { get; set; }

    public string? Phone { get; set; }

    /// <summary>+ = the shop owes the supplier. Minus = the supplier owes the shop (a credit).</summary>
    public long PayablePaisa { get; set; }

    public long OpeningPaisa { get; set; }

    public long PurchasesPaisa { get; set; }

    public long PaidPaisa { get; set; }

    /// <summary>Credit notes: goods returned and taken off the bill.</summary>
    public long CreditedPaisa { get; set; }

    /// <summary>Goods returned and the money taken in cash instead of a credit note.</summary>
    public long RefundedPaisa { get; set; }

    public int PurchasesCount { get; set; }

    public int ReturnsCount { get; set; }

    public string? LastPurchaseDate { get; set; }

    public string PayableText => Money.Format(PayablePaisa);

    public string PurchasesText => Money.Format(PurchasesPaisa);

    public string PaidText => Money.Format(PaidPaisa);

    public string CreditedText => Money.Format(CreditedPaisa);

    public string RefundedText => Money.Format(RefundedPaisa);

    /// <summary>One sentence the owner can read without knowing the sign convention.</summary>
    public string PayableLabel
    {
        get
        {
            if (PayablePaisa > 0)
            {
                return Money.Format(PayablePaisa) + " to pay";
            }

            return PayablePaisa < 0
                ? Money.Format(-PayablePaisa) + " with the supplier (credit)"
                : "settled";
        }
    }

    public string AgencyLabel => string.IsNullOrWhiteSpace(Agency) ? "-" : Agency!;

    public string PhoneLabel => string.IsNullOrWhiteSpace(Phone) ? "-" : Phone!;

    public string LastPurchaseLabel => string.IsNullOrWhiteSpace(LastPurchaseDate)
        ? "never bought"
        : LastPurchaseDate!.Length >= 10 ? LastPurchaseDate![..10] : LastPurchaseDate!;

    public string ActivityText => PurchasesCount + " bill(s) - " + ReturnsCount + " return(s)";
}

/// <summary>One line of the supplier's account: a purchase, money paid, or goods sent back.</summary>
public sealed class SupplierMovement
{
    public string? Date { get; set; }

    /// <summary>opening | purchase | payment | credit_note | cash_return</summary>
    public string Kind { get; set; } = "purchase";

    public string Code { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Money added to what the shop owes (a purchase).</summary>
    public long DebitPaisa { get; set; }

    /// <summary>Money taken off what the shop owes (a payment or a credit note).</summary>
    public long CreditPaisa { get; set; }

    public long RunningPaisa { get; set; }

    public string DebitLabel => DebitPaisa == 0 ? string.Empty : Money.Format(DebitPaisa);

    public string CreditLabel => CreditPaisa == 0 ? string.Empty : Money.Format(CreditPaisa);

    public string RunningLabel => Money.Format(RunningPaisa);

    public string DateLabel => string.IsNullOrWhiteSpace(Date) || Date.Length < 16 ? Date ?? string.Empty : Date![..16];

    public string SlipDate => DateLabel.Length >= 11 ? DateLabel[5..] : DateLabel;

    public string KindLabel => Kind switch
    {
        "opening" => "Opening balance",
        "payment" => "Money paid",
        "credit_note" => "Returned (credit note)",
        "cash_return" => "Returned (cash received)",
        _ => "Purchase"
    };
}

/// <summary>A supplier's whole page: numbers, every movement, and whether the books agree.</summary>
public sealed class SupplierPage
{
    public SupplierSummary Summary { get; set; } = new();

    public List<SupplierMovement> Lines { get; init; } = new();

    public long ExpectedPaisa { get; set; }

    public long StoredPaisa { get; set; }

    public long DifferencePaisa => StoredPaisa - ExpectedPaisa;

    public bool BooksMatch => DifferencePaisa == 0;

    public string BooksLabel => BooksMatch
        ? "Books match - the payable is exactly purchases - payments - credit notes"
        : "MISMATCH: the stored payable is " + Money.Format(DifferencePaisa) + " away from its history";
}

/// <summary>What the owner reads after money is paid to a supplier.</summary>
public sealed class SupplierPayment
{
    public int PaymentId { get; set; }

    public long AmountPaisa { get; set; }

    public long NewBalancePaisa { get; set; }

    public string Code { get; set; } = string.Empty;
}

/// <summary>One line of a supplier return: which batch, and how much of it.</summary>
public sealed class SupplierReturnLineInput
{
    public int ProductId { get; set; }

    public int BatchId { get; set; }

    public double QtyEntered { get; set; }

    /// <summary>box | strip | base</summary>
    public string Unit { get; set; } = "base";
}

/// <summary>Goods going back to the supplier (`SR-nnnn`).</summary>
public sealed class SupplierReturnInput
{
    public int SupplierId { get; set; }

    /// <summary>expired | damaged | wrong_item | overstock | other</summary>
    public string Reason { get; set; } = "expired";

    public string? Note { get; set; }

    /// <summary>True = the supplier gave the money back now; false = a credit note on the account.</summary>
    public bool CashReceived { get; set; }

    public List<SupplierReturnLineInput> Lines { get; set; } = new();
}

/// <summary>What is printed and shown after `SR-nnnn` is saved.</summary>
public sealed class SupplierReturnResult
{
    public int ReturnId { get; set; }

    public string Code { get; set; } = string.Empty;

    public long TotalPaisa { get; set; }

    public long NewPayablePaisa { get; set; }

    public bool CashReceived { get; set; }

    public List<string> Warnings { get; init; } = new();

    public string TotalText => Money.Format(TotalPaisa);

    public string SettlementLabel => CashReceived
        ? "Cash received from the supplier"
        : "Credit note - taken off what the shop owes";
}

/// <summary>A batch on the shelf that is expired or nearly expired - the goods to send back (9.3.3).</summary>
public sealed class ExpiredBatchRow
{
    public int BatchId { get; set; }

    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string BatchNo { get; set; } = string.Empty;

    public string? ExpiryDate { get; set; }

    public double QtyBase { get; set; }

    public long CostPaisa { get; set; }

    public string BaseUnit { get; set; } = "tablet";

    public int BoxStrips { get; set; }

    public int StripTablets { get; set; }

    public string QtyLabel => Qty.Format(QtyBase) + " " + BaseUnit;

    public string ValueLabel => Money.Format(Calc.Round(QtyBase * CostPaisa));

    public string ExpiryLabel => string.IsNullOrWhiteSpace(ExpiryDate) ? "no date" : ExpiryDate!;

    /// <summary>"Panadol 500mg - B-1 (2026-01-31)" - the one line the owner taps.</summary>
    public string Caption => ProductName + " - " + BatchNo + " (" + ExpiryLabel + "), " + QtyLabel;
}
