using System.Globalization;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>
/// The supplier's account and the goods that go back (`SR-nnnn`), Spec 9.3, 17.4, F-28.
///
/// Two jobs, one promise: **what the shop owes a supplier is exactly opening balance + purchases -
/// payments - credit notes.** Every screen that shows a payable re-derives it from the history and
/// says "Books match" - or shows the difference in red. A number the app cannot explain is worse
/// than no number.
///
/// Three money movements exist, and only three:
///   • a purchase (`purchases.total_paisa`) raises the payable;
///   • money paid to the supplier (`payments` with a supplier and no purchase) lowers it, and paying
///     more than is owed is **refused** - never a negative payable through a payment;
///   • a supplier return lowers it by a **credit note**, or leaves it alone when the supplier handed
///     the cash back instead (`supplier_returns.settled = 1`).
///
/// One warning for the owner, in plain words: a cash refund from a supplier is **not** a `payments`
/// row - it is `supplier_returns.settled = 1`. The shift/galla report (Phase 3) must count those rows
/// as money IN, or the drawer will look long by exactly that amount.
///
/// Stock follows U-30: returning more than the batch holds still completes, the stock goes negative
/// and the answer carries a warning - unless the owner has switched negative stock off in Settings.
/// </summary>
public sealed class SupplierLedgerService
{
    public const string ReasonExpired = "expired";
    public const string ReasonDamaged = "damaged";
    public const string ReasonWrongItem = "wrong_item";
    public const string ReasonOverstock = "overstock";
    public const string ReasonOther = "other";

    public static readonly string[] ReturnReasons =
    {
        ReasonExpired, ReasonDamaged, ReasonWrongItem, ReasonOverstock, ReasonOther
    };

    /// <summary>Ways the shop can pay a supplier. "credit" is not one of them.</summary>
    public static readonly string[] PaymentMethods = { "cash", "online", "card" };

    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly NumberingService _numbering;
    private readonly SettingsService _settings;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    public SupplierLedgerService(
        Database db,
        AuditService audit,
        NumberingService numbering,
        SettingsService settings,
        PermissionService permissions,
        LogService log)
    {
        _db = db;
        _audit = audit;
        _numbering = numbering;
        _settings = settings;
        _permissions = permissions;
        _log = log;
    }

    // --------------------------------------------------------------------- reads

    /// <summary>The supplier list with the payable, searchable by name, agency or phone.</summary>
    public ServiceResult<List<SupplierSummary>> List(string? search = null, bool onlyOwing = false)
    {
        try
        {
            var term = (search ?? string.Empty).Trim();

            using var connection = _db.Open();

            var rows = _db.Query<SupplierSummary>(connection, SummarySql + @"
WHERE s.active = 1
  AND (@term = '' OR s.name LIKE @like OR IFNULL(s.agency, '') LIKE @like OR IFNULL(s.phone, '') LIKE @like)
  AND (@onlyOwing = 0 OR s.balance_paisa <> 0)
ORDER BY s.balance_paisa DESC, s.name COLLATE NOCASE;",
                new { term, like = "%" + term + "%", onlyOwing = onlyOwing ? 1 : 0 });

            return ServiceResult<List<SupplierSummary>>.Success(rows);
        }
        catch (Exception exception)
        {
            _log.Error("The supplier list could not be loaded.", exception);
            return ServiceResult<List<SupplierSummary>>.Fail("The suppliers could not be loaded.");
        }
    }

