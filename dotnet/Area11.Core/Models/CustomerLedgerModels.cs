using Area11.Core.Utils;

namespace Area11.Core.Models;

/// <summary>One line of a customer's khata book: a bill that added debt, money received, or a return.</summary>
public sealed class LedgerLine
{
    public string Date { get; set; } = string.Empty;

    /// <summary>bill | collection | return | cash_bill</summary>
    public string Kind { get; set; } = "bill";

    /// <summary>The document number: INV-0001, RET-0002, or the payment number for a collection.</summary>
    public string Code { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Money added to the khata (a credit bill).</summary>
    public long DebitPaisa { get; set; }

    /// <summary>Money taken off the khata (a collection or a return's credit part).</summary>
    public long CreditPaisa { get; set; }

    /// <summary>The balance after this line - the number the customer reads.</summary>
    public long RunningPaisa { get; set; }

    public string DebitLabel => DebitPaisa == 0 ? string.Empty : Money.Format(DebitPaisa);

    public string CreditLabel => CreditPaisa == 0 ? string.Empty : Money.Format(CreditPaisa);

    public string RunningLabel => Money.Format(RunningPaisa);

    public string DateLabel => string.IsNullOrWhiteSpace(Date) || Date.Length < 16 ? Date : Date[..16];

    /// <summary>"10-21 10:05" - the date without the year, so a movement fits on one 58 mm line.</summary>
    public string SlipDate => DateLabel.Length >= 11 ? DateLabel[5..] : DateLabel;

    public string KindLabel => Kind switch
    {
        "opening" => "Opening balance",
        "collection" => "Money received",
        "return" => "Return (khata)",
        "cash_bill" => "Bill (cash)",
        _ => "Credit bill"
    };
}

/// <summary>The numbers at the top of a customer's page.</summary>
public sealed class CustomerSummary
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Category { get; set; }

    public long CreditLimitPaisa { get; set; }

    /// <summary>
    /// What the customer already owed when the shop moved to Area11 (F-37). It is part of the khata,
    /// not a note beside it: the page starts its running total here, or "Books match" would be a lie
    /// on the first day.
    /// </summary>
    public long OpeningPaisa { get; set; }

    public long BalancePaisa { get; set; }

    public int LoyaltyPoints { get; set; }

    public int Stars { get; set; } = 1;

    public int BillsCount { get; set; }

    public long LifetimePaisa { get; set; }

    public long ProfitPaisa { get; set; }

    public long ReturnsPaisa { get; set; }

    public string? LastBillDate { get; set; }

    public string BalanceText => Money.Format(BalancePaisa);

    public string LimitText => CreditLimitPaisa <= 0 ? "no limit" : Money.Format(CreditLimitPaisa);

    public string LifetimeText => Money.Format(LifetimePaisa);

    public string ProfitText => Money.Format(ProfitPaisa);

    public string ReturnsText => Money.Format(ReturnsPaisa);

    public string StarsLabel => new string('*', Math.Clamp(Stars, 1, 5)) + new string('.', 5 - Math.Clamp(Stars, 1, 5));

    public string CategoryLabel => (Category ?? "normal").ToLowerInvariant() switch
    {
        "vip" => "VIP",
        "doctor" => "Doctor",
        _ => "Normal"
    };

    /// <summary>What the row says about the khata - the words the cashier uses.</summary>
    public string CreditStateLabel
    {
        get
        {
            if (BalancePaisa <= 0)
            {
                return "clear";
            }

            if (CreditLimitPaisa <= 0)
            {
                return Money.Format(BalancePaisa) + " udhaar";
            }

            return BalancePaisa > CreditLimitPaisa
                ? Money.Format(BalancePaisa) + " - OVER the limit"
                : Money.Format(BalancePaisa) + " of " + Money.Format(CreditLimitPaisa);
        }
    }

    public string PhoneLabel => string.IsNullOrWhiteSpace(Phone) ? "-" : Phone!;

    public string LastBillLabel => string.IsNullOrWhiteSpace(LastBillDate)
        ? "never bought"
        : LastBillDate!.Length >= 10 ? LastBillDate![..10] : LastBillDate!;
}

/// <summary>A customer's whole page: numbers, every movement, and whether the books agree.</summary>
public sealed class CustomerPage
{
    public CustomerSummary Summary { get; set; } = new();

    public List<LedgerLine> Lines { get; init; } = new();

    /// <summary>What the movements add up to, computed line by line with the same clamps as the app.</summary>
    public long ExpectedPaisa { get; set; }

    /// <summary>What is stored in `customers.balance_paisa`.</summary>
    public long StoredPaisa { get; set; }

    public long DifferencePaisa => StoredPaisa - ExpectedPaisa;

    public bool BooksMatch => DifferencePaisa == 0;

    public string BooksLabel => BooksMatch
        ? "Books match - every rupee of the khata is explained"
        : "MISMATCH: the stored khata is " + Money.Format(DifferencePaisa) + " away from its history";
}

/// <summary>What the cashier reads after money is received.</summary>
public sealed class CollectionResult
{
    public int PaymentId { get; set; }

    public long AmountPaisa { get; set; }

    public long NewBalancePaisa { get; set; }

    public string Code { get; set; } = string.Empty;
}
