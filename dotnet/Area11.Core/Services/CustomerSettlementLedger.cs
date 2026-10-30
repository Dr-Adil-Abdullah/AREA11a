using Area11.Core.Data;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>A bill taking part in one customer's chronological khata replay.</summary>
internal sealed class CustomerSettlementBill
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Date { get; set; } = string.Empty;

    public long OriginalDuePaisa { get; set; }

    /// <summary>False until the replay reaches this bill, so an old receipt cannot see a future bill.</summary>
    public bool Started { get; internal set; }

    /// <summary>The part of this particular bill still owed after collections and its own returns.</summary>
    public long CreditRemainingPaisa { get; internal set; }
}

/// <summary>A customer-level collection or a khata reduction tied to one return's own bill.</summary>
internal sealed class CustomerSettlementEvent
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public string Date { get; set; } = string.Empty;

    public long AmountPaisa { get; set; }

    public string Kind { get; set; } = string.Empty;

    public int? SaleId { get; set; }

    public bool IsCollection => string.Equals(Kind, "collection", StringComparison.OrdinalIgnoreCase);
}

internal sealed class CustomerSettlementHit
{
    public int? SaleId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Date { get; set; } = string.Empty;

    public long PaidPaisa { get; set; }

    public long LeftPaisa { get; set; }

    public bool IsOpening { get; set; }
}

internal sealed class CustomerSettlementCollection
{
    public int PaymentId { get; set; }

    public long BalanceBeforePaisa { get; set; }

    public long ReceivedPaisa { get; set; }

    public long BalanceAfterPaisa { get; set; }

    public List<CustomerSettlementHit> Hits { get; } = new();
}

internal sealed class CustomerSettlementSnapshot
{
    public List<CustomerSettlementBill> Bills { get; } = new();

    public long OpeningRemainingPaisa { get; internal set; }

    public CustomerSettlementCollection? Collection { get; internal set; }

    public CustomerSettlementBill? Bill(int saleId) => Bills.FirstOrDefault(bill => bill.Id == saleId);
}

