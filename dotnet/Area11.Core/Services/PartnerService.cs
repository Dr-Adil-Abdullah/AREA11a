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

        try
        {
            var id = _db.Tx(connection =>
            {
                _db.Execute(connection, @"
INSERT INTO customers(name, phone, category, credit_limit_paisa, balance_paisa, loyalty_points, stars, active, created_at)
VALUES(@name, @phone, @category, 0, 0, 0, 1, 1, @at);",
                    new
                    {
                        name = clean,
                        phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
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
