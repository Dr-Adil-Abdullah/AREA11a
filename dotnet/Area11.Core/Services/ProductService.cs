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
       p.strength_value, p.strength_unit,
       p.cost_paisa, p.retail_paisa, p.vip_paisa, p.doctor_paisa, p.reorder_level,
       p.track_expiry, p.active,
       c.name AS category_name,
       (SELECT GROUP_CONCAT(tagged.name, ', ')
        FROM product_category_tags pct
        JOIN categories tagged ON tagged.id = pct.category_id AND tagged.active = 1
        WHERE pct.product_id = p.id) AS category_tags,
       co.name AS company_name,
       (SELECT ph.file_name FROM photos ph
        WHERE ph.entity = 'product' AND ph.entity_id = p.id AND IFNULL(ph.active, 1) = 1
        ORDER BY ph.sort_order, ph.id LIMIT 1) AS photo_file_name,
       CAST(IFNULL((SELECT SUM(b.qty_base) FROM batches b WHERE b.product_id = p.id AND b.active = 1), 0) AS REAL) AS stock_base
FROM products p
LEFT JOIN categories c ON c.id = p.category_id
LEFT JOIN companies co ON co.id = p.company_id
";

    private const int MaxCatalogNameLength = 120;
    private const int MaxCatalogSortOrder = 1_000_000_000;
    private const string SortOrderError = "Order must be a whole number from 0 to 1,000,000,000.";

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
        bool lowStockOnly = false,
        string? strength = null)
    {
        try
        {
            var text = (term ?? string.Empty).Trim();
            var searchKey = PackagingPolicy.SearchKey(text);
            var strengthText = NormalizeStrengthFilter(strength);

            using var connection = _db.Open();
            var products = _db.Query<Product>(connection, SelectSql + @"
WHERE p.active = 1
  AND (@term = '' OR p.name LIKE @like OR IFNULL(p.generic, '') LIKE @like
       OR IFNULL(p.brand, '') LIKE @like OR IFNULL(p.barcode, '') LIKE @like
       OR IFNULL(p.rack_no, '') LIKE @like OR IFNULL(p.room, '') LIKE @like
       OR IFNULL(p.pack_size_label, '') LIKE @like OR IFNULL(p.base_unit, '') LIKE @like
       OR IFNULL(c.name, '') LIKE @like OR IFNULL(co.name, '') LIKE @like
       OR IFNULL(p.strength_unit, '') LIKE @like
       OR (p.strength_value > 0 AND printf('%g', p.strength_value) LIKE @like)
       OR (p.strength_value > 0 AND (printf('%g', p.strength_value) || ' ' || p.strength_unit) LIKE @like)
       OR (@searchKey <> '' AND replace(replace(lower(IFNULL(p.pack_size_label, '')), ' ', ''), '×', 'x') LIKE @keyLike)
       OR (@searchKey <> '' AND p.strength_value > 0
           AND replace(replace(lower(printf('%g', p.strength_value) || ' ' || IFNULL(p.strength_unit, '')),
                       ' ', ''), '×', 'x') LIKE @keyLike)
       OR EXISTS (SELECT 1 FROM product_barcodes pb
                  WHERE pb.product_id = p.id AND pb.active = 1 AND pb.barcode LIKE @like)
       OR EXISTS (SELECT 1 FROM product_category_tags search_tags
                  JOIN categories search_category ON search_category.id = search_tags.category_id
                  WHERE search_tags.product_id = p.id AND search_category.active = 1
                    AND search_category.name LIKE @like))
  AND (@categoryId IS NULL OR p.category_id = @categoryId OR EXISTS (
      SELECT 1 FROM categories child
      WHERE child.id = p.category_id AND child.parent_id = @categoryId)
       OR EXISTS (
      SELECT 1 FROM product_category_tags pct
      JOIN categories tagged ON tagged.id = pct.category_id AND tagged.active = 1
      WHERE pct.product_id = p.id
        AND (pct.category_id = @categoryId OR tagged.parent_id = @categoryId)))
  AND (@companyId IS NULL OR p.company_id = @companyId)
  AND (@strength = '' OR
       lower(trim(p.strength_unit)) = @strength OR
       (p.strength_value > 0 AND printf('%g', p.strength_value) = @strength) OR
       (p.strength_value > 0 AND replace(lower(printf('%g', p.strength_value) || ' ' || trim(p.strength_unit)), ' ', '') = replace(@strength, ' ', '')))
  AND (@lowStock = 0 OR
       (IFNULL((SELECT SUM(b.qty_base) FROM batches b WHERE b.product_id = p.id AND b.active = 1), 0) >= 0
        AND IFNULL((SELECT SUM(b.qty_base) FROM batches b WHERE b.product_id = p.id AND b.active = 1), 0) <= p.reorder_level))
ORDER BY p.name COLLATE NOCASE;",
                new
                {
                    term = text,
                    like = $"%{text}%",
                    searchKey,
                    keyLike = $"%{searchKey}%",
                    categoryId,
                    companyId,
                    strength = strengthText,
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

    /// <summary>
    /// The search the counter and the product list use (F-50, Spec 8.4): the exact search first, and -
    /// only when it found nothing - the closest medicines to what was typed.
    ///
    /// Why "only when it found nothing": an exact answer is never second-guessed. A search that
    /// quietly mixes a guess into a good result is how a cashier learns not to trust the list; a
    /// search that says "nothing is spelt that way, did you mean these" is how he learns to use it.
    ///
    /// Why the fallback is limited to a pharmacy-sized catalog: a shop has a few thousand rows, and
    /// ranking them in memory costs less than the round trip that would be needed to ask SQL to do it.
    /// </summary>
    public ServiceResult<ProductSearchResult> SearchSmart(
        string? term = null,
        int? categoryId = null,
        int? companyId = null,
        bool lowStockOnly = false,
        string? strength = null)
    {
        var typed = (term ?? string.Empty).Trim();

        var exact = Search(typed, categoryId, companyId, lowStockOnly, strength);

        if (!exact.Ok || exact.Value is null)
        {
            return ServiceResult<ProductSearchResult>.Fail(exact.Error ?? "The product list could not be loaded.");
        }

        var answer = new ProductSearchResult { Typed = typed };
        answer.Items.AddRange(exact.Value);

        if (answer.Items.Count > 0 || Fuzzy.Words(typed).Length == 0)
        {
            return ServiceResult<ProductSearchResult>.Success(answer);
        }

        // Nothing exact: look for the neighbourhood. The filters still apply, because a person who
        // narrowed the list to one company does not want a suggestion from another one.
        var all = Search(null, categoryId, companyId, lowStockOnly, strength);

        if (!all.Ok || all.Value is null)
        {
            return ServiceResult<ProductSearchResult>.Success(answer);
        }

        var close = Fuzzy.Rank(typed, all.Value, product => product.Name, product => product.Generic);

        if (close.Count == 0)
        {
            return ServiceResult<ProductSearchResult>.Success(answer);
        }

        answer.IsFuzzy = true;
        answer.ClosestName = close[0].Item.Name;
        answer.NoteLabel = Fuzzy.SuggestionLabel(typed, answer.ClosestName);

        foreach (var hit in close)
        {
            answer.Items.Add(hit.Item);
        }

        return ServiceResult<ProductSearchResult>.Success(answer);
    }

    public ServiceResult<Product> Get(int id)
    {
        try
        {
            using var connection = _db.Open();
            var product = _db.QuerySingle<Product>(connection, SelectSql + "WHERE p.id = @id;", new { id });
            if (product is not null)
            {
                product.CategoryTagIds = _db.Query<int>(connection,
                    "SELECT category_id FROM product_category_tags WHERE product_id = @id ORDER BY category_id;",
                    new { id }).ToList();
            }

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

        var packSizeError = PackagingPolicy.PackSizeError(input.PackSizeLabel);
        if (packSizeError is not null)
        {
            return ServiceResult<int>.Fail(packSizeError);
        }
        var packSizeLabel = PackagingPolicy.CleanPackSize(input.PackSizeLabel);

        var baseUnit = NormalizeUnitName(input.BaseUnit) is { Length: > 0 } cleanBase ? cleanBase : "tablet";
        var strengthUnit = NormalizeUnitName(input.StrengthUnit);
        var strengthValue = input.StrengthValue ?? 0;
        var strengthError = StrengthError(strengthValue, strengthUnit);
        if (strengthError is not null)
        {
            return ServiceResult<int>.Fail(strengthError);
        }
        if (strengthValue == 0)
        {
            strengthUnit = string.Empty;
        }

        try
        {
            var id = _db.Tx(connection =>
            {
                EnsureActiveCatalogLinks(connection, input.CategoryId, input.CompanyId);
                EnsureActiveUnit(connection, baseUnit);
                if (strengthValue > 0)
                {
                    EnsureActiveUnit(connection, strengthUnit);
                }

                _db.Execute(connection, @"
INSERT INTO products
    (name, generic, brand, barcode, category_id, company_id, rack_no, room, pack_size_label,
     base_unit, strength_value, strength_unit, box_strips, strip_tablets,
     cost_paisa, retail_paisa, vip_paisa, doctor_paisa,
     reorder_level, track_expiry, active, created_at, updated_at)
VALUES
    (@name, @generic, @brand, @barcode, @categoryId, @companyId, @rackNo, @room, @packSizeLabel,
     @baseUnit, @strengthValue, @strengthUnit, @boxStrips, @stripTablets,
     @costPaisa, @retailPaisa, @vipPaisa, @doctorPaisa,
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
                        packSizeLabel = NullIfEmpty(packSizeLabel),
                        baseUnit,
                        strengthValue,
                        strengthUnit,
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
                SaveCategoryTags(connection, newId, input.CategoryId, input.CategoryTagIds);

                _audit.Record(connection, actor, "Inventory", "product_create", "products", newId.ToString(),
                    newValue: $"{{\"name\":\"{name}\",\"retailPaisa\":{input.RetailPaisa ?? 0}}}");

                return newId;
            });

            return ServiceResult<int>.Success(id);
        }
        catch (ValidationException exception)
        {
            return ServiceResult<int>.Fail(exception.Message);
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

                before.CategoryTagIds = _db.Query<int>(connection,
                    "SELECT category_id FROM product_category_tags WHERE product_id = @id ORDER BY category_id;",
                    new { id }).ToList();

                EnsureActiveCatalogLinks(connection, patch.CategoryId, patch.CompanyId);
                var effectiveCategoryId = patch.CategoryId ?? before.CategoryId;
                var effectiveTagIds = patch.CategoryTagIds ?? before.CategoryTagIds;
                if (effectiveCategoryId is int primary && effectiveTagIds.Contains(primary))
                {
                    throw new ValidationException("The primary category cannot also be an additional category tag.");
                }
                if (patch.BaseUnit is not null)
                {
                    var cleanBase = NormalizeUnitName(patch.BaseUnit);
                    if (cleanBase.Length == 0)
                    {
                        throw new ValidationException("Choose a base unit from the managed list.");
                    }
                    EnsureActiveUnit(connection, cleanBase);
                }

                var effectiveStrength = patch.StrengthValue ?? before.StrengthValue;
                var effectiveStrengthUnit = patch.StrengthUnit is null
                    ? before.StrengthUnit
                    : NormalizeUnitName(patch.StrengthUnit);
                var strengthError = StrengthError(effectiveStrength, effectiveStrengthUnit);
                if (strengthError is not null)
                {
                    throw new ValidationException(strengthError);
                }
                if (effectiveStrength > 0)
                {
                    EnsureActiveUnit(connection, NormalizeUnitName(effectiveStrengthUnit));
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
                    var packSizeError = PackagingPolicy.PackSizeError(patch.PackSizeLabel);
                    if (packSizeError is not null)
                    {
                        throw new ValidationException(packSizeError);
                    }

                    Set("pack_size_label",
                        NullIfEmpty(PackagingPolicy.CleanPackSize(patch.PackSizeLabel)), "packSizeLabel");
                }

                if (patch.BaseUnit is not null)
                {
                    Set("base_unit", NormalizeUnitName(patch.BaseUnit), "baseUnit");
                }

                if (patch.StrengthValue is not null || patch.StrengthUnit is not null)
                {
                    Set("strength_value", effectiveStrength, "strengthValue");
                    Set("strength_unit", effectiveStrength == 0 ? string.Empty : NormalizeUnitName(effectiveStrengthUnit), "strengthUnit");
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

                if (patch.CategoryTagIds is not null)
                {
                    SaveCategoryTags(connection, id, effectiveCategoryId, patch.CategoryTagIds);
                }

                if (sets.Count == 0 && patch.CategoryTagIds is null)
                {
                    return;
                }

                // Tag-only edits are still product edits: keep the change visible to audit,
                // workbook snapshots and any reader that uses updated_at for freshness.
                sets.Add("updated_at = @at");
                _db.Execute(connection,
                    $"UPDATE products SET {string.Join(", ", sets)} WHERE id = @id;",
                    args);

                var after = _db.QuerySingle<Product>(connection, SelectSql + "WHERE p.id = @id;", new { id });
                if (after is not null)
                {
                    after.CategoryTagIds = _db.Query<int>(connection,
                        "SELECT category_id FROM product_category_tags WHERE product_id = @id ORDER BY category_id;",
                        new { id }).ToList();
                }

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

    public ServiceResult<List<Category>> GetCategories(bool activeOnly = true)
    {
        try
        {
            using var connection = _db.Open();
            var rows = _db.Query<Category>(connection, @"
SELECT c.id, c.name, c.parent_id AS parentId, c.sort_order AS sortOrder, c.active,
       parent.name AS parentName,
       (SELECT COUNT(DISTINCT p.id) FROM products p
        LEFT JOIN product_category_tags pct ON pct.product_id = p.id AND pct.category_id = c.id
        WHERE p.category_id = c.id OR pct.category_id = c.id) AS productCount
FROM categories c
LEFT JOIN categories parent ON parent.id = c.parent_id
" + (activeOnly ? "WHERE c.active = 1 " : string.Empty)
                + "ORDER BY c.sort_order, c.name COLLATE NOCASE, c.id;");
            return ServiceResult<List<Category>>.Success(rows);
        }
        catch (Exception exception)
        {
            _log.Error("Categories could not be loaded.", exception);
            return ServiceResult<List<Category>>.Fail("Categories could not be loaded.");
        }
    }

    public ServiceResult<List<Company>> GetCompanies(bool activeOnly = true)
    {
        try
        {
            using var connection = _db.Open();
            var rows = _db.Query<Company>(connection, @"
SELECT c.id, c.name, c.phone, c.notes, c.rep_name AS repName, c.rep_phone AS repPhone,
       c.visit_day AS visitDay, c.active, c.sort_order AS sortOrder,
       (SELECT COUNT(1) FROM products p WHERE p.company_id = c.id) AS productCount
FROM companies c
" + (activeOnly ? "WHERE c.active = 1 " : string.Empty)
                + "ORDER BY c.sort_order, c.name COLLATE NOCASE, c.id;");
            return ServiceResult<List<Company>>.Success(rows);
        }
        catch (Exception exception)
        {
            _log.Error("Companies could not be loaded.", exception);
            return ServiceResult<List<Company>>.Fail("Companies could not be loaded.");
        }
    }

    /// <summary>“＋ New” inside the product form (U-18), also used by catalog management (E-04).</summary>
    public ServiceResult<int> AddCategory(
        User actor,
        string? name,
        int? parentId = null,
        int? sortOrder = null)
    {
        if (!actor.CanManage)
        {
            return ServiceResult<int>.Fail("Only the owner or a manager can change categories.");
        }

        var clean = CleanCatalogName(name);
        if (clean.Length == 0)
        {
            return ServiceResult<int>.Fail("Enter a category name.");
        }

        if (clean.Length > MaxCatalogNameLength)
        {
            return ServiceResult<int>.Fail("Keep the category name under 120 characters.");
        }

        if (sortOrder is not null && !IsValidSortOrder(sortOrder.Value))
        {
            return ServiceResult<int>.Fail(SortOrderError);
        }

        try
        {
            return _db.Tx(connection =>
            {
                if (CatalogNameTaken(connection, "categories", clean, 0))
                {
                    return ServiceResult<int>.Fail(
                        "That category name is already used, including by a category put away.");
                }

                var parentError = CategoryParentError(connection, 0, parentId, null);
                if (parentError is not null)
                {
                    return ServiceResult<int>.Fail(parentError);
                }

                var order = sortOrder ?? NextSortOrder(connection, "categories");
                if (!IsValidSortOrder(order))
                {
                    return ServiceResult<int>.Fail(SortOrderError);
                }

                _db.Execute(connection, @"
INSERT INTO categories(name, parent_id, sort_order, active, created_at)
VALUES(@name, @parentId, @sortOrder, 1, @at);",
                    new { name = clean, parentId, sortOrder = order, at = Clock.Now() });

                var id = Convert.ToInt32(_db.Scalar(connection,
                    "SELECT id FROM categories WHERE rowid = last_insert_rowid();") ?? 0);
                var after = FindCategory(connection, id);
                _audit.RecordChange(connection, actor, "Inventory", "category_create", "categories",
                    id.ToString(), null, after);
                return ServiceResult<int>.Success(id);
            });
        }
        catch (Exception exception)
        {
            _log.Error($"Category '{clean}' could not be added.", exception);
            return ServiceResult<int>.Fail("The category could not be saved. " + exception.Message);
        }
    }

    /// <summary>Rename, choose a top-level parent, or change order without touching product history.</summary>
    public ServiceResult UpdateCategory(User actor, int id, string? name, int? parentId, int sortOrder)
    {
        if (!actor.CanManage)
        {
            return ServiceResult.Fail("Only the owner or a manager can change categories.");
        }

        var clean = CleanCatalogName(name);
        if (id <= 0 || clean.Length == 0)
        {
            return ServiceResult.Fail("Pick a category and enter its name.");
        }

        if (clean.Length > MaxCatalogNameLength)
        {
            return ServiceResult.Fail("Keep the category name under 120 characters.");
        }

        if (!IsValidSortOrder(sortOrder))
        {
            return ServiceResult.Fail(SortOrderError);
        }

        try
        {
            return _db.Tx(connection =>
            {
                var before = FindCategory(connection, id);
                if (before is null)
                {
                    return ServiceResult.Fail("That category no longer exists.");
                }

                if (CatalogNameTaken(connection, "categories", clean, id))
                {
                    return ServiceResult.Fail(
                        "That category name is already used, including by a category put away.");
                }

                var parentError = CategoryParentError(connection, id, parentId, before.ParentId);
                if (parentError is not null)
                {
                    return ServiceResult.Fail(parentError);
                }

                if (before.Name == clean && before.ParentId == parentId && before.SortOrder == sortOrder)
                {
                    return ServiceResult.Success();
                }

                _db.Execute(connection, @"
UPDATE categories SET name = @name, parent_id = @parentId, sort_order = @sortOrder
WHERE id = @id;",
                    new { name = clean, parentId, sortOrder, id });

                var after = FindCategory(connection, id);
                _audit.RecordChange(connection, actor, "Inventory", "category_update", "categories",
                    id.ToString(), before, after);
                return ServiceResult.Success();
            });
        }
        catch (Exception exception)
        {
            _log.Error($"Category {id} could not be updated.", exception);
            return ServiceResult.Fail("The category could not be saved. " + exception.Message);
        }
    }

    /// <summary>
    /// Soft put-away only. Products linked directly to this category become uncategorised; child
    /// categories, products, batches, bills and stock movements remain. Restore never invents links.
    /// </summary>
    public ServiceResult SetCategoryActive(User actor, int id, bool active)
    {
        if (!actor.CanManage)
        {
            return ServiceResult.Fail("Only the owner or a manager can change categories.");
        }

        try
        {
            return _db.Tx(connection =>
            {
                var before = FindCategory(connection, id);
                if (before is null)
                {
                    return ServiceResult.Fail("That category no longer exists.");
                }

                if (before.Active == active)
                {
                    return ServiceResult.Success();
                }

                if (active && CatalogNameTaken(connection, "categories", before.Name, id))
                {
                    return ServiceResult.Fail("Another category uses this name. Rename one before bringing it back.");
                }

                var unlinkedIds = new List<int>();
                var removedTagLinks = 0;
                if (!active)
                {
                    unlinkedIds = _db.Query<int>(connection,
                        "SELECT id FROM products WHERE category_id = @id ORDER BY id;", new { id });
                    removedTagLinks = Convert.ToInt32(_db.Scalar(connection,
                        "SELECT COUNT(1) FROM product_category_tags WHERE category_id = @id;", new { id }) ?? 0);
                    _db.Execute(connection, @"
UPDATE products SET category_id = NULL, updated_at = @at WHERE category_id = @id;",
                        new { at = Clock.Now(), id });
                    _db.Execute(connection,
                        "DELETE FROM product_category_tags WHERE category_id = @id;", new { id });
                }

                _db.Execute(connection, "UPDATE categories SET active = @active WHERE id = @id;",
                    new { active, id });
                var after = FindCategory(connection, id);
                var details = active
                    ? "Brought back. Former product and filter-tag links were not recreated."
                    : $"Put away; unlinked {unlinkedIds.Count} primary product(s) [{string.Join(", ", unlinkedIds)}] and removed {removedTagLinks} filter tag link(s). No product or historical record was deleted.";
                _audit.RecordChange(connection, actor, "Inventory",
                    active ? "category_restore" : "category_put_away", "categories", id.ToString(),
                    before, after, details);
                return ServiceResult.Success();
            });
        }
        catch (Exception exception)
        {
            _log.Error($"Category {id} active state could not be changed.", exception);
            return ServiceResult.Fail("The category could not be changed. " + exception.Message);
        }
    }

    public ServiceResult<int> AddCompany(User actor, string? name, int? sortOrder = null)
    {
        if (!actor.CanManage)
        {
            return ServiceResult<int>.Fail("Only the owner or a manager can change companies.");
        }

        var clean = CleanCatalogName(name);
        if (clean.Length == 0)
        {
            return ServiceResult<int>.Fail("Enter a company name.");
        }

        if (clean.Length > MaxCatalogNameLength)
        {
            return ServiceResult<int>.Fail("Keep the company name under 120 characters.");
        }

        if (sortOrder is not null && !IsValidSortOrder(sortOrder.Value))
        {
            return ServiceResult<int>.Fail(SortOrderError);
        }

        try
        {
            return _db.Tx(connection =>
            {
                if (CatalogNameTaken(connection, "companies", clean, 0))
                {
                    return ServiceResult<int>.Fail(
                        "That company name is already used, including by a company put away.");
                }

                var order = sortOrder ?? NextSortOrder(connection, "companies");
                if (!IsValidSortOrder(order))
                {
                    return ServiceResult<int>.Fail(SortOrderError);
                }

                _db.Execute(connection, @"
INSERT INTO companies(name, sort_order, active) VALUES(@name, @sortOrder, 1);",
                    new { name = clean, sortOrder = order });
                var id = Convert.ToInt32(_db.Scalar(connection,
                    "SELECT id FROM companies WHERE rowid = last_insert_rowid();") ?? 0);
                var after = FindCompany(connection, id);
                _audit.RecordChange(connection, actor, "Inventory", "company_create", "companies",
                    id.ToString(), null, after);
                return ServiceResult<int>.Success(id);
            });
        }
        catch (Exception exception)
        {
            _log.Error($"Company '{clean}' could not be added.", exception);
            return ServiceResult<int>.Fail("The company could not be saved. " + exception.Message);
        }
    }

    /// <summary>Only name and order change; phone, notes and the medical rep stay on the row.</summary>
    public ServiceResult UpdateCompany(User actor, int id, string? name, int sortOrder)
    {
        if (!actor.CanManage)
        {
            return ServiceResult.Fail("Only the owner or a manager can change companies.");
        }

        var clean = CleanCatalogName(name);
        if (id <= 0 || clean.Length == 0)
        {
            return ServiceResult.Fail("Pick a company and enter its name.");
        }

        if (clean.Length > MaxCatalogNameLength)
        {
            return ServiceResult.Fail("Keep the company name under 120 characters.");
        }

        if (!IsValidSortOrder(sortOrder))
        {
            return ServiceResult.Fail(SortOrderError);
        }

        try
        {
            return _db.Tx(connection =>
            {
                var before = FindCompany(connection, id);
                if (before is null)
                {
                    return ServiceResult.Fail("That company no longer exists.");
                }

                if (CatalogNameTaken(connection, "companies", clean, id))
                {
                    return ServiceResult.Fail(
                        "That company name is already used, including by a company put away.");
                }

                if (before.Name == clean && before.SortOrder == sortOrder)
                {
                    return ServiceResult.Success();
                }

                _db.Execute(connection,
                    "UPDATE companies SET name = @name, sort_order = @sortOrder WHERE id = @id;",
                    new { name = clean, sortOrder, id });
                var after = FindCompany(connection, id);
                _audit.RecordChange(connection, actor, "Inventory", "company_update", "companies",
                    id.ToString(), before, after);
                return ServiceResult.Success();
            });
        }
        catch (Exception exception)
        {
            _log.Error($"Company {id} could not be updated.", exception);
            return ServiceResult.Fail("The company could not be saved. " + exception.Message);
        }
    }

    /// <summary>Soft put-away only; restore deliberately does not recreate former product links.</summary>
    public ServiceResult SetCompanyActive(User actor, int id, bool active)
    {
        if (!actor.CanManage)
        {
            return ServiceResult.Fail("Only the owner or a manager can change companies.");
        }

        try
        {
            return _db.Tx(connection =>
            {
                var before = FindCompany(connection, id);
                if (before is null)
                {
                    return ServiceResult.Fail("That company no longer exists.");
                }

                if (before.Active == active)
                {
                    return ServiceResult.Success();
                }

                if (active && CatalogNameTaken(connection, "companies", before.Name, id))
                {
                    return ServiceResult.Fail("Another company uses this name. Rename one before bringing it back.");
                }

                var unlinkedIds = new List<int>();
                if (!active)
                {
                    unlinkedIds = _db.Query<int>(connection,
                        "SELECT id FROM products WHERE company_id = @id ORDER BY id;", new { id });
                    _db.Execute(connection, @"
UPDATE products SET company_id = NULL, updated_at = @at WHERE company_id = @id;",
                        new { at = Clock.Now(), id });
                }

                _db.Execute(connection, "UPDATE companies SET active = @active WHERE id = @id;",
                    new { active, id });
                var after = FindCompany(connection, id);
                var details = active
                    ? "Brought back. Former product links were not recreated."
                    : $"Put away; unlinked {unlinkedIds.Count} product(s) [{string.Join(", ", unlinkedIds)}]. No product or historical record was deleted.";
                _audit.RecordChange(connection, actor, "Inventory",
                    active ? "company_restore" : "company_put_away", "companies", id.ToString(),
                    before, after, details);
                return ServiceResult.Success();
            });
        }
        catch (Exception exception)
        {
            _log.Error($"Company {id} active state could not be changed.", exception);
            return ServiceResult.Fail("The company could not be changed. " + exception.Message);
        }
    }

    /// <summary>Managed units used by product stock and descriptive strength fields.</summary>
    public ServiceResult<List<Unit>> GetUnits(bool activeOnly = true)
    {
        try
        {
            using var connection = _db.Open();
            var rows = _db.Query<Unit>(connection, @"
SELECT u.id, u.name, u.active, u.whole_only AS wholeOnly, u.sort_order AS sortOrder,
       (SELECT COUNT(1) FROM products p WHERE LOWER(TRIM(p.base_unit)) = u.name) AS productCount,
       (SELECT COUNT(1) FROM products p
        WHERE p.strength_value > 0 AND LOWER(TRIM(p.strength_unit)) = u.name) AS strengthCount
FROM units u
" + (activeOnly ? "WHERE u.active = 1 " : string.Empty)
                + "ORDER BY u.sort_order, u.name COLLATE NOCASE, u.id;");
            return ServiceResult<List<Unit>>.Success(rows);
        }
        catch (Exception exception)
        {
            _log.Error("Units could not be loaded.", exception);
            return ServiceResult<List<Unit>>.Fail("Units could not be loaded.");
        }
    }

    public ServiceResult<int> AddUnit(User actor, string? name, int? sortOrder = null, bool wholeOnly = false)
    {
        if (!actor.CanManage)
        {
            return ServiceResult<int>.Fail("Only the owner or a manager can change units.");
        }

        var clean = NormalizeUnitName(name);
        if (clean.Length == 0)
        {
            return ServiceResult<int>.Fail("Enter a unit name, for example injection or ml.");
        }
        if (clean.Length > 40)
        {
            return ServiceResult<int>.Fail("Keep the unit name under 40 characters.");
        }

        try
        {
            return _db.Tx(connection =>
            {
                if (UnitNameTaken(connection, clean, 0))
                {
                    return ServiceResult<int>.Fail("That unit is already on the list, including a put-away unit.");
                }

                var order = sortOrder ?? NextSortOrder(connection, "units");
                if (!IsValidSortOrder(order))
                {
                    return ServiceResult<int>.Fail(SortOrderError);
                }

                _db.Execute(connection,
                    "INSERT INTO units(name, active, whole_only, sort_order) VALUES(@name, 1, @wholeOnly, @order);",
                    new { name = clean, wholeOnly, order });
                var id = Convert.ToInt32(_db.Scalar(connection,
                    "SELECT id FROM units WHERE rowid = last_insert_rowid();") ?? 0);
                _audit.RecordChange(connection, actor, "Inventory", "unit_create", "units",
                    id.ToString(), null, FindUnit(connection, id));
                return ServiceResult<int>.Success(id);
            });
        }
        catch (Exception exception)
        {
            _log.Error("A unit could not be added.", exception);
            return ServiceResult<int>.Fail("The unit could not be saved. " + exception.Message);
        }
    }

    /// <summary>Renames current product labels atomically; historical quantities and documents are untouched.</summary>
    public ServiceResult UpdateUnit(User actor, int id, string? name, int sortOrder, bool? wholeOnly = null)
    {
        if (!actor.CanManage)
        {
            return ServiceResult.Fail("Only the owner or a manager can change units.");
        }

        var clean = NormalizeUnitName(name);
        if (id <= 0 || clean.Length == 0 || clean.Length > 40 || !IsValidSortOrder(sortOrder))
        {
            return ServiceResult.Fail("Choose a unit, enter a name under 40 characters and a valid order.");
        }

        try
        {
            return _db.Tx(connection =>
            {
                var before = FindUnit(connection, id);
                if (before is null)
                {
                    return ServiceResult.Fail("That unit no longer exists.");
                }
                if (UnitNameTaken(connection, clean, id))
                {
                    return ServiceResult.Fail("That unit name is already used, including by a put-away unit.");
                }

                var parameters = new { clean, old = before.Name, order = sortOrder, wholeOnly, id, at = Clock.Now() };
                _db.Execute(connection, @"
UPDATE products SET base_unit = @clean, updated_at = @at
WHERE LOWER(TRIM(base_unit)) = @old;", parameters);
                _db.Execute(connection, @"
UPDATE products SET strength_unit = @clean, updated_at = @at
WHERE strength_value > 0 AND LOWER(TRIM(strength_unit)) = @old;", parameters);
                _db.Execute(connection, "UPDATE units SET name = @clean, whole_only = COALESCE(@wholeOnly, whole_only), sort_order = @order WHERE id = @id", parameters);
                _audit.RecordChange(connection, actor, "Inventory", "unit_update", "units",
                    id.ToString(), before, FindUnit(connection, id),
                    "Current product unit labels updated together; no stock quantity or historical document was rewritten.");
                return ServiceResult.Success();
            });
        }
        catch (Exception exception)
        {
            _log.Error("A unit could not be updated.", exception);
            return ServiceResult.Fail("The unit could not be saved. " + exception.Message);
        }
    }

    /// <summary>Put-away is refused while a current product uses the unit; no stock label is silently cleared.</summary>
    public ServiceResult SetUnitActive(User actor, int id, bool active)
    {
        if (!actor.CanManage)
        {
            return ServiceResult.Fail("Only the owner or a manager can change units.");
        }

        try
        {
            return _db.Tx(connection =>
            {
                var before = FindUnit(connection, id);
                if (before is null)
                {
                    return ServiceResult.Fail("That unit no longer exists.");
                }
                if (before.Active == active)
                {
                    return ServiceResult.Success();
                }
                if (!active && before.UsageCount > 0)
                {
                    return ServiceResult.Fail(
                        $"This unit is used by {before.UsageCount} product field(s). Rename those products first; nothing was unlinked.");
                }
                if (active && UnitNameTaken(connection, before.Name, id))
                {
                    return ServiceResult.Fail("Another unit uses this name. Rename one before bringing it back.");
                }

                _db.Execute(connection, "UPDATE units SET active = @active WHERE id = @id;", new { active, id });
                _audit.RecordChange(connection, actor, "Inventory",
                    active ? "unit_restore" : "unit_put_away", "units", id.ToString(),
                    before, FindUnit(connection, id));
                return ServiceResult.Success();
            });
        }
        catch (Exception exception)
        {
            _log.Error("A unit's active state could not be changed.", exception);
            return ServiceResult.Fail("The unit could not be changed. " + exception.Message);
        }
    }

    private Unit? FindUnit(SqliteConnection connection, int id) =>
        _db.QuerySingle<Unit>(connection, @"
SELECT u.id, u.name, u.active, u.whole_only AS wholeOnly, u.sort_order AS sortOrder,
       (SELECT COUNT(1) FROM products p WHERE LOWER(TRIM(p.base_unit)) = u.name) AS productCount,
       (SELECT COUNT(1) FROM products p
        WHERE p.strength_value > 0 AND LOWER(TRIM(p.strength_unit)) = u.name) AS strengthCount
FROM units u WHERE u.id = @id;", new { id });

    private static string NormalizeUnitName(string? value) =>
        string.Join(" ", (value ?? string.Empty).Trim()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToLowerInvariant();

    private static string NormalizeStrengthFilter(string? value) => NormalizeUnitName(value);

    private static string? StrengthError(double value, string? unit)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            return "Strength must be a finite number of zero or more.";
        }
        if (value > 0 && NormalizeUnitName(unit).Length == 0)
        {
            return "Choose a managed unit for the strength, such as ml.";
        }
        return null;
    }

    private void EnsureActiveUnit(SqliteConnection connection, string? name)
    {
        var clean = NormalizeUnitName(name);
        var exists = Convert.ToInt32(_db.Scalar(connection,
            "SELECT COUNT(1) FROM units WHERE name = @name AND active = 1;", new { name = clean }) ?? 0);
        if (clean.Length == 0 || exists == 0)
        {
            throw new ValidationException(
                $"The unit '{clean}' is not on the active managed list. Add or restore it in Manage units first.");
        }
    }

    private bool UnitNameTaken(SqliteConnection connection, string name, int exceptId) =>
        Convert.ToInt64(_db.Scalar(connection,
            "SELECT COUNT(1) FROM units WHERE LOWER(name) = LOWER(@name) AND id <> @exceptId;",
            new { name, exceptId }) ?? 0L) > 0;

    private void SaveCategoryTags(
        SqliteConnection connection,
        int productId,
        int? primaryCategoryId,
        IReadOnlyCollection<int>? tagIds)
    {
        if (tagIds is null)
        {
            return;
        }

        var clean = tagIds.Where(id => id > 0).Distinct().ToList();
        if (primaryCategoryId is int primary && clean.Contains(primary))
        {
            throw new ValidationException("The primary category is already included; choose only additional category tags.");
        }

        foreach (var tagId in clean)
        {
            var exists = Convert.ToInt32(_db.Scalar(connection,
                "SELECT COUNT(1) FROM categories WHERE id = @tagId AND active = 1;", new { tagId }) ?? 0);
            if (exists == 0)
            {
                throw new ValidationException("Every additional category tag must be on the active category list.");
            }
        }

        _db.Execute(connection, "DELETE FROM product_category_tags WHERE product_id = @productId;", new { productId });
        foreach (var tagId in clean)
        {
            _db.Execute(connection,
                "INSERT INTO product_category_tags(product_id, category_id) VALUES(@productId, @tagId);",
                new { productId, tagId });
        }
    }

    private void EnsureActiveCatalogLinks(SqliteConnection connection, int? categoryId, int? companyId)
    {
        if (categoryId is not null)
        {
            var exists = Convert.ToInt32(_db.Scalar(connection,
                "SELECT COUNT(1) FROM categories WHERE id = @categoryId AND active = 1;",
                new { categoryId }) ?? 0);
            if (exists == 0)
            {
                throw new ValidationException("Choose a category that is on the active list.");
            }
        }

        if (companyId is not null)
        {
            var exists = Convert.ToInt32(_db.Scalar(connection,
                "SELECT COUNT(1) FROM companies WHERE id = @companyId AND active = 1;",
                new { companyId }) ?? 0);
            if (exists == 0)
            {
                throw new ValidationException("Choose a company that is on the active list.");
            }
        }
    }

    private Category? FindCategory(SqliteConnection connection, int id) =>
        _db.QuerySingle<Category>(connection, @"
SELECT id, name, parent_id AS parentId, sort_order AS sortOrder, active
FROM categories WHERE id = @id;", new { id });

    private Company? FindCompany(SqliteConnection connection, int id) =>
        _db.QuerySingle<Company>(connection, @"
SELECT id, name, phone, notes, rep_name AS repName, rep_phone AS repPhone,
       visit_day AS visitDay, sort_order AS sortOrder, active
FROM companies WHERE id = @id;", new { id });

    private string? CategoryParentError(
        SqliteConnection connection,
        int categoryId,
        int? parentId,
        int? currentParentId)
    {
        if (parentId is null)
        {
            return null;
        }

        if (parentId.Value <= 0 || parentId.Value == categoryId)
        {
            return "A category cannot be its own parent.";
        }

        var parent = FindCategory(connection, parentId.Value);
        if (parent is null)
        {
            return "Choose a parent category that exists.";
        }

        if (!parent.Active && currentParentId != parentId)
        {
            return "Bring the parent category back before assigning it.";
        }

        if (parent.ParentId is not null)
        {
            return "Only two levels are allowed: choose a top-level parent.";
        }

        var children = Convert.ToInt32(_db.Scalar(connection,
            "SELECT COUNT(1) FROM categories WHERE parent_id = @categoryId;", new { categoryId }) ?? 0);
        return categoryId > 0 && children > 0
            ? "This category has subcategories. Move them before giving it a parent."
            : null;
    }

    private bool CatalogNameTaken(SqliteConnection connection, string table, string name, int exceptId)
    {
        // Only two fixed callers pass a table name; never accept a table name from the UI.
        var sql = table == "categories"
            ? "SELECT COUNT(1) FROM categories WHERE name = @name COLLATE NOCASE AND id <> @exceptId;"
            : "SELECT COUNT(1) FROM companies WHERE name = @name COLLATE NOCASE AND id <> @exceptId;";
        return Convert.ToInt32(_db.Scalar(connection, sql, new { name, exceptId }) ?? 0) > 0;
    }

    private int NextSortOrder(SqliteConnection connection, string table)
    {
        var sql = table switch
        {
            "categories" => "SELECT IFNULL(MAX(sort_order), 0) + 10 FROM categories;",
            "companies" => "SELECT IFNULL(MAX(sort_order), 0) + 10 FROM companies;",
            "units" => "SELECT IFNULL(MAX(sort_order), 0) + 10 FROM units;",
            _ => throw new ArgumentOutOfRangeException(nameof(table))
        };
        return Convert.ToInt32(_db.Scalar(connection, sql) ?? 10);
    }

    private static string CleanCatalogName(string? name) => (name ?? string.Empty).Trim();

    private static bool IsValidSortOrder(int value) => value is >= 0 and <= MaxCatalogSortOrder;

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
