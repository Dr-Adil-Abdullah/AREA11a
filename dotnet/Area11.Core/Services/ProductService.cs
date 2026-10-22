using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>
/// Catalog: products, categories, companies (Spec 8).
/// Stock is read from the batches, never stored on the product, so a stock-take, a sale or a
/// return can never leave two different quantities behind.
/// </summary>
public sealed class ProductService
{
    private const string SelectSql = @"
SELECT p.id, p.name, p.generic, p.brand, p.barcode, p.category_id, p.company_id,
       p.rack_no, p.room, p.pack_size_label, p.base_unit, p.box_strips, p.strip_tablets,
       p.cost_paisa, p.retail_paisa, p.vip_paisa, p.doctor_paisa, p.reorder_level,
       p.track_expiry, p.active,
       c.name AS category_name,
       co.name AS company_name,
       IFNULL((SELECT SUM(b.qty_base) FROM batches b WHERE b.product_id = p.id AND b.active = 1), 0) AS stock_base
FROM products p
LEFT JOIN categories c ON c.id = p.category_id
LEFT JOIN companies co ON co.id = p.company_id
";

    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly LogService _log;

    public ProductService(Database db, AuditService audit, LogService log)
    {
        _db = db;
        _audit = audit;
        _log = log;
    }

    public ServiceResult<List<Product>> Search(
        string? term = null,
        int? categoryId = null,
        int? companyId = null,
        bool lowStockOnly = false)
    {
        try
        {
            var text = (term ?? string.Empty).Trim();

            using var connection = _db.Open();
            var products = _db.Query<Product>(connection, SelectSql + @"
WHERE p.active = 1
  AND (@term = '' OR p.name LIKE @like OR IFNULL(p.generic, '') LIKE @like
       OR IFNULL(p.barcode, '') LIKE @like OR IFNULL(p.rack_no, '') LIKE @like
       OR EXISTS (SELECT 1 FROM product_barcodes pb
                  WHERE pb.product_id = p.id AND pb.active = 1 AND pb.barcode LIKE @like))
  AND (@categoryId IS NULL OR p.category_id = @categoryId)
  AND (@companyId IS NULL OR p.company_id = @companyId)
  AND (@lowStock = 0 OR
       IFNULL((SELECT SUM(b.qty_base) FROM batches b WHERE b.product_id = p.id AND b.active = 1), 0) <= p.reorder_level)
ORDER BY p.name COLLATE NOCASE;",
                new
                {
                    term = text,
                    like = $"%{text}%",
                    categoryId,
                    companyId,
                    lowStock = lowStockOnly ? 1 : 0
                });

            return ServiceResult<List<Product>>.Success(products);
        }
        catch (Exception exception)
        {
            _log.Error("The product list could not be loaded.", exception);
            return ServiceResult<List<Product>>.Fail("The product list could not be loaded.");
        }
    }

    public ServiceResult<Product> Get(int id)
    {
        try
        {
            using var connection = _db.Open();
            var product = _db.QuerySingle<Product>(connection, SelectSql + "WHERE p.id = @id;", new { id });

            return product is null
                ? ServiceResult<Product>.Fail("That product no longer exists.")
                : ServiceResult<Product>.Success(product);
        }
        catch (Exception exception)
        {
            _log.Error($"Product {id} could not be loaded.", exception);
            return ServiceResult<Product>.Fail("The product could not be loaded.");
        }
    }

