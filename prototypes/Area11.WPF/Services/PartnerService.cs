using Area11.WPF.Data;
using Area11.WPF.Models;
using Area11.WPF.Utils;

namespace Area11.WPF.Services;

/// <summary>
/// Customers and suppliers share one ledger rule: balances only change through bills,
/// payments and returns — never by editing the number by hand.
/// </summary>
public sealed class PartnerService
{
    private readonly Database _db;
    private readonly LoggerService _log;
    private readonly AuditService _audit;

    public PartnerService(Database db, LoggerService log, AuditService audit)
    {
        _db = db;
        _log = log;
        _audit = audit;
    }

    public OperationResult<List<Supplier>> GetSuppliers()
    {
        try
        {
            using var connection = _db.Open();
            var rows = _db.Query(connection, @"
                SELECT id, name, agency, phone, balance_paisa
                FROM suppliers WHERE active = 1 ORDER BY name COLLATE NOCASE;",
                reader => new Supplier
                {
                    Id = reader.Int("id"),
                    Name = reader.Str("name"),
                    Agency = reader.NullStr("agency"),
                    Phone = reader.NullStr("phone"),
                    BalancePaisa = reader.Long("balance_paisa")
                });

            return OperationResult<List<Supplier>>.Success(rows);
        }
        catch (Exception exception)
        {
            _log.Error("Suppliers could not be loaded.", exception);
            return OperationResult<List<Supplier>>.Fail("Suppliers could not be loaded.");
        }
    }

    public OperationResult<List<Customer>> GetCustomers(string? search = null)
    {
        try
        {
            using var connection = _db.Open();
            var term = (search ?? string.Empty).Trim();
            var rows = _db.Query(connection, @"
                SELECT id, name, phone, type, credit_limit_paisa, balance_paisa
                FROM customers
                WHERE active = 1 AND ($hasText = 0 OR name LIKE $like OR IFNULL(phone,'') LIKE $like)
                ORDER BY name COLLATE NOCASE;",
                reader => new Customer
                {
                    Id = reader.Int("id"),
                    Name = reader.Str("name"),
                    Phone = reader.NullStr("phone"),
                    Type = reader.Str("type"),
                    CreditLimitPaisa = reader.Long("credit_limit_paisa"),
                    BalancePaisa = reader.Long("balance_paisa")
                },
                ("$hasText", term.Length > 0 ? 1 : 0),
                ("$like", $"%{term}%"));

            return OperationResult<List<Customer>>.Success(rows);
        }
        catch (Exception exception)
        {
            _log.Error("Customers could not be loaded.", exception);
            return OperationResult<List<Customer>>.Fail("Customers could not be loaded.");
        }
    }

    public OperationResult<int> CreateSupplier(string name, string? phone, string? agency, string userName)
    {
        try
        {
            var cleanName = (name ?? string.Empty).Trim();
            if (cleanName.Length == 0)
            {
                throw new ValidationException("Supplier name is required.");
            }

            var id = _db.Tx(connection =>
            {
                _db.Exec(connection, @"
                    INSERT INTO suppliers(name, agency, phone, balance_paisa, active, created_at)
                    VALUES($name, $agency, $phone, 0, 1, $at);",
                    ("$name", cleanName),
                    ("$agency", string.IsNullOrWhiteSpace(agency) ? DBNull.Value : agency.Trim()),
                    ("$phone", string.IsNullOrWhiteSpace(phone) ? DBNull.Value : phone.Trim()),
                    ("$at", FormatHelper.Stamp()));

                var newId = (int)_db.LastInsertId(connection);
                _audit.Record(connection, userName, "Suppliers", "create", "suppliers", newId, null, new { name = cleanName });
                return newId;
            });

            return OperationResult<int>.Success(id);
        }
        catch (ValidationException exception)
        {
            return OperationResult<int>.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error("Supplier could not be created.", exception);
            return OperationResult<int>.Fail("The supplier could not be saved.");
        }
    }