    /// <summary>One supplier: the numbers, every movement, and the payable rebuilt from history.</summary>
    public ServiceResult<SupplierPage> Page(int supplierId)
    {
        try
        {
            using var connection = _db.Open();

            var summary = _db.QuerySingle<SupplierSummary>(connection, SummarySql + @"
WHERE s.id = @id;", new { id = supplierId });

            if (summary is null)
            {
                return ServiceResult<SupplierPage>.Fail("That supplier was not found.");
            }

            var page = new SupplierPage
            {
                Summary = summary,
                StoredPaisa = summary.PayablePaisa
            };

            var running = summary.OpeningPaisa;

            if (summary.OpeningPaisa != 0)
            {
                page.Lines.Add(new SupplierMovement
                {
                    Date = string.Empty,
                    Kind = "opening",
                    Code = "OPEN",
                    Description = "Balance carried into Area11",
                    DebitPaisa = summary.OpeningPaisa > 0 ? summary.OpeningPaisa : 0,
                    CreditPaisa = summary.OpeningPaisa < 0 ? -summary.OpeningPaisa : 0,
                    RunningPaisa = running
                });
            }

            foreach (var movement in ReadMovements(connection, supplierId))
            {
                running += movement.DebitPaisa;
                running -= movement.CreditPaisa;
                movement.RunningPaisa = running;
                page.Lines.Add(movement);
            }

            page.ExpectedPaisa = running;

            return ServiceResult<SupplierPage>.Success(page);
        }
        catch (Exception exception)
        {
            _log.Error("The page of supplier " + supplierId + " could not be loaded.", exception);
            return ServiceResult<SupplierPage>.Fail("The supplier's account could not be loaded.");
        }
    }

    /// <summary>Expired stock still on the shelf - the goods the owner sends back first (9.3.3).</summary>
    public ServiceResult<List<ExpiredBatchRow>> ExpiredBatches(int limit = 50)
    {
        try
        {
            using var connection = _db.Open();

            var rows = _db.Query<ExpiredBatchRow>(connection, @"
SELECT b.id AS batchId, b.product_id AS productId, p.name AS productName, b.batch_no AS batchNo,
       b.expiry_date AS expiryDate, b.qty_base AS qtyBase, b.cost_paisa AS costPaisa,
       p.base_unit AS baseUnit, p.box_strips AS boxStrips, p.strip_tablets AS stripTablets
FROM batches b
JOIN products p ON p.id = b.product_id
WHERE b.active = 1 AND b.qty_base > 0
  AND b.expiry_date IS NOT NULL AND b.expiry_date <> ''
  AND date(b.expiry_date) <= date('now', 'localtime')
ORDER BY b.expiry_date ASC, p.name COLLATE NOCASE
LIMIT @limit;", new { limit = Math.Clamp(limit, 1, 500) });

            return ServiceResult<List<ExpiredBatchRow>>.Success(rows);
        }
        catch (Exception exception)
        {
            _log.Error("The expired batches could not be read.", exception);
            return ServiceResult<List<ExpiredBatchRow>>.Fail("The expired stock could not be loaded.");
        }
    }

    // -------------------------------------------------------------------- paying

    /// <summary>
    /// Money paid to a supplier. Refused when it is more than the shop owes - the account may go
    /// negative only through a credit note (goods sent back), never through a payment.
    /// </summary>
    public ServiceResult<SupplierPayment> Pay(
        User? user, int supplierId, long amountPaisa, string? method, string? note)
    {
        if (!_permissions.Can(user, Modules.Suppliers))
        {
            return ServiceResult<SupplierPayment>.Fail("You do not have permission to pay a supplier.");
        }

        if (amountPaisa <= 0)
        {
            return ServiceResult<SupplierPayment>.Fail("The amount must be more than zero.");
        }

        var cleanMethod = (method ?? "cash").Trim().ToLowerInvariant();
        if (Array.IndexOf(PaymentMethods, cleanMethod) < 0)
        {
            return ServiceResult<SupplierPayment>.Fail("Choose cash, online or card.");
        }

        try
        {
            return _db.Tx(connection =>
            {
                var supplier = _db.QuerySingle<SupplierBalanceRow>(connection, @"
SELECT id, name, balance_paisa AS payablePaisa FROM suppliers WHERE id = @id AND active = 1;",
                    new { id = supplierId })
                    ?? throw new ValidationException("That supplier was not found.");

                if (supplier.PayablePaisa < 0)
                {
                    throw new ValidationException(
                        supplier.Name + " owes the shop " + Money.Format(-supplier.PayablePaisa)
                        + " (a credit), so there is nothing to pay. Use a purchase to set it off.");
                }

                if (supplier.PayablePaisa == 0)
                {
                    throw new ValidationException(supplier.Name + " is settled - there is nothing to pay.");
                }

                if (amountPaisa > supplier.PayablePaisa)
                {
                    throw new ValidationException(
                        supplier.Name + " has to be paid " + Money.Format(supplier.PayablePaisa) + " only. "
                        + "Paying " + Money.Format(amountPaisa) + " would leave the account in credit, "
                        + "so it is refused.");
                }

                var stamp = Clock.Now();
                var cleanNote = string.IsNullOrWhiteSpace(note) ? "Supplier payment" : note!.Trim();

                _db.Execute(connection, @"
INSERT INTO payments(date, method, amount_paisa, supplier_id, user_id, note)
VALUES(@at, @method, @amount, @supplierId, @userId, @note);",
                    new
                    {
                        at = stamp,
                        method = cleanMethod,
                        amount = amountPaisa,
                        supplierId,
                        userId = user?.Id,
                        note = cleanNote
                    });

                var paymentId = (int)_db.NextId(connection);
                var newBalance = supplier.PayablePaisa - amountPaisa;

                _db.Execute(connection, "UPDATE suppliers SET balance_paisa = @balance WHERE id = @id;",
                    new { balance = newBalance, id = supplierId });

                _audit.Record(connection, user, "Suppliers", "supplier_pay", "suppliers", supplierId.ToString(),
                    oldValue: supplier.PayablePaisa.ToString(CultureInfo.InvariantCulture),
                    newValue: newBalance.ToString(CultureInfo.InvariantCulture),
                    details: cleanMethod + " " + Money.Format(amountPaisa) + " - " + cleanNote);

                return ServiceResult<SupplierPayment>.Success(new SupplierPayment
                {
                    PaymentId = paymentId,
                    AmountPaisa = amountPaisa,
                    NewBalancePaisa = newBalance,
                    Code = "PAY-" + paymentId.ToString("D4", CultureInfo.InvariantCulture)
                });
            });
        }
        catch (ValidationException exception)
        {
            return ServiceResult<SupplierPayment>.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error("A supplier payment for " + supplierId + " could not be saved.", exception);
            return ServiceResult<SupplierPayment>.Fail("The payment could not be recorded. Nothing was changed.");
        }
    }

    // ------------------------------------------------------------------ returning

    /// <summary>
    /// Goods back to the supplier under `SR-nnnn`: the stock leaves the chosen batches and the
    /// supplier's payable drops by a credit note (or stays put when the cash is handed back).
    /// </summary>
    public ServiceResult<SupplierReturnResult> Return(User user, SupplierReturnInput input)
    {
        if (!_permissions.Can(user, Modules.Suppliers))
        {
            return ServiceResult<SupplierReturnResult>.Fail(
                "Only the owner or a manager can send goods back to a supplier.");
        }

        try
        {
            return _db.Tx(connection => SaveReturn(connection, input, user));
        }
        catch (ValidationException exception)
        {
            return ServiceResult<SupplierReturnResult>.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error("A supplier return for " + input.SupplierId + " could not be saved.", exception);
            return ServiceResult<SupplierReturnResult>.Fail("The return could not be saved. Nothing was changed.");
        }
    }

    // ------------------------------------------------------------------ statement

    /// <summary>The supplier's statement on the same 58 / 80 mm pipeline as every other slip.</summary>
    public ServiceResult<ReceiptDocument> Statement(int supplierId)
    {
        var page = Page(supplierId);

        if (!page.Ok || page.Value is null)
        {
            return ServiceResult<ReceiptDocument>.Fail(page.Error ?? "The statement could not be prepared.");
        }

        var data = page.Value;
        var width = _settings.GetInt("printer.width", 58) >= 80 ? 48 : 32;

        var document = new ReceiptDocument
        {
            Title = "Supplier statement " + data.Summary.Name,
            Width = width,
            AutoCut = _settings.GetBool("printer.autoCut", true),
            Copies = 1
        };

        void Add(string text, ReceiptAlign align = ReceiptAlign.Left, bool bold = false, bool big = false) =>
            document.Lines.Add(new ReceiptLine(ReceiptService.Fit(text, width), align, bold, big));

        void Pair(string left, string right, bool bold = false) =>
            document.Lines.Add(new ReceiptLine(ReceiptService.TwoColumns(left, right, width), ReceiptAlign.Left, bold));

        var shop = _settings.GetString("store.name");
        Add(string.IsNullOrWhiteSpace(shop) ? "Area11" : shop, ReceiptAlign.Center, bold: true);
        document.Lines.Add(ReceiptLine.Sep(width));
        Add("SUPPLIER STATEMENT", ReceiptAlign.Center, bold: true, big: true);

        Add(data.Summary.Name, bold: true);
        Pair(data.Summary.AgencyLabel, data.Summary.PhoneLabel);
        Pair(data.Summary.ActivityText, "Last: " + data.Summary.LastPurchaseLabel);
        document.Lines.Add(ReceiptLine.Sep(width));

        foreach (var line in data.Lines)
        {
            var amount = line.DebitPaisa > 0
                ? "+" + Money.Fixed(line.DebitPaisa)
                : line.CreditPaisa > 0 ? "-" + Money.Fixed(line.CreditPaisa) : "-";

            Pair(line.SlipDate, amount + "  = " + Money.Fixed(line.RunningPaisa));
            Add("  " + ReceiptService.Fit(line.Code + " " + line.Description, width - 2));
        }

        document.Lines.Add(ReceiptLine.Sep(width));
        Pair("TOTAL PAYABLE", Money.Fixed(data.StoredPaisa), bold: true);
        Pair("Purchases", Money.Fixed(data.Summary.PurchasesPaisa));
        Pair("Paid", Money.Fixed(data.Summary.PaidPaisa));
        Pair("Credit notes", Money.Fixed(data.Summary.CreditedPaisa));

        if (!data.BooksMatch)
        {
            Add("NOTE: the payable does not match its history.", ReceiptAlign.Center, bold: true);
        }

        document.Lines.Add(ReceiptLine.Sep(width));
        Add("Checked by: ______________");
        Add("Signature");
        document.Lines.Add(ReceiptLine.Sep(width));

        var footer = _settings.GetString("store.footerNote");
        if (!string.IsNullOrWhiteSpace(footer))
        {
            Add(footer, ReceiptAlign.Center);
        }

        return ServiceResult<ReceiptDocument>.Success(document);
    }

    // ------------------------------------------------------------------- saving

    private ServiceResult<SupplierReturnResult> SaveReturn(
        SqliteConnection connection, SupplierReturnInput input, User user)
    {
        if (input.Lines is null || input.Lines.Count == 0)
        {
            throw new ValidationException("Add at least one batch to send back.");
        }

        var reason = (input.Reason ?? string.Empty).Trim().ToLowerInvariant();
        if (Array.IndexOf(ReturnReasons, reason) < 0)
        {
            throw new ValidationException("Choose a reason for the return.");
        }

        var supplier = _db.QuerySingle<SupplierBalanceRow>(connection, @"
SELECT id, name, balance_paisa AS payablePaisa FROM suppliers WHERE id = @id AND active = 1;",
            new { id = input.SupplierId })
            ?? throw new ValidationException("That supplier was not found.");

        var warnings = new List<string>();
        var stamp = Clock.Now();

        var planned = new List<PlannedReturnLine>();
        var seen = new HashSet<int>();

        foreach (var wanted in input.Lines)
        {
            if (!seen.Add(wanted.BatchId))
            {
                throw new ValidationException("The same batch is listed twice. Put both quantities on one line.");
            }

            if (wanted.QtyEntered <= 0)
            {
                throw new ValidationException("Every line needs a quantity bigger than zero.");
            }

            var product = _db.QuerySingle<ProductDbRow>(connection, @"
SELECT id, name, base_unit AS baseUnit, box_strips AS boxStrips, strip_tablets AS stripTablets,
       cost_paisa AS costPaisa
FROM products WHERE id = @id AND active = 1;", new { id = wanted.ProductId })
                ?? throw new ValidationException("A medicine in this return is not in the list any more.");

            var batch = _db.QuerySingle<BatchDbRow>(connection, @"
SELECT id, product_id AS productId, batch_no AS batchNo, qty_base AS qtyBase,
       cost_paisa AS costPaisa, expiry_date AS expiryDate, supplier_id AS supplierId
FROM batches WHERE id = @id AND product_id = @productId AND active = 1;",
                new { id = wanted.BatchId, productId = product.Id })
                ?? throw new ValidationException(
                    product.Name + ": that batch is not on the shelf any more. Refresh the list.");

            // A batch normally goes back to the company that sold it. When it did not come from this
            // supplier, the credit note would land on the wrong account - say so, but do not stop
            // the shop (the owner may be settling an old arrangement).
            if (batch.SupplierId is int batchSupplier && batchSupplier != input.SupplierId)
            {
                warnings.Add(product.Name + " (" + batch.BatchNo + ") was received from another supplier. "
                    + "The credit note will still be put on " + supplier.Name + " - check that this is right.");
            }

            var qtyBase = Calc.ToBase(wanted.QtyEntered,
                Packs.PerUnit(product.BoxStrips, product.StripTablets, wanted.Unit));

            if (qtyBase <= 0)
            {
                throw new ValidationException(product.Name + ": the quantity must be more than zero.");
            }

            var costPaisa = batch.CostPaisa > 0 ? batch.CostPaisa : product.CostPaisa;
            var stockAfter = batch.QtyBase - qtyBase;

            if (stockAfter < -0.0005)
            {
                if (!_settings.GetBool("stock.allowNegative", true))
                {
                    throw new ValidationException(
                        product.Name + ": only " + Qty.Format(batch.QtyBase) + " " + product.BaseUnit
                        + " in batch " + batch.BatchNo + ". Negative stock is switched off in Settings.");
                }

                // U-30: never block. The shelf is the truth, and the warning is the record.
                warnings.Add(product.Name + " (" + batch.BatchNo + "): stock MINUS ("
                    + Qty.Format(stockAfter) + " " + product.BaseUnit + ") after this return.");
            }

            planned.Add(new PlannedReturnLine
            {
                Product = product,
                Batch = batch,
                QtyBase = qtyBase,
                CostPaisa = costPaisa,
                LineTotalPaisa = Calc.Round(qtyBase * costPaisa)
            });
        }

        var totalPaisa = planned.Sum(line => line.LineTotalPaisa);

        // The number is taken only once every line has passed its checks, so a refused form does not
        // burn an SR number.
        var code = _numbering.Take(connection, "supplierReturn");

        _db.Execute(connection, @"
INSERT INTO supplier_returns(code, date, supplier_id, purchase_id, reason, note, total_paisa, settled,
                             user_id, created_at)
VALUES(@code, @at, @supplierId, NULL, @reason, @note, @total, @settled, @userId, @at);",
            new
            {
                code,
                at = stamp,
                supplierId = supplier.Id,
                reason,
                note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note!.Trim(),
                total = totalPaisa,
                settled = input.CashReceived ? 1 : 0,
                userId = user.Id
            });

        var returnId = (int)_db.NextId(connection);

        foreach (var line in planned)
        {
            _db.Execute(connection, @"
INSERT INTO supplier_return_items(return_id, product_id, batch_id, qty_base, cost_paisa, line_total_paisa)
VALUES(@returnId, @productId, @batchId, @qty, @cost, @total);",
                new
                {
                    returnId,
                    productId = line.Product.Id,
                    batchId = line.Batch.Id,
                    qty = line.QtyBase,
                    cost = line.CostPaisa,
                    total = line.LineTotalPaisa
                });

            _db.Execute(connection, "UPDATE batches SET qty_base = qty_base - @qty WHERE id = @id;",
                new { qty = line.QtyBase, id = line.Batch.Id });

            _db.Execute(connection, @"
INSERT INTO stock_movements(product_id, batch_id, type, qty_base, ref_type, ref_id, ref_code, note,
                            user_id, created_at)
VALUES(@productId, @batchId, 'supplier_return', @qty, 'supplier_return', @returnId, @code, @note,
       @userId, @at);",
                new
                {
                    productId = line.Product.Id,
                    batchId = line.Batch.Id,
                    qty = -line.QtyBase,
                    returnId,
                    code,
                    note = "Supplier return " + code + " (" + ReasonLabel(reason) + ")",
                    userId = user.Id,
                    at = stamp
                });
        }

        var newPayable = supplier.PayablePaisa;

        if (input.CashReceived)
        {
            // The money came back in the hand, so the account is untouched: goods out, cash in.
            warnings.Add("The " + Money.Format(totalPaisa) + " was taken in cash. The supplier's account "
                + "is unchanged; the cash belongs in the drawer, not in `payments`.");
        }
        else
        {
            newPayable = supplier.PayablePaisa - totalPaisa;

            _db.Execute(connection, "UPDATE suppliers SET balance_paisa = @balance WHERE id = @id;",
                new { balance = newPayable, id = supplier.Id });

            if (newPayable < 0)
            {
                warnings.Add("The credit note is bigger than what was owed: "
                    + Money.Format(-newPayable) + " now stands in the shop's favour with "
                    + supplier.Name + ". The next purchase will set it off.");
            }
        }

        _audit.Record(connection, user, "Suppliers", "supplier_return", "supplier_returns", returnId.ToString(),
            oldValue: supplier.PayablePaisa.ToString(CultureInfo.InvariantCulture),
            newValue: newPayable.ToString(CultureInfo.InvariantCulture),
            details: code + " " + Money.Format(totalPaisa) + ", " + ReasonLabel(reason)
                + (input.CashReceived ? ", cash received" : ", credit note")
                + ", lines: " + planned.Count);

        var result = new SupplierReturnResult
        {
            ReturnId = returnId,
            Code = code,
            TotalPaisa = totalPaisa,
            NewPayablePaisa = newPayable,
            CashReceived = input.CashReceived
        };

        foreach (var warning in warnings)
        {
            result.Warnings.Add(warning);
        }

        return ServiceResult<SupplierReturnResult>.Success(result, warnings.ToArray());
    }

    // -------------------------------------------------------------------- reads

    private List<SupplierMovement> ReadMovements(SqliteConnection connection, int supplierId)
    {
        // Aliases carry the property names: Dapper matches by name, so a "debit" column would never
        // reach DebitPaisa and the running balance would silently read zero.
        var purchases = _db.Query<SupplierMovement>(connection, @"
SELECT p.date AS date, 'purchase' AS kind, p.code AS code,
       p.total_paisa AS debitPaisa, 0 AS creditPaisa,
       'Bought ' || printf('%d', (SELECT COUNT(1) FROM purchase_items i WHERE i.purchase_id = p.id))
           || ' item(s)' AS description
FROM purchases p
WHERE p.supplier_id = @id
ORDER BY p.date, p.id;", new { id = supplierId });

        // The part of a purchase that was paid on the spot. It is NOT in the standalone list above
        // (that list excludes anything tied to a purchase), so without this line the rebuilt payable
        // would be bigger than the stored one by exactly the money already handed over.
        var paidWithPurchase = _db.Query<SupplierMovement>(connection, @"
SELECT p.date AS date, 'payment' AS kind, p.code AS code,
       0 AS debitPaisa, p.paid_paisa AS creditPaisa,
       'Paid with the purchase' AS description
FROM purchases p
WHERE p.supplier_id = @id AND p.paid_paisa > 0
ORDER BY p.date, p.id;", new { id = supplierId });

        var payments = _db.Query<SupplierMovement>(connection, @"
SELECT pm.date AS date, 'payment' AS kind,
       'PAY-' || printf('%04d', pm.id) AS code,
       0 AS debitPaisa, pm.amount_paisa AS creditPaisa,
       'Paid (' || pm.method || ')' AS description
FROM payments pm
WHERE pm.supplier_id = @id AND pm.purchase_id IS NULL
ORDER BY pm.date, pm.id;", new { id = supplierId });

        var returns = _db.Query<SupplierMovement>(connection, @"
SELECT r.date AS date,
       CASE WHEN r.settled = 1 THEN 'cash_return' ELSE 'credit_note' END AS kind,
       r.code AS code,
       0 AS debitPaisa,
       CASE WHEN r.settled = 1 THEN 0 ELSE r.total_paisa END AS creditPaisa,
       CASE WHEN r.settled = 1
            THEN 'Returned, cash received: ' || replace(IFNULL(r.reason, 'other'), '_', ' ')
            ELSE 'Returned (' || replace(IFNULL(r.reason, 'other'), '_', ' ') || ')' END AS description
FROM supplier_returns r
WHERE r.supplier_id = @id
ORDER BY r.date, r.id;", new { id = supplierId });

        var output = new List<SupplierMovement>();
        output.AddRange(purchases);
        output.AddRange(paidWithPurchase);
        output.AddRange(payments);
        output.AddRange(returns);

        // Oldest first: money in (a purchase) before money out on the same day, then a stable order.
        return output
            .OrderBy(line => line.Date, StringComparer.Ordinal)
            .ThenBy(line => line.Kind == "purchase" ? 0 : 1)
            .ToList();
    }

    private const string SummarySql = @"
SELECT s.id AS id, s.name AS name, s.agency AS agency, s.phone AS phone,
       s.balance_paisa AS payablePaisa, s.opening_balance_paisa AS openingPaisa,
       IFNULL((SELECT SUM(p.total_paisa) FROM purchases p WHERE p.supplier_id = s.id), 0) AS purchasesPaisa,
       IFNULL((SELECT SUM(pm.amount_paisa) FROM payments pm
               WHERE pm.supplier_id = s.id AND pm.purchase_id IS NULL), 0)
         + IFNULL((SELECT SUM(p.paid_paisa) FROM purchases p WHERE p.supplier_id = s.id), 0) AS paidPaisa,
       IFNULL((SELECT SUM(r.total_paisa) FROM supplier_returns r
               WHERE r.supplier_id = s.id AND r.settled = 0), 0) AS creditedPaisa,
       IFNULL((SELECT SUM(r.total_paisa) FROM supplier_returns r
               WHERE r.supplier_id = s.id AND r.settled = 1), 0) AS refundedPaisa,
       (SELECT COUNT(1) FROM purchases p WHERE p.supplier_id = s.id) AS purchasesCount,
       (SELECT COUNT(1) FROM supplier_returns r WHERE r.supplier_id = s.id) AS returnsCount,
       (SELECT MAX(p.date) FROM purchases p WHERE p.supplier_id = s.id) AS lastPurchaseDate
FROM suppliers s
";

    private static string ReasonLabel(string reason) => reason switch
    {
        ReasonExpired => "Expired",
        ReasonDamaged => "Broken / damaged",
        ReasonWrongItem => "Wrong goods",
        ReasonOverstock => "Overstock",
        _ => "Other"
    };

    // -------------------------------------------------------------------- types

    private sealed class SupplierBalanceRow
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public long PayablePaisa { get; set; }
    }

    private sealed class ProductDbRow
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string BaseUnit { get; set; } = "tablet";

        public int BoxStrips { get; set; }

        public int StripTablets { get; set; }

        public long CostPaisa { get; set; }
    }

    private sealed class BatchDbRow
    {
        public int Id { get; set; }

        public int ProductId { get; set; }

        public string BatchNo { get; set; } = string.Empty;

        public double QtyBase { get; set; }

        public long CostPaisa { get; set; }

        public string? ExpiryDate { get; set; }

        public int? SupplierId { get; set; }
    }

    private sealed class PlannedReturnLine
    {
        public ProductDbRow Product { get; set; } = new();

        public BatchDbRow Batch { get; set; } = new();

        public double QtyBase { get; set; }

        public long CostPaisa { get; set; }

        public long LineTotalPaisa { get; set; }
    }
}