    public ServiceResult<int> Create(User actor, ProductPatch input)
    {
        if (!actor.CanManage)
        {
            return ServiceResult<int>.Fail("Only the owner or a manager can add products.");
        }

        var name = (input.Name ?? string.Empty).Trim();
        if (name.Length < 2)
        {
            return ServiceResult<int>.Fail("Enter the medicine name.");
        }

        if (input.RetailPaisa is < 0 || input.CostPaisa is < 0)
        {
            return ServiceResult<int>.Fail("Rates cannot be negative.");
        }

        try
        {
            var id = _db.Tx(connection =>
            {
                _db.Execute(connection, @"
INSERT INTO products
    (name, generic, brand, barcode, category_id, company_id, rack_no, room, pack_size_label,
     base_unit, box_strips, strip_tablets, cost_paisa, retail_paisa, vip_paisa, doctor_paisa,
     reorder_level, track_expiry, active, created_at, updated_at)
VALUES
    (@name, @generic, @brand, @barcode, @categoryId, @companyId, @rackNo, @room, @packSizeLabel,
     @baseUnit, @boxStrips, @stripTablets, @costPaisa, @retailPaisa, @vipPaisa, @doctorPaisa,
     @reorderLevel, @trackExpiry, 1, @at, @at);",
                    new
                    {
                        name,
                        generic = NullIfEmpty(input.Generic),
                        brand = NullIfEmpty(input.Brand),
                        barcode = NullIfEmpty(input.Barcode),
                        categoryId = input.CategoryId,
                        companyId = input.CompanyId,
                        rackNo = NullIfEmpty(input.RackNo),
                        room = NullIfEmpty(input.Room),
                        packSizeLabel = NullIfEmpty(input.PackSizeLabel),
                        baseUnit = string.IsNullOrWhiteSpace(input.BaseUnit) ? "tablet" : input.BaseUnit.Trim(),
                        boxStrips = Math.Max(0, input.BoxStrips ?? 0),
                        stripTablets = Math.Max(0, input.StripTablets ?? 0),
                        costPaisa = Math.Max(0, input.CostPaisa ?? 0),
                        retailPaisa = Math.Max(0, input.RetailPaisa ?? 0),
                        vipPaisa = Math.Max(0, input.VipPaisa ?? 0),
                        doctorPaisa = Math.Max(0, input.DoctorPaisa ?? 0),
                        reorderLevel = Math.Max(0, input.ReorderLevel ?? 0),
                        trackExpiry = input.TrackExpiry is null or true ? 1 : 0,
                        at = Clock.Now()
                    });

                var newId = (int)_db.NextId(connection);

                _audit.Record(connection, actor, "Inventory", "product_create", "products", newId.ToString(),
                    newValue: $"{{\"name\":\"{name}\",\"retailPaisa\":{input.RetailPaisa ?? 0}}}");

                return newId;
            });

            return ServiceResult<int>.Success(id);
        }
        catch (SqliteException exception) when (exception.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<int>.Fail("That barcode already belongs to another product.");
        }
        catch (Exception exception)
        {
            _log.Error("A product could not be created.", exception);
            return ServiceResult<int>.Fail("The product could not be saved.");
        }
    }