    public OperationResult<int> CreateCustomer(string name, string? phone, string type, string userName)
    {
        try
        {
            var cleanName = (name ?? string.Empty).Trim();
            if (cleanName.Length == 0)
            {
                throw new ValidationException("Customer name is required.");
            }

            var id = _db.Tx(connection =>
            {
                _db.Exec(connection, @"
                    INSERT INTO customers(name, phone, type, credit_limit_paisa, balance_paisa, active, created_at)
                    VALUES($name, $phone, $type, 0, 0, 1, $at);",
                    ("$name", cleanName),
                    ("$phone", string.IsNullOrWhiteSpace(phone) ? DBNull.Value : phone.Trim()),
                    ("$type", string.IsNullOrWhiteSpace(type) ? "normal" : type),
                    ("$at", FormatHelper.Stamp()));

                var newId = (int)_db.LastInsertId(connection);
                _audit.Record(connection, userName, "Customers", "create", "customers", newId, null, new { name = cleanName });
                return newId;
            });

            return OperationResult<int>.Success(id);
        }
        catch (ValidationException exception)
        {
            return OperationResult<int>.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error("Customer could not be created.", exception);
            return OperationResult<int>.Fail("The customer could not be saved.");
        }
    }

    /// <summary>Money handed to a supplier reduces what the shop owes (and may become an advance).</summary>
    public OperationResult PaySupplier(int supplierId, long amountPaisa, string? note, string userName)
    {
        var warnings = new List<string>();
        try
        {
            if (amountPaisa <= 0)
            {
                throw new ValidationException("Payment must be greater than zero.");
            }

            _db.Tx(connection =>
            {
                var supplier = _db.Query(connection,
                    "SELECT name, balance_paisa FROM suppliers WHERE id = $id AND active = 1;",
                    reader => new { name = reader.Str("name"), balance = reader.Long("balance_paisa") },
                    ("$id", supplierId)).FirstOrDefault()
                    ?? throw new ValidationException("Supplier not found.");

                _db.Exec(connection, @"
                    INSERT INTO payments(purchase_id, supplier_id, amount_paisa, method, note, created_at, created_by)
                    VALUES(NULL, $supplier, $amount, 'cash', $note, $at, $user);",
                    ("$supplier", supplierId),
                    ("$amount", amountPaisa),
                    ("$note", string.IsNullOrWhiteSpace(note) ? DBNull.Value : note.Trim()),
                    ("$at", FormatHelper.Stamp()),
                    ("$user", userName));

                _db.Exec(connection,
                    "UPDATE suppliers SET balance_paisa = balance_paisa - $amount WHERE id = $id;",
                    ("$amount", amountPaisa),
                    ("$id", supplierId));

                _audit.Record(connection, userName, "Suppliers", "payment", "suppliers", supplierId,
                    new { balance = supplier.balance },
                    new { balance = supplier.balance - amountPaisa, paid = amountPaisa });

                if (supplier.balance <= 0)
                {
                    warnings.Add($"{supplier.name}: nothing was owed, so this whole amount is an advance (supplier owes us).");
                }
                else if (amountPaisa > supplier.balance)
                {
                    warnings.Add($"{supplier.name}: paid more than the balance — the extra is now an advance.");
                }
            });

            return OperationResult.Success(warnings);
        }
        catch (ValidationException exception)
        {
            return OperationResult.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error($"Supplier payment for #{supplierId} failed.", exception);
            return OperationResult.Fail("The payment could not be saved.");
        }
    }

    /// <summary>Customer collects goods on credit; the receivable grows by the credit part of the bill.</summary>
    public void AddCustomerCredit(Microsoft.Data.Sqlite.SqliteConnection connection, int customerId, long amountPaisa)
    {
        _db.Exec(connection,
            "UPDATE customers SET balance_paisa = balance_paisa + $amount WHERE id = $id;",
            ("$amount", amountPaisa),
            ("$id", customerId));
    }
}
