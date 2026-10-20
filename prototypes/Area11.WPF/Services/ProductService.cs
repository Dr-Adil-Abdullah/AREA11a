using Area11.WPF.Data;
using Area11.WPF.Models;
using Microsoft.Data.Sqlite;

namespace Area11.WPF.Services;

/// <summary>Catalog rules: pack formula, prices, categories/companies and soft delete.</summary>
public sealed class ProductService
{
    private readonly Database _db;
    private readonly LoggerService _log;
    private readonly AuditService _audit;

    public ProductService(Database db, LoggerService log, AuditService audit)
    {
        _db = db;
        _log = log;
        _audit = audit;
    }

    public OperationResult<List<Product>> Search(string? text, int? categoryId = null, bool lowStockOnly = false)
    {
        try
        {
            using var connection = _db.Open();
            var term = (text ?? string.Empty).Trim();
            var rows = _db.Query(connection, @"
                SELECT p.*, c.name AS category_name, m.name AS company_name
                FROM products p
                LEFT JOIN categories c ON c.id = p.category_id
                LEFT JOIN companies m ON m.id = p.company_id
                WHERE p.active = 1
                  AND ($hasText = 0 OR p.name LIKE $like
                       OR IFNULL(p.barcode, '') LIKE $like
                       OR IFNULL(p.generic, '') LIKE $like
                       OR IFNULL(p.rack, '') LIKE $like
                       OR IFNULL(p.room, '') LIKE $like)
                  AND ($category = 0 OR p.category_id = $category)
                  AND ($low = 0 OR (p.reorder_level > 0 AND p.stock_base <= p.reorder_level))
                ORDER BY p.name COLLATE NOCASE;",
                MapProduct,
                ("$hasText", term.Length > 0 ? 1 : 0),
                ("$like", $"%{term}%"),
                ("$category", categoryId is > 0 ? categoryId.Value : 0),
                ("$low", lowStockOnly ? 1 : 0));

            return OperationResult<List<Product>>.Success(rows);
        }
        catch (Exception exception)
        {
            _log.Error("Product search failed.", exception);
            return OperationResult<List<Product>>.Fail("Products could not be loaded.");
        }
    }

    public OperationResult<Product> GetById(int productId)
    {
        try
        {
            using var connection = _db.Open();
            var product = _db.Query(connection, @"
                SELECT p.*, c.name AS category_name, m.name AS company_name
                FROM products p
                LEFT JOIN categories c ON c.id = p.category_id
                LEFT JOIN companies m ON m.id = p.company_id
                WHERE p.id = $id;",
                MapProduct,
                ("$id", productId)).FirstOrDefault();

            return product is null
                ? OperationResult<Product>.Fail("Product not found.")
                : OperationResult<Product>.Success(product);
        }
        catch (Exception exception)
        {
            _log.Error($"Product {productId} could not be read.", exception);
            return OperationResult<Product>.Fail("The product could not be loaded.");
        }
    }