/// <summary>
/// The single chronological allocation rule used by bill-linked returns, recovery ageing and the
/// recovery slip (Spec 13.5, 15.2.1). A collection pays the oldest bill that exists **at that
/// moment**. A return is different: its khata part belongs to the bill printed on that return and
/// can never clear an older, unrelated bill.
///
/// Original sale rows and old return/payment rows are inputs only. The replay changes no history.
/// Bills enter the timeline on their own date, which also makes a reprint immune to later bills.
/// Carried-in khata has no invented bill date: dated bills are paid first and any remaining
/// collection then reduces the opening balance. Equal timestamps are deterministic: bills first,
/// then collections, then returns, with the row id breaking ties inside each kind.
/// </summary>
internal static class CustomerSettlementLedger
{
    public static CustomerSettlementSnapshot Read(Database db, SqliteConnection connection,
        int customerId, int? throughPaymentId = null)
    {
        var opening = db.QuerySingle<long>(connection,
            "SELECT IFNULL(opening_balance_paisa, 0) FROM customers WHERE id = @id;", new { id = customerId });

        var bills = db.Query<CustomerSettlementBill>(connection, @"
SELECT s.id AS id, s.customer_id AS customerId, s.code AS code, s.date AS date,
       s.due_paisa AS originalDuePaisa
FROM sales s
WHERE s.customer_id = @id AND s.status IN ('paid', 'partial', 'credit');", new { id = customerId });

        var events = db.Query<CustomerSettlementEvent>(connection, @"
SELECT customerId, id, date, amountPaisa, kind, saleId FROM (
    SELECT p.customer_id AS customerId, p.id AS id, p.date AS date,
           p.amount_paisa AS amountPaisa, 'collection' AS kind, NULL AS saleId
    FROM payments p
    WHERE p.customer_id = @id AND p.sale_id IS NULL AND p.purchase_id IS NULL
      AND p.provisional_return_id IS NULL AND p.amount_paisa > 0
    UNION ALL
    SELECT s.customer_id AS customerId, r.id AS id, r.date AS date,
           r.credit_reduced_paisa AS amountPaisa, 'return' AS kind, r.sale_id AS saleId
    FROM sale_returns r
    JOIN sales s ON s.id = r.sale_id
    WHERE s.customer_id = @id AND r.credit_reduced_paisa > 0
);", new { id = customerId });

        return Replay(bills, events, opening, throughPaymentId);
    }

    public static CustomerSettlementSnapshot Replay(IEnumerable<CustomerSettlementBill> bills,
        IEnumerable<CustomerSettlementEvent> events, long openingPaisa, int? throughPaymentId = null)
    {
        var snapshot = new CustomerSettlementSnapshot();
        snapshot.Bills.AddRange(bills);
        var byId = snapshot.Bills.ToDictionary(bill => bill.Id);
        var openingRemaining = Math.Max(0, openingPaisa);

        var timeline = snapshot.Bills.Select(bill => new TimelineRow
            {
                Date = bill.Date,
                KindOrder = 0,
                Id = bill.Id,
                Bill = bill
            })
            .Concat(events.Select(item => new TimelineRow
            {
                Date = item.Date,
                KindOrder = item.IsCollection ? 1 : 2,
                Id = item.Id,
                Event = item
            }))
            .OrderBy(row => row.Date, StringComparer.Ordinal)
            .ThenBy(row => row.KindOrder)
            .ThenBy(row => row.Id);

        long Balance() => openingRemaining
            + snapshot.Bills.Where(bill => bill.Started).Sum(bill => bill.CreditRemainingPaisa);

        foreach (var row in timeline)
        {
            if (row.Bill is not null)
            {
                row.Bill.Started = true;
                row.Bill.CreditRemainingPaisa = Math.Max(0, row.Bill.OriginalDuePaisa);
                continue;
            }

            var item = row.Event!;
            if (!item.IsCollection)
            {
                if (item.SaleId is int saleId && byId.TryGetValue(saleId, out var returnedBill))
                {
                    // A malformed old timestamp must not silently detach the return from its bill.
                    if (!returnedBill.Started)
                    {
                        returnedBill.Started = true;
                        returnedBill.CreditRemainingPaisa = Math.Max(0, returnedBill.OriginalDuePaisa);
                    }

                    returnedBill.CreditRemainingPaisa = Math.Max(0,
                        returnedBill.CreditRemainingPaisa - Math.Max(0, item.AmountPaisa));
                }

                continue;
            }

            var capture = throughPaymentId == item.Id;
            var collection = capture
                ? new CustomerSettlementCollection
                {
                    PaymentId = item.Id,
                    BalanceBeforePaisa = Balance(),
                    ReceivedPaisa = item.AmountPaisa
                }
                : null;
            var left = Math.Max(0, item.AmountPaisa);

            foreach (var bill in snapshot.Bills
                         .Where(bill => bill.Started && bill.CreditRemainingPaisa > 0)
                         .OrderBy(bill => bill.Date, StringComparer.Ordinal)
                         .ThenBy(bill => bill.Id))
            {
                if (left <= 0)
                {
                    break;
                }

                var take = Math.Min(left, bill.CreditRemainingPaisa);
                bill.CreditRemainingPaisa -= take;
                left -= take;

                collection?.Hits.Add(new CustomerSettlementHit
                {
                    SaleId = bill.Id,
                    Code = bill.Code,
                    Date = bill.Date,
                    PaidPaisa = take,
                    LeftPaisa = bill.CreditRemainingPaisa
                });
            }

            if (left > 0 && openingRemaining > 0)
            {
                var take = Math.Min(left, openingRemaining);
                openingRemaining -= take;
                left -= take;

                collection?.Hits.Add(new CustomerSettlementHit
                {
                    Code = "OPENING",
                    PaidPaisa = take,
                    LeftPaisa = openingRemaining,
                    IsOpening = true
                });
            }

            if (collection is not null)
            {
                collection.BalanceAfterPaisa = Balance();
                snapshot.Collection = collection;
                snapshot.OpeningRemainingPaisa = openingRemaining;
                break; // a receipt ends at its own payment; later history cannot rewrite it
            }
        }

        snapshot.OpeningRemainingPaisa = openingRemaining;
        return snapshot;
    }

    private sealed class TimelineRow
    {
        public string Date { get; set; } = string.Empty;

        public int KindOrder { get; set; }

        public int Id { get; set; }

        public CustomerSettlementBill? Bill { get; set; }

        public CustomerSettlementEvent? Event { get; set; }
    }
}
