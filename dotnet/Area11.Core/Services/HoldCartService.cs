using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>
/// Hold / park a bill (Spec 8.5.1, 12.1.5). Two customers at one counter: park the first cart, bill
/// the second, then bring the first one back exactly as it was.
///
/// Why a database and not a file: the original app used the browser's localStorage, which dies with
/// the browser profile. A parked bill holds a customer's medicines — it must survive a crash, a
/// restart and a power cut, so it lives in the shop database (`held_carts`, migration 016).
///
/// A parked cart is not a sale: no INV number, no money, no stock movement. It only becomes a bill
/// when it is resumed and the cashier presses Complete. That keeps the audit trail honest: parking a
/// cart can never be mistaken for a sale in today's figures.
/// </summary>
public sealed class HoldCartService
{
    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly NumberingService _numbering;
    private readonly PermissionService _permissions;
    private readonly SaleService _sales;
    private readonly LogService _log;

    public HoldCartService(
        Database db,
        AuditService audit,
        NumberingService numbering,
        PermissionService permissions,
        SaleService sales,
        LogService log)
    {
        _db = db;
        _audit = audit;
        _numbering = numbering;
        _permissions = permissions;
        _sales = sales;
        _log = log;
    }

    /// <summary>Parks a cart and returns its code (H-0001).</summary>
    public ServiceResult<string> Hold(SaleInput input, User user)
    {
        if (!_permissions.Can(user, Modules.Pos))
        {
            return ServiceResult<string>.Fail("You do not have permission to hold a cart.");
        }

        if (input.Lines.Count == 0)
        {
            return ServiceResult<string>.Fail("There is nothing to hold - the cart is empty.");
        }

        // The total is what the same calculation would charge right now. If the cart cannot be
        // priced (a limit, a missing rate, a switched-off discount), parking it would only move the
        // error to a later cashier, so it is refused here with the same words.
        var quote = _sales.Quote(input, user);
        if (!quote.Ok || quote.Value is null)
        {
            return ServiceResult<string>.Fail(quote.Error ?? "This cart cannot be held.");
        }

        try
        {
            var code = _db.Tx(connection =>
            {
                var number = _numbering.Take(connection, "hold");
                var stamp = Clock.Now();

                _db.Execute(connection, @"
INSERT INTO held_carts(code, created_at, updated_at, user_id, customer_id, discount_percent,
                       note, total_paisa, items_count, status)
VALUES(@code, @at, @at, @userId, @customerId, @discount, @note, @total, @count, 'held');",
                    new
                    {
                        code = number,
                        at = stamp,
                        userId = user.Id,
                        customerId = input.CustomerId is > 0 ? input.CustomerId.Value : (int?)null,
                        discount = (double)input.DiscountPercent,
                        note = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(),
                        total = quote.Value.TotalPaisa,
                        count = input.Lines.Count
                    });

                var cartId = (int)_db.NextId(connection);
                var position = 0;

                foreach (var line in input.Lines)
                {
                    var name = _db.QuerySingle<string>(connection,
                        "SELECT name FROM products WHERE id = @id;", new { id = line.ProductId })
                        ?? "Unknown item";

                    _db.Execute(connection, @"
INSERT INTO held_cart_items(cart_id, product_id, name_snapshot, unit, quantity, unit_price_paisa, position)
VALUES(@cartId, @productId, @name, @unit, @quantity, @price, @position);",
                        new
                        {
                            cartId,
                            productId = line.ProductId,
                            name,
                            unit = string.IsNullOrWhiteSpace(line.Unit) ? "base" : line.Unit.Trim().ToLowerInvariant(),
                            quantity = line.Quantity,
                            price = line.UnitPricePaisa,
                            position = position++
                        });
                }

                _audit.Record(connection, user, "POS", "cart_hold", "held_carts", cartId.ToString(),
                    newValue: $"{{\"code\":\"{number}\",\"total\":{quote.Value.TotalPaisa},\"lines\":{input.Lines.Count}}}");

                return number;
            });

            return ServiceResult<string>.Success(code);
        }
        catch (Exception exception)
        {
            _log.Error("The cart could not be held.", exception);
            return ServiceResult<string>.Fail("The cart could not be held. Please try again.");
        }
    }