    /// <summary>
    /// Partial update (Spec 8.1.4.1): only non-null fields are written, so changing the retail
    /// rate can never wipe the cost, the pack formula, the rack or the reorder level.
    /// </summary>
    public ServiceResult Update(User actor, int id, ProductPatch patch)
    {
        if (!actor.CanManage)
        {
            return ServiceResult.Fail("Only the owner or a manager can edit products.");
        }

        try
        {
            _db.Tx(connection =>
            {
                var before = _db.QuerySingle<Product>(connection, SelectSql + "WHERE p.id = @id;", new { id });
                if (before is null)
                {
                    throw new ValidationException("That product no longer exists.");
                }

                var sets = new List<string>();
                var args = new DynamicParameters();
                args.Add("id", id);
                args.Add("at", Clock.Now());

                void Set(string column, object? value, string parameter)
                {
                    sets.Add($"{column} = @{parameter}");
                    args.Add(parameter, value);
                }

                if (patch.Name is not null)
                {
                    var name = patch.Name.Trim();
                    if (name.Length < 2)
                    {
                        throw new ValidationException("The name is too short.");
                    }

                    Set("name", name, "name");
                }

                if (patch.Generic is not null)
                {
                    Set("generic", NullIfEmpty(patch.Generic), "generic");
                }

                if (patch.Brand is not null)
                {
                    Set("brand", NullIfEmpty(patch.Brand), "brand");
                }

                if (patch.Barcode is not null)
                {
                    Set("barcode", NullIfEmpty(patch.Barcode), "barcode");
                }

                if (patch.CategoryId is not null)
                {
                    Set("category_id", patch.CategoryId, "categoryId");
                }

                if (patch.CompanyId is not null)
                {
                    Set("company_id", patch.CompanyId, "companyId");
                }

                if (patch.RackNo is not null)
                {
                    Set("rack_no", NullIfEmpty(patch.RackNo), "rackNo");
                }

                if (patch.Room is not null)
                {
                    Set("room", NullIfEmpty(patch.Room), "room");
                }

                if (patch.PackSizeLabel is not null)
                {
                    Set("pack_size_label", NullIfEmpty(patch.PackSizeLabel), "packSizeLabel");
                }

                if (patch.BaseUnit is not null)
                {
                    Set("base_unit", patch.BaseUnit.Trim(), "baseUnit");
                }

                if (patch.BoxStrips is not null)
                {
                    Set("box_strips", Math.Max(0, patch.BoxStrips.Value), "boxStrips");
                }

                if (patch.StripTablets is not null)
                {
                    Set("strip_tablets", Math.Max(0, patch.StripTablets.Value), "stripTablets");
                }

                if (patch.CostPaisa is not null)
                {
                    Set("cost_paisa", Math.Max(0, patch.CostPaisa.Value), "costPaisa");
                }

                if (patch.RetailPaisa is not null)
                {
                    Set("retail_paisa", Math.Max(0, patch.RetailPaisa.Value), "retailPaisa");
                }

                if (patch.VipPaisa is not null)
                {
                    Set("vip_paisa", Math.Max(0, patch.VipPaisa.Value), "vipPaisa");
                }

                if (patch.DoctorPaisa is not null)
                {
                    Set("doctor_paisa", Math.Max(0, patch.DoctorPaisa.Value), "doctorPaisa");
                }

                if (patch.ReorderLevel is not null)
                {
                    Set("reorder_level", Math.Max(0, patch.ReorderLevel.Value), "reorderLevel");
                }

                if (patch.TrackExpiry is not null)
                {
                    Set("track_expiry", patch.TrackExpiry.Value ? 1 : 0, "trackExpiry");
                }

                if (patch.Active is not null)
                {
                    Set("active", patch.Active.Value ? 1 : 0, "active");
                }

                if (sets.Count == 0)
                {
                    return;
                }

                sets.Add("updated_at = @at");

                _db.Execute(connection,
                    $"UPDATE products SET {string.Join(", ", sets)} WHERE id = @id;",
                    args);

                var after = _db.QuerySingle<Product>(connection, SelectSql + "WHERE p.id = @id;", new { id });

                _audit.RecordChange(connection, actor, "Inventory", "product_edit", "products", id.ToString(),
                    before, after);

                if (before.RetailPaisa != after?.RetailPaisa || before.CostPaisa != after?.CostPaisa)
                {
                    _audit.Record(connection, actor, "Inventory", "price_change", "products", id.ToString(),
                        oldValue: $"{{\"cost\":{before.CostPaisa},\"retail\":{before.RetailPaisa}}}",
                        newValue: $"{{\"cost\":{after?.CostPaisa ?? 0},\"retail\":{after?.RetailPaisa ?? 0}}}");
                }
            });

            return ServiceResult.Success();
        }
        catch (ValidationException exception)
        {
            return ServiceResult.Fail(exception.Message);
        }
        catch (SqliteException exception) when (exception.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult.Fail("That barcode already belongs to another product.");
        }
        catch (Exception exception)
        {
            _log.Error($"Product {id} could not be updated.", exception);
            return ServiceResult.Fail("The product could not be saved.");
        }
    }