    public OperationResult<int> Create(ProductInput input, string userName)
    {
        var warnings = new List<string>();
        try
        {
            var id = _db.Tx(connection =>
            {
                var name = RequireName(input.Name);
                var categoryId = ResolveCategory(connection, input.CategoryId, input.NewCategoryName);
                var companyId = ResolveCompany(connection, input.CompanyId, input.NewCompanyName);
                var boxStrips = Math.Max(1, input.BoxStrips);
                var stripUnits = Math.Max(1, input.StripUnits);
                var stamp = Utils.FormatHelper.Stamp();

                _db.Exec(connection, @"
                    INSERT INTO products
                        (name, barcode, generic, category_id, company_id, box_strips, strip_units,
                         cost_paisa, retail_paisa, vip_paisa, doctor_paisa, rack, room, reorder_level,
                         stock_base, active, created_at, updated_at)
                    VALUES
                        ($name, $barcode, $generic, $category, $company, $boxStrips, $stripUnits,
                         $cost, $retail, $vip, $doctor, $rack, $room, $reorder, 0, 1, $at, $at);",
                    ("$name", name),
                    ("$barcode", NullIfEmpty(input.Barcode)),
                    ("$generic", NullIfEmpty(input.Generic)),
                    ("$category", categoryId ?? (object)DBNull.Value),
                    ("$company", companyId ?? (object)DBNull.Value),
                    ("$boxStrips", boxStrips),
                    ("$stripUnits", stripUnits),
                    ("$cost", input.CostPaisa),
                    ("$retail", input.RetailPaisa),
                    ("$vip", input.VipPaisa),
                    ("$doctor", input.DoctorPaisa),
                    ("$rack", NullIfEmpty(input.Rack)),
                    ("$room", NullIfEmpty(input.Room)),
                    ("$reorder", Math.Max(0, input.ReorderLevel)),
                    ("$at", stamp));

                var newId = (int)_db.LastInsertId(connection);

                _audit.Record(connection, userName, "Inventory", "create", "products", newId, null,
                    new { name, cost = input.CostPaisa, retail = input.RetailPaisa, boxStrips, stripUnits });

                if (input.RetailPaisa < input.CostPaisa)
                {
                    warnings.Add($"{name}: retail price is below cost — every sale will lose money.");
                }

                return newId;
            });

            return OperationResult<int>.Success(id, warnings);
        }
        catch (ValidationException exception)
        {
            return OperationResult<int>.Fail(exception.Message);
        }
        catch (Exception exception)
        {
            _log.Error("Product could not be created.", exception);
            return OperationResult<int>.Fail("The product could not be saved. Please try again.");
        }
    }

