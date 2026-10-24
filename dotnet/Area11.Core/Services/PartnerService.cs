using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>Customers and suppliers: the two account books (Spec 9.1, 17).</summary>
public sealed class PartnerService
{
    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly LogService _log;

    public PartnerService(Database db, AuditService audit, LogService log)
    {
        _db = db;
        _audit = audit;
        _log = log;
    }

    public ServiceResult<List<Customer>> GetCustomers(string? search = null)
    {
        try
        {
            var term = (search ?? string.Empty).Trim();

            using var connection = _db.Open();
            var rows = _db.Query<Customer>(connection, @"
SELECT id, name, phone, category, credit_limit_paisa, balance_paisa, loyalty_points, photo, active
FROM customers
WHERE active = 1
  AND (@term = '' OR name LIKE @like OR IFNULL(phone, '') LIKE @like)
ORDER BY name COLLATE NOCASE;",
                new { term, like = $"%{term}%" });

            return ServiceResult<List<Customer>>.Success(rows);
        }
        catch (Exception exception)
        {
            _log.Error("Customers could not be loaded.", exception);
            return ServiceResult<List<Customer>>.Fail("Customers could not be loaded.");
        }
    }

    public ServiceResult<int> CreateCustomer(User actor, string name, string? phone, string category)
    {
        var clean = (name ?? string.Empty).Trim();
        if (clean.Length < 2)
        {
            return ServiceResult<int>.Fail("Enter the customer's name.");
        }

        var type = (category ?? "normal").Trim().ToLowerInvariant();
        if (type is not ("normal" or "vip" or "doctor"))
        {
            type = "normal";
        }

        var cleanPhone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();

        try
        {
            // A phone number is how the shop finds a person's khata. Two rows with the same number
            // split one family's debt in two, and at the counter nobody can tell which row to
            // collect from - so the second one is refused and the cashier is sent to the first
            // (D-50). Leaving the phone empty is always allowed.
            if (cleanPhone is not null)
            {
                using var look = _db.Open();
                var owner = _db.QuerySingle<string>(look, @"
SELECT name FROM customers
WHERE active = 1 AND IFNULL(phone, '') = @phone
LIMIT 1;",
                    new { phone = cleanPhone });

                if (!string.IsNullOrEmpty(owner))
                {
                    return ServiceResult<int>.Fail(
                        "That phone number already belongs to " + owner
                        + ". Open that customer to put the sale on their khata, or leave the phone empty.");
                }
            }

            var id = _db.Tx(connection =>
            {
                _db.Execute(connection, @"
INSERT INTO customers(name, phone, category, credit_limit_paisa, balance_paisa, loyalty_points, stars, active, created_at)
VALUES(@name, @phone, @category, 0, 0, 0, 1, 1, @at);",
                    new
                    {
                        name = clean,
                        phone = cleanPhone,
                        category = type,
                        at = Clock.Now()
                    });

                var newId = (int)_db.NextId(connection);

                _audit.Record(connection, actor, "Customers", "create", "customers", newId.ToString(),
                    newValue: $"{{\"name\":\"{clean}\",\"category\":\"{type}\"}}");

                return newId;
            });

            return ServiceResult<int>.Success(id);
        }
        catch (Exception exception)
        {
            _log.Error("A customer could not be created.", exception);
            return ServiceResult<int>.Fail("The customer could not be saved.");
        }
    }

    /// <summary>
    /// Corrects a customer's name, phone, category and - owner or manager only - credit limit (F-11).
    ///
    /// The three promises:
    ///   • **nothing else moves.** The udhaar is history, not an editable field; this method never
    ///     touches `balance_paisa`, so the khata page still says "Books match" after an edit.
    ///   • **the limit is money, the phone is not.** A cashier may fix a name or a phone (the counter
    ///     cannot work with a wrong number), but a limit that really changes needs the owner or a
    ///     manager - and a refusal changes **nothing**, not even the name in the same form.
    ///   • **allowed with a warning, not blocked.** A limit below the udhaar, or a phone two
    ///     customers share, is the owner's business: it is saved and said out loud (U-30 spirit).
    /// </summary>
    public ServiceResult<CustomerEditResult> UpdateCustomer(User actor, CustomerEditInput input)
    {
        if (input is null)
        {
            return ServiceResult<CustomerEditResult>.Fail("Nothing to save.");
        }

        var name = (input.Name ?? string.Empty).Trim();
        if (name.Length < 2)
        {
            return ServiceResult<CustomerEditResult>.Fail("Enter the customer's name (at least two letters).");
        }

        var category = (input.Category ?? "normal").Trim().ToLowerInvariant();
        if (category is not ("normal" or "vip" or "doctor"))
        {
            category = "normal";
        }

        var phone = string.IsNullOrWhiteSpace(input.Phone) ? null : input.Phone.Trim();

        if (input.CreditLimitPaisa is < 0)
        {
            return ServiceResult<CustomerEditResult>.Fail("The credit limit cannot be negative. Use 0 for no limit.");
        }

        try
        {
            return _db.Tx(connection =>
                SaveCustomer(connection, actor, input, name, phone, category));
        }
        catch (Exception exception)
        {
            _log.Error("The customer " + input.Id + " could not be updated.", exception);
            return ServiceResult<CustomerEditResult>.Fail("The customer could not be saved. Nothing was changed.");
        }
    }