    /// <summary>Soft delete only — history, batches and bills stay intact (Spec 5.1.5).</summary>
    public ServiceResult Deactivate(User actor, int id)
    {
        if (!actor.CanManage)
        {
            return ServiceResult.Fail("Only the owner or a manager can remove products.");
        }

        try
        {
            _db.Tx(connection =>
            {
                _db.Execute(connection, "UPDATE products SET active = 0, updated_at = @at WHERE id = @id;",
                    new { at = Clock.Now(), id });

                _audit.Record(connection, actor, "Inventory", "product_deactivate", "products", id.ToString(),
                    oldValue: "active: 1", newValue: "active: 0");
            });

            return ServiceResult.Success();
        }
        catch (Exception exception)
        {
            _log.Error($"Product {id} could not be deactivated.", exception);
            return ServiceResult.Fail("The product could not be removed.");
        }
    }

    public ServiceResult<List<Category>> GetCategories()
    {
        try
        {
            using var connection = _db.Open();
            var rows = _db.Query<Category>(connection,
                "SELECT id, name, parent_id, sort_order, active FROM categories WHERE active = 1 ORDER BY sort_order, name COLLATE NOCASE;");
            return ServiceResult<List<Category>>.Success(rows);
        }
        catch (Exception exception)
        {
            _log.Error("Categories could not be loaded.", exception);
            return ServiceResult<List<Category>>.Fail("Categories could not be loaded.");
        }
    }

    public ServiceResult<List<Company>> GetCompanies()
    {
        try
        {
            using var connection = _db.Open();
            var rows = _db.Query<Company>(connection,
                "SELECT id, name, phone, notes, active FROM companies WHERE active = 1 ORDER BY name COLLATE NOCASE;");
            return ServiceResult<List<Company>>.Success(rows);
        }
        catch (Exception exception)
        {
            _log.Error("Companies could not be loaded.", exception);
            return ServiceResult<List<Company>>.Fail("Companies could not be loaded.");
        }
    }

    /// <summary>“＋ New” inside the product form (U-18).</summary>
    public ServiceResult<int> AddCategory(User actor, string name)
    {
        if (!actor.CanManage)
        {
            return ServiceResult<int>.Fail("Only the owner or a manager can add categories.");
        }

        var clean = (name ?? string.Empty).Trim();
        if (clean.Length == 0)
        {
            return ServiceResult<int>.Fail("Enter a category name.");
        }

        try
        {
            var id = _db.Tx(connection =>
            {
                _db.Execute(connection,
                    "INSERT OR IGNORE INTO categories(name, sort_order, active, created_at) VALUES(@name, 0, 1, @at);",
                    new { name = clean, at = Clock.Now() });

                var newId = Convert.ToInt32(_db.Scalar(connection,
                    "SELECT id FROM categories WHERE name = @name;", new { name = clean }) ?? 0);

                _audit.Record(connection, actor, "Inventory", "category_create", "categories",
                    newId.ToString(), newValue: $"{{\"name\":\"{clean}\"}}");

                return newId;
            });

            return ServiceResult<int>.Success(id);
        }
        catch (Exception exception)
        {
            _log.Error($"Category '{clean}' could not be added.", exception);
            return ServiceResult<int>.Fail("The category could not be saved.");
        }
    }

    public ServiceResult<int> AddCompany(User actor, string name)
    {
        if (!actor.CanManage)
        {
            return ServiceResult<int>.Fail("Only the owner or a manager can add companies.");
        }

        var clean = (name ?? string.Empty).Trim();
        if (clean.Length == 0)
        {
            return ServiceResult<int>.Fail("Enter a company name.");
        }

        try
        {
            var id = _db.Tx(connection =>
            {
                _db.Execute(connection,
                    "INSERT OR IGNORE INTO companies(name, active) VALUES(@name, 1);",
                    new { name = clean });

                var newId = Convert.ToInt32(_db.Scalar(connection,
                    "SELECT id FROM companies WHERE name = @name;", new { name = clean }) ?? 0);

                _audit.Record(connection, actor, "Inventory", "company_create", "companies",
                    newId.ToString(), newValue: $"{{\"name\":\"{clean}\"}}");

                return newId;
            });

            return ServiceResult<int>.Success(id);
        }
        catch (Exception exception)
        {
            _log.Error($"Company '{clean}' could not be added.", exception);
            return ServiceResult<int>.Fail("The company could not be saved.");
        }
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