    public OperationResult Update(int productId, ProductInput input, string userName)
    {
        var warnings = new List<string>();
        try
        {
            _db.Tx(connection =>
            {
                var before = _db.Query(connection,
                    "SELECT name, cost_paisa, retail_paisa, box_strips, strip_units FROM products WHERE id = $id;",
                    reader => new
                    {
                        name = reader.Str("name"),
                        cost = reader.Long("cost_paisa"),
                        retail = reader.Long("retail_paisa"),
                        boxStrips = reader.Int("box_strips"),
                        stripUnits = reader.Int("strip_units")
                    },
                    ("$id", productId)).FirstOrDefault()
                    ?? throw new ValidationException("Product not found.");

                var name = RequireName(input.Name);
                var categoryId = ResolveCategory(connection, input.CategoryId, input.NewCategoryName);
                var companyId = ResolveCompany(connection, input.CompanyId, input.NewCompanyName);

                _db.Exec(connection, @"
                    UPDATE products SET
                        name = $name,
                        barcode = $barcode,
                        generic = $generic,
                        category_id = $category,
                        company_id = $company,
                        box_strips = $boxStrips,
                        strip_units = $stripUnits,
                        cost_paisa = $cost,
                        retail_paisa = $retail,
                        vip_paisa = $vip,
                        doctor_paisa = $doctor,
                        rack = $rack,
                        room = $room,
                        reorder_level = $reorder,
                        updated_at = $at
                    WHERE id = $id;",
                    ("$name", name),
                    ("$barcode", NullIfEmpty(input.Barcode)),
                    ("$generic", NullIfEmpty(input.Generic)),
                    ("$category", categoryId ?? (object)DBNull.Value),
                    ("$company", companyId ?? (object)DBNull.Value),
                    ("$boxStrips", Math.Max(1, input.BoxStrips)),
                    ("$stripUnits", Math.Max(1, input.StripUnits)),
                    ("$cost", input.CostPaisa),
                    ("$retail", input.RetailPaisa),
                    ("$vip", input.VipPaisa),
                    ("$doctor", input.DoctorPaisa),
                    ("$rack", NullIfEmpty(input.Rack)),
                    ("$room", NullIfEmpty(input.Room)),
                    ("$reorder", Math.Max(0, input.ReorderLevel)),
                    ("$at", Utils.FormatHelper.Stamp()),
                    ("$id", productId));

                _audit.Record(connection, userName, "Inventory", "update", "products", productId, before,
                    new { name, cost = input.CostPaisa, retail = input.RetailPaisa });

                if (input.RetailPaisa < input.CostPaisa)
                {
                    warnings.Add($"{name}: retail price is below cost — every sale will lose money.");
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
            _log.Error($"Product {productId} could not be updated.", exception);
            return OperationResult.Fail("The product could not be updated.");
        }
    }

    public OperationResult Deactivate(int productId, string userName)
    {
        try
        {
            _db.Tx(connection =>
            {
                _db.Exec(connection,
                    "UPDATE products SET active = 0, updated_at = $at WHERE id = $id;",
                    ("$at", Utils.FormatHelper.Stamp()),
                    ("$id", productId));

                _audit.Record(connection, userName, "Inventory", "deactivate", "products", productId,
                    new { active = true }, new { active = false });
            });

            return OperationResult.Success();
        }
        catch (Exception exception)
        {
            _log.Error($"Product {productId} could not be deactivated.", exception);
            return OperationResult.Fail("The product could not be removed.");
        }
    }

    public List<Category> GetCategories()
    {
        using var connection = _db.Open();
        return _db.Query(connection,
            "SELECT id, name, parent_id FROM categories WHERE active = 1 ORDER BY name COLLATE NOCASE;",
            reader => new Category
            {
                Id = reader.Int("id"),
                Name = reader.Str("name"),
                ParentId = reader.NullInt("parent_id")
            });
    }

    public List<Company> GetCompanies()
    {
        using var connection = _db.Open();
        return _db.Query(connection,
            "SELECT id, name FROM companies WHERE active = 1 ORDER BY name COLLATE NOCASE;",
            reader => new Company { Id = reader.Int("id"), Name = reader.Str("name") });
    }

    private static Product MapProduct(SqliteDataReader reader) => new()
    {
        Id = reader.Int("id"),
        Name = reader.Str("name"),
        Barcode = reader.NullStr("barcode"),
        Generic = reader.NullStr("generic"),
        CategoryId = reader.NullInt("category_id"),
        CategoryName = reader.NullStr("category_name"),
        CompanyId = reader.NullInt("company_id"),
        CompanyName = reader.NullStr("company_name"),
        BoxStrips = reader.Int("box_strips"),
        StripUnits = reader.Int("strip_units"),
        CostPaisa = reader.Long("cost_paisa"),
        RetailPaisa = reader.Long("retail_paisa"),
        VipPaisa = reader.Long("vip_paisa"),
        DoctorPaisa = reader.Long("doctor_paisa"),
        Rack = reader.NullStr("rack"),
        Room = reader.NullStr("room"),
        ReorderLevel = reader.Int("reorder_level"),
        StockBase = reader.Long("stock_base"),
        PhotoFile = reader.NullStr("photo_file"),
        Active = reader.Bool("active")
    };

    private int? ResolveCategory(SqliteConnection connection, int? categoryId, string? newName)
    {
        var name = newName?.Trim();
        if (!string.IsNullOrEmpty(name))
        {
            var existing = _db.Scalar<long>(connection,
                "SELECT id FROM categories WHERE name = $name COLLATE NOCASE AND active = 1 LIMIT 1;",
                ("$name", name));

            if (existing > 0)
            {
                return (int)existing;
            }

            _db.Exec(connection, "INSERT INTO categories(name, active) VALUES($name, 1);", ("$name", name));
            return (int)_db.LastInsertId(connection);
        }

        return categoryId is > 0 ? categoryId : null;
    }

    private int? ResolveCompany(SqliteConnection connection, int? companyId, string? newName)
    {
        var name = newName?.Trim();
        if (!string.IsNullOrEmpty(name))
        {
            var existing = _db.Scalar<long>(connection,
                "SELECT id FROM companies WHERE name = $name COLLATE NOCASE AND active = 1 LIMIT 1;",
                ("$name", name));

            if (existing > 0)
            {
                return (int)existing;
            }

            _db.Exec(connection, "INSERT INTO companies(name, active) VALUES($name, 1);", ("$name", name));
            return (int)_db.LastInsertId(connection);
        }

        return companyId is > 0 ? companyId : null;
    }

    private static string RequireName(string? name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new ValidationException("Product name is required.");
        }

        return trimmed;
    }

    private static object NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
}