    private ServiceResult<CustomerEditResult> SaveCustomer(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        User actor,
        CustomerEditInput input,
        string name,
        string? phone,
        string category)
    {
        // The public Customer model is reused on purpose: Dapper is happiest with a public type, and
        // there is no second row class to keep in step with the schema.
        var row = _db.QuerySingle<Customer>(connection, @"
SELECT id AS id, name AS name, phone AS phone, category AS category,
       credit_limit_paisa AS creditLimitPaisa, balance_paisa AS balancePaisa
FROM customers
WHERE id = @id AND active = 1;",
            new { id = input.Id });

        if (row is null)
        {
            return ServiceResult<CustomerEditResult>.Fail("That customer no longer exists - refresh the list.");
        }

        var result = new CustomerEditResult
        {
            Id = row.Id,
            Name = name,
            OldLimitPaisa = row.CreditLimitPaisa,
            CreditLimitPaisa = row.CreditLimitPaisa
        };

        // The limit only moves when the form really sends a different number.
        if (input.CreditLimitPaisa is long wanted && wanted != row.CreditLimitPaisa)
        {
            if (!actor.CanManage)
            {
                return ServiceResult<CustomerEditResult>.Fail(
                    "Only the owner or a manager can change a credit limit. Nothing was changed.");
            }

            result.LimitChanged = true;
            result.CreditLimitPaisa = wanted;

            if (wanted > 0 && row.BalancePaisa > wanted)
            {
                result.Warnings.Add("The udhaar " + Money.Format(row.BalancePaisa) + " is above the new limit "
                    + Money.Format(wanted) + " - the next credit bill will be refused until it comes down.");
            }
        }

        if (phone is not null)
        {
            var shared = _db.Query<string>(connection, @"
SELECT name FROM customers
WHERE active = 1 AND id <> @id AND IFNULL(phone, '') <> '' AND phone = @phone
ORDER BY name COLLATE NOCASE;",
                new { id = row.Id, phone });

            if (shared.Count > 0)
            {
                result.Warnings.Add("The phone " + phone + " is also on " + string.Join(", ", shared)
                    + " - a search by phone will show both.");
            }
        }

        _db.Execute(connection, @"
UPDATE customers
SET name = @name, phone = @phone, category = @category, credit_limit_paisa = @limit
WHERE id = @id;",
            new
            {
                name,
                phone,
                category,
                limit = result.CreditLimitPaisa,
                id = row.Id
            });

        // Old → new, only the fields that moved, in the same transaction (Spec 22.4).
        _audit.RecordChange(connection, actor, "Customers", "update", "customers", row.Id.ToString(),
            new { row.Name, row.Phone, row.Category, row.CreditLimitPaisa },
            new { Name = name, Phone = phone, Category = category, CreditLimitPaisa = result.CreditLimitPaisa });

        return ServiceResult<CustomerEditResult>.Success(result);
    }

    public ServiceResult<List<Supplier>> GetSuppliers()
    {
        try
        {
            using var connection = _db.Open();
            var rows = _db.Query<Supplier>(connection, @"
SELECT id, name, agency, phone, balance_paisa, notes, active
FROM suppliers WHERE active = 1 ORDER BY name COLLATE NOCASE;");

            return ServiceResult<List<Supplier>>.Success(rows);
        }
        catch (Exception exception)
        {
            _log.Error("Suppliers could not be loaded.", exception);
            return ServiceResult<List<Supplier>>.Fail("Suppliers could not be loaded.");
        }
    }

    public ServiceResult<int> CreateSupplier(User actor, string name, string? phone, string? agency)
    {
        if (!actor.CanManage)
        {
            return ServiceResult<int>.Fail("Only the owner or a manager can add suppliers.");
        }

        var clean = (name ?? string.Empty).Trim();
        if (clean.Length < 2)
        {
            return ServiceResult<int>.Fail("Enter the supplier's name.");
        }

        try
        {
            var id = _db.Tx(connection =>
            {
                _db.Execute(connection, @"
INSERT INTO suppliers(name, agency, phone, balance_paisa, active, created_at)
VALUES(@name, @agency, @phone, 0, 1, @at);",
                    new
                    {
                        name = clean,
                        agency = string.IsNullOrWhiteSpace(agency) ? null : agency.Trim(),
                        phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
                        at = Clock.Now()
                    });

                var newId = (int)_db.NextId(connection);

                _audit.Record(connection, actor, "Suppliers", "create", "suppliers", newId.ToString(),
                    newValue: $"{{\"name\":\"{clean}\"}}");

                return newId;
            });

            return ServiceResult<int>.Success(id);
        }
        catch (Exception exception)
        {
            _log.Error("A supplier could not be created.", exception);
            return ServiceResult<int>.Fail("The supplier could not be saved.");
        }
    }
}