    /// <summary>Parked carts, newest first. Resumed and discarded carts are history, not a list.</summary>
    public ServiceResult<List<HeldCartSummary>> List(User user)
    {
        try
        {
            using var connection = _db.Open();

            var rows = _db.Query<HeldCartSummary>(connection, @"
SELECT h.id, h.code, h.created_at AS createdAt, c.name AS customerName,
       h.total_paisa AS totalPaisa, h.items_count AS itemsCount,
       COALESCE(u.name, '') AS cashierName
FROM held_carts h
LEFT JOIN customers c ON c.id = h.customer_id
LEFT JOIN users u ON u.id = h.user_id
WHERE h.status = 'held'
ORDER BY h.created_at DESC, h.id DESC;");

            return ServiceResult<List<HeldCartSummary>>.Success(rows.ToList(), new[] { $"{rows.Count} held cart(s)." });
        }
        catch (Exception exception)
        {
            _log.Error("The held carts could not be listed.", exception);
            return ServiceResult<List<HeldCartSummary>>.Fail("The held carts could not be listed.");
        }
    }

    /// <summary>
    /// Brings a parked cart back and marks it resumed in the same transaction, so two counters
    /// pressing "Resume" at the same moment cannot both get the same cart.
    /// </summary>
    public ServiceResult<HeldCart> Resume(User user, int cartId)
    {
        if (!_permissions.Can(user, Modules.Pos))
        {
            return ServiceResult<HeldCart>.Fail("You do not have permission to bring back a cart.");
        }

        try
        {
            var cart = _db.Tx(connection =>
            {
                var row = _db.QuerySingle<CartRow>(connection, @"
SELECT id, code, customer_id, discount_percent, status
FROM held_carts WHERE id = @id;", new { id = cartId });

                if (row is null)
                {
                    throw new ValidationException("That held cart no longer exists.");
                }

                if (!string.Equals(row.Status, "held", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ValidationException("That cart was already taken or thrown away.");
                }

                _db.Execute(connection,
                    "UPDATE held_carts SET status = 'resumed', updated_at = @at WHERE id = @id;",
                    new { at = Clock.Now(), id = cartId });

                var lines = _db.Query<HeldCartLine>(connection, @"
SELECT product_id AS productId, name_snapshot AS name, unit, quantity, unit_price_paisa AS unitPricePaisa
FROM held_cart_items WHERE cart_id = @id ORDER BY position, id;", new { id = cartId });

                _audit.Record(connection, user, "POS", "cart_resume", "held_carts", cartId.ToString(),
                    details: $"{row.Code} brought back to the counter.");

                return new HeldCart
                {
                    Id = row.Id,
                    Code = row.Code,
                    CustomerId = row.CustomerId,
                    DiscountPercent = (decimal)row.DiscountPercent,
                    Lines = lines.ToList()
                };
            });

            return ServiceResult<HeldCart>.Success(cart);
        }
        catch (ValidationException exception)
        {
            return ServiceResult<HeldCart>.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error($"Held cart {cartId} could not be resumed.", exception);
            return ServiceResult<HeldCart>.Fail("The cart could not be brought back.");
        }
    }

    /// <summary>Throws a parked cart away. It stays in the table as history; the audit says who did it.</summary>
    public ServiceResult Discard(User user, int cartId)
    {
        if (!_permissions.Can(user, Modules.Pos))
        {
            return ServiceResult.Fail("You do not have permission to discard a cart.");
        }

        try
        {
            _db.Tx(connection =>
            {
                var code = _db.QuerySingle<string>(connection,
                    "SELECT code FROM held_carts WHERE id = @id AND status = 'held';", new { id = cartId });

                if (code is null)
                {
                    throw new ValidationException("That cart was already taken or thrown away.");
                }

                _db.Execute(connection,
                    "UPDATE held_carts SET status = 'discarded', updated_at = @at WHERE id = @id;",
                    new { at = Clock.Now(), id = cartId });

                _audit.Record(connection, user, "POS", "cart_discard", "held_carts", cartId.ToString(),
                    details: $"{code} discarded.");
            });

            return ServiceResult.Success();
        }
        catch (ValidationException exception)
        {
            return ServiceResult.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error($"Held cart {cartId} could not be discarded.", exception);
            return ServiceResult.Fail("The cart could not be thrown away.");
        }
    }

    /// <summary>How many carts are waiting - the badge on the counter.</summary>
    public int Count()
    {
        try
        {
            using var connection = _db.Open();
            return (int)Convert.ToInt64(_db.Scalar(connection,
                "SELECT COUNT(1) FROM held_carts WHERE status = 'held';") ?? 0L);
        }
        catch (Exception exception)
        {
            _log.Error("The number of held carts could not be counted.", exception);
            return 0;
        }
    }

    private sealed class CartRow
    {
        public int Id { get; set; }

        public string Code { get; set; } = string.Empty;

        public int? CustomerId { get; set; }

        public double DiscountPercent { get; set; }

        public string Status { get; set; } = "held";
    }
}
