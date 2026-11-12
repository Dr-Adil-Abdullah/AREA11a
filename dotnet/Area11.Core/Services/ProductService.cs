using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;
using Dapper;
using Microsoft.Data.Sqlite;
using System.Dynamic;
using System.Text;

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
       p.generic_formula_id, p.brand_id, p.preferred_supplier_id,
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

    /// <summary>Portion 10: the non-text facets shared by the one-word and multi-word searches.</summary>
    private const string FilterTailSql = @"
  AND (@categoryId IS NULL OR p.category_id = @categoryId OR EXISTS (
      SELECT 1 FROM categories child
      WHERE child.id = p.category_id AND child.parent_id = @categoryId)
       OR EXISTS (
      SELECT 1 FROM product_category_tags pct
      JOIN categories tagged ON tagged.id = pct.category_id AND tagged.active = 1
      WHERE pct.product_id = p.id
        AND (pct.category_id = @categoryId OR tagged.parent_id = @categoryId)))
  AND (@companyId IS NULL OR p.company_id = @companyId)
  AND (@supplierId IS NULL OR EXISTS (
      SELECT 1 FROM product_supplier_directory directory
      WHERE directory.product_id = p.id AND directory.supplier_id = @supplierId))
  AND (@strength = '' OR
       lower(trim(p.strength_unit)) = @strength OR
       (p.strength_value > 0 AND printf('%g', p.strength_value) = @strength) OR
       (p.strength_value > 0 AND replace(lower(printf('%g', p.strength_value) || ' ' || trim(p.strength_unit)), ' ', '') = replace(@strength, ' ', '')))
  AND (@lowStock = 0 OR
       (IFNULL((SELECT SUM(b.qty_base) FROM batches b WHERE b.product_id = p.id AND b.active = 1), 0) >= 0
        AND IFNULL((SELECT SUM(b.qty_base) FROM batches b WHERE b.product_id = p.id AND b.active = 1), 0) <= p.reorder_level))
ORDER BY p.name COLLATE NOCASE;";

    private const int MaxCatalogNameLength = 120;
    private const int MaxCatalogSortOrder = 1_000_000_000;
    private const string SortOrderError = "Order must be a whole number from 0 to 1,000,000,000.";

    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly LogService _log;
    private readonly string? _photosFolder;

    /// <param name="photosFolder">Tests pass their own folder; the shop uses %ProgramData%\Area11\photos.</param>
    public ProductService(Database db, AuditService audit, LogService log, string? photosFolder = null)
    {
        _db = db;
        _audit = audit;
        _log = log;
        _photosFolder = photosFolder;
    }

    private PhotoService Photos() =>
        new(_db, _audit, new PermissionService(_db, _log, _audit), _log, _photosFolder);

    public ServiceResult<List<Product>> Search(
        string? term = null,
        int? categoryId = null,
        int? companyId = null,
        bool lowStockOnly = false,
        string? strength = null,
        int? supplierId = null)
    {
        try
        {
            var text = (term ?? string.Empty).Trim();
            var searchKey = PackagingPolicy.SearchKey(text);
            var strengthText = NormalizeStrengthFilter(strength);

            using var connection = _db.Open();
            var tokens = PackagingPolicy.Tokens(text);
            var products = tokens.Length > 1
                ? SearchTokenized(connection, tokens, categoryId, companyId, lowStockOnly, strengthText, supplierId)
                : _db.Query<Product>(connection, SelectSql + @"
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
" + FilterTailSql,
                new
                {
                    term = text,
                    like = $"%{text}%",
                    searchKey,
                    keyLike = $"%{searchKey}%",
                    categoryId,
                    companyId,
                    supplierId,
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
    /// Portion 10: the multi-word search. Every word must land in some field (AND), in any order,
    /// so "panadol 100ml" narrows to the 100ml variant while "panadol" alone still lists every one.
    /// One-word queries keep the historical statement above byte for byte.
    /// </summary>
    private List<Product> SearchTokenized(
        SqliteConnection connection,
        string[] tokens,
        int? categoryId,
        int? companyId,
        bool lowStockOnly,
        string strengthText,
        int? supplierId)
    {
        var where = new StringBuilder("WHERE p.active = 1\n");
        IDictionary<string, object?> args = new ExpandoObject();
        for (var i = 0; i < tokens.Length; i++)
        {
            where.Append("  AND (p.name LIKE @like").Append(i).Append(" OR IFNULL(p.generic, '') LIKE @like").Append(i)
                .Append(" OR IFNULL(p.brand, '') LIKE @like").Append(i)
                .Append(" OR IFNULL(p.barcode, '') LIKE @like").Append(i)
                .Append(" OR IFNULL(p.rack_no, '') LIKE @like").Append(i)
                .Append(" OR IFNULL(p.room, '') LIKE @like").Append(i)
                .Append(" OR IFNULL(p.pack_size_label, '') LIKE @like").Append(i)
                .Append(" OR IFNULL(p.base_unit, '') LIKE @like").Append(i)
                .Append(" OR IFNULL(c.name, '') LIKE @like").Append(i)
                .Append(" OR IFNULL(co.name, '') LIKE @like").Append(i)
                .Append(" OR IFNULL(p.strength_unit, '') LIKE @like").Append(i)
                .Append(" OR (p.strength_value > 0 AND printf('%g', p.strength_value) LIKE @like").Append(i).Append(')')
                .Append(" OR (p.strength_value > 0 AND (printf('%g', p.strength_value) || ' ' || p.strength_unit) LIKE @like").Append(i).Append(')')
                .Append(" OR replace(replace(lower(IFNULL(p.pack_size_label, '')), ' ', ''), '×', 'x') LIKE @keyLike").Append(i)
                .Append(" OR (p.strength_value > 0 AND replace(replace(lower(printf('%g', p.strength_value) || ' ' || IFNULL(p.strength_unit, '')), ' ', ''), '×', 'x') LIKE @keyLike").Append(i).Append(')')
                .Append(" OR EXISTS (SELECT 1 FROM product_barcodes pb WHERE pb.product_id = p.id AND pb.active = 1 AND pb.barcode LIKE @like").Append(i).Append(')')
                .Append(" OR EXISTS (SELECT 1 FROM product_category_tags search_tags JOIN categories search_category ON search_category.id = search_tags.category_id WHERE search_tags.product_id = p.id AND search_category.active = 1 AND search_category.name LIKE @like").Append(i).Append("))\n");
            args["like" + i] = "%" + tokens[i] + "%";
            args["keyLike" + i] = "%" + PackagingPolicy.SearchKey(tokens[i]) + "%";
        }

        where.Append(FilterTailSql);
        args["categoryId"] = categoryId;
        args["companyId"] = companyId;
        args["supplierId"] = supplierId;
        args["strength"] = strengthText;
        args["lowStock"] = lowStockOnly ? 1 : 0;
        return _db.Query<Product>(connection, SelectSql + where.ToString(), args);
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
        string? strength = null,
        int? supplierId = null)
    {
        var typed = (term ?? string.Empty).Trim();

        var exact = Search(typed, categoryId, companyId, lowStockOnly, strength, supplierId);

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
        var all = Search(null, categoryId, companyId, lowStockOnly, strength, supplierId);

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

    /// <summary>
    /// A new product with its answers and, optionally, a photo chosen on the same form. The photo
    /// is copied and recorded inside the same transaction, before the answers are validated, so a
    /// required photo field sees it; a failed creation leaves neither a row nor a copied file.
    /// </summary>
    public ServiceResult<int> Create(User actor, ProductPatch input, IReadOnlyDictionary<int, string?>? customValues = null,
        string? stagedPhotoPath = null)
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

        var staged = string.IsNullOrWhiteSpace(stagedPhotoPath) ? null : stagedPhotoPath.Trim();
        if (staged is not null)
        {
            var stagedError = Photos().ValidateSource(staged);
            if (stagedError is not null)
            {
                return ServiceResult<int>.Fail(stagedError);
            }
        }

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

        string? stagedCopy = null;
        try
        {
            var id = _db.Tx(connection =>
            {
                var linkedGeneric = LinkedCatalogText(connection, "generic_formulas", input.GenericFormulaId, input.Generic, "salt");
                var linkedBrand = LinkedCatalogText(connection, "brands", input.BrandId, input.Brand, "brand");
                foreach (var (key, label, value) in new[]
                {
                    ("product.generic", "Generic / salt", linkedGeneric),
                    ("product.brand", "Brand", linkedBrand),
                    ("product.rack", "Rack / shelf", input.RackNo),
                    ("product.pack", "Pack description", input.PackSizeLabel)
                })
                {
                    var required = NativeFieldPolicyService.RequiredTextError(_db, connection, key, label, value);
                    if (required is not null) throw new ValidationException(required);
                }
                EnsureActiveCatalogLinks(connection, input.CategoryId, input.CompanyId, input.GenericFormulaId, input.BrandId);
                EnsureActiveSupplier(connection, input.PreferredSupplierId);
                EnsureActiveUnit(connection, baseUnit);
                if (strengthValue > 0)
                {
                    EnsureActiveUnit(connection, strengthUnit);
                }

                _db.Execute(connection, @"
INSERT INTO products
    (name, generic, brand, barcode, category_id, company_id, generic_formula_id, brand_id,
     preferred_supplier_id, rack_no, room, pack_size_label,
     base_unit, strength_value, strength_unit, box_strips, strip_tablets,
     cost_paisa, retail_paisa, vip_paisa, doctor_paisa,
     reorder_level, track_expiry, active, created_at, updated_at)
VALUES
    (@name, @generic, @brand, @barcode, @categoryId, @companyId, @formulaId, @brandId,
     @preferredSupplierId, @rackNo, @room, @packSizeLabel,
     @baseUnit, @strengthValue, @strengthUnit, @boxStrips, @stripTablets,
     @costPaisa, @retailPaisa, @vipPaisa, @doctorPaisa,
     @reorderLevel, @trackExpiry, 1, @at, @at);",
                    new
                    {
                        name,
                        generic = linkedGeneric,
                        brand = linkedBrand,
                        formulaId = input.GenericFormulaId,
                        brandId = input.BrandId,
                        preferredSupplierId = input.PreferredSupplierId,
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
                if (staged is not null)
                {
                    var attached = Photos().AddInTransaction(connection, actor, PhotoEntity.Product, newId, staged, out stagedCopy);
                    if (!attached.Ok) throw new ValidationException(attached.Error ?? "The photo could not be attached.");
                }
                var custom = new CustomFieldService(_db, _audit, new PermissionService(_db, _log, _audit), _log);
                var answers = custom.SaveNewRecordValuesInTransaction(connection, actor, CustomFieldEntity.Product, newId, customValues);
                if (!answers.Ok) throw new ValidationException(answers.Error ?? "Product extra fields could not be saved.");
                SaveCategoryTags(connection, newId, input.CategoryId, input.CategoryTagIds);

                _audit.Record(connection, actor, "Inventory", "product_create", "products", newId.ToString(),
                    newValue: $"{{\"name\":\"{name}\",\"retailPaisa\":{input.RetailPaisa ?? 0}}}");

                return newId;
            });

            return ServiceResult<int>.Success(id, SameNameWarnings(id, name));
        }
        catch (ValidationException exception)
        {
            Photos().DiscardCopy(stagedCopy);
            return ServiceResult<int>.Fail(exception.Message);
        }
        catch (SqliteException exception) when (exception.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase))
        {
            Photos().DiscardCopy(stagedCopy);
            return ServiceResult<int>.Fail("That barcode already belongs to another product.");
        }
        catch (Exception exception)
        {
            Photos().DiscardCopy(stagedCopy);
            _log.Error("A product could not be created.", exception);
            return ServiceResult<int>.Fail("The product could not be saved.");
        }
    }

    /// <summary>
    /// The owner's necessary product fields this patch leaves empty (v0.70.0, owner batch).
    /// The editor shows these in a warn-once popup; the service never blocks the save, because
    /// the owner chose "warn me once, then let me save anyway". A linked salt or brand counts
    /// as present - the typed text is only one of the two ways to fill it.
    /// </summary>
    public IReadOnlyList<string> MissingRequiredLabels(ProductPatch patch)
    {
        using var connection = _db.Open();
        var missing = new List<string>();
        var generic = patch.GenericFormulaId > 0 ? "linked" : patch.Generic;
        if (NativeFieldPolicyService.RequiredTextError(_db, connection,
            "product.generic", "Generic / salt", generic) is not null)
        {
            missing.Add("Generic / salt");
        }
        var brand = patch.BrandId > 0 ? "linked" : patch.Brand;
        if (NativeFieldPolicyService.RequiredTextError(_db, connection,
            "product.brand", "Brand", brand) is not null)
        {
            missing.Add("Brand");
        }
        if (NativeFieldPolicyService.RequiredTextError(_db, connection,
            "product.rack", "Rack / shelf", patch.RackNo) is not null)
        {
            missing.Add("Rack / shelf");
        }
        if (NativeFieldPolicyService.RequiredTextError(_db, connection,
            "product.pack", "Pack description", patch.PackSizeLabel) is not null)
        {
            missing.Add("Pack description");
        }
        if (NativeFieldPolicyService.IsRequired(_db, connection, "product.retail")
            && patch.RetailPaisa is null or <= 0)
        {
            missing.Add("Selling price");
        }
        return missing;
    }

    /// <summary>
    /// Partial update (Spec 8.1.4.1): only non-null fields are written, so changing the retail
    /// rate can never wipe the cost, the pack formula, the rack or the reorder level.
    /// </summary>
    public ServiceResult Update(User actor, int id, ProductPatch patch, IReadOnlyDictionary<int, string?>? customValues = null)
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

                // Do not strand old records that already lack a newly required field. Reject only
                // an explicit attempt to clear a value that already existed.
                foreach (var (key, label, oldValue, newValue) in new[]
                {
                    ("product.generic", "Generic / salt", before.Generic, patch.Generic),
                    ("product.brand", "Brand", before.Brand, patch.Brand),
                    ("product.rack", "Rack / shelf", before.RackNo, patch.RackNo),
                    ("product.pack", "Pack description", before.PackSizeLabel, patch.PackSizeLabel)
                })
                {
                    if (newValue is null || string.IsNullOrWhiteSpace(oldValue)) continue;
                    var required = NativeFieldPolicyService.RequiredTextError(_db, connection, key, label, newValue);
                    if (required is not null) throw new ValidationException(required);
                }

                before.CategoryTagIds = _db.Query<int>(connection,
                    "SELECT category_id FROM product_category_tags WHERE product_id = @id ORDER BY category_id;",
                    new { id }).ToList();

                EnsureActiveCatalogLinks(connection, patch.CategoryId, patch.CompanyId, patch.GenericFormulaId, patch.BrandId);
                EnsureActiveSupplier(connection, patch.PreferredSupplierId);
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

                if (patch.GenericFormulaId is not null)
                {
                    var catalogGeneric = LinkedCatalogText(connection, "generic_formulas",
                        patch.GenericFormulaId, patch.Generic ?? before.Generic, "salt");
                    Set("generic_formula_id", patch.GenericFormulaId, "formulaId");
                    Set("generic", catalogGeneric, "generic");
                }
                else if (patch.ClearGenericFormula)
                {
                    Set("generic_formula_id", null, "formulaId");
                    if (patch.Generic is not null)
                    {
                        Set("generic", NullIfEmpty(patch.Generic), "generic");
                    }
                }
                else
                {
                    if (patch.Generic is not null)
                    {
                        Set("generic", NullIfEmpty(patch.Generic), "generic");
                    }

                    if (before.GenericFormulaId is int linkedFormula && patch.Generic is not null
                        && !LinkedNameMatches(connection, "generic_formulas", linkedFormula, patch.Generic))
                    {
                        Set("generic_formula_id", null, "formulaId");
                    }
                }

                if (patch.BrandId is not null)
                {
                    var catalogBrand = LinkedCatalogText(connection, "brands",
                        patch.BrandId, patch.Brand ?? before.Brand, "brand");
                    Set("brand_id", patch.BrandId, "brandId");
                    Set("brand", catalogBrand, "brand");
                }
                else if (patch.ClearBrand)
                {
                    Set("brand_id", null, "brandId");
                    if (patch.Brand is not null)
                    {
                        Set("brand", NullIfEmpty(patch.Brand), "brand");
                    }
                }
                else
                {
                    if (patch.Brand is not null)
                    {
                        Set("brand", NullIfEmpty(patch.Brand), "brand");
                    }

                    if (before.BrandId is int linkedBrand && patch.Brand is not null
                        && !LinkedNameMatches(connection, "brands", linkedBrand, patch.Brand))
                    {
                        Set("brand_id", null, "brandId");
                    }
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

                if (patch.PreferredSupplierId is not null)
                {
                    Set("preferred_supplier_id", patch.PreferredSupplierId, "preferredSupplierId");
                }
                else if (patch.ClearPreferredSupplier)
                {
                    Set("preferred_supplier_id", null, "preferredSupplierId");
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

                if (customValues is not null)
                {
                    var custom = new CustomFieldService(_db, _audit, new PermissionService(_db, _log, _audit), _log);
                    var answers = custom.SaveValuesInTransaction(connection, actor, CustomFieldEntity.Product, id, customValues);
                    if (!answers.Ok) throw new ValidationException(answers.Error ?? "Product extra fields could not be saved.");
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
                var unlinkedBrandIds = new List<int>();
                if (!active)
                {
                    unlinkedIds = _db.Query<int>(connection,
                        "SELECT id FROM products WHERE company_id = @id ORDER BY id;", new { id });
                    _db.Execute(connection, @"
UPDATE products SET company_id = NULL, updated_at = @at WHERE company_id = @id;",
                        new { at = Clock.Now(), id });
                    unlinkedBrandIds = _db.Query<int>(connection,
                        "SELECT id FROM brands WHERE company_id = @id ORDER BY id;", new { id });
                    _db.Execute(connection, @"
UPDATE brands SET company_id = NULL, updated_at = @at WHERE company_id = @id;",
                        new { at = Clock.Now(), id });
                }

                _db.Execute(connection, "UPDATE companies SET active = @active WHERE id = @id;",
                    new { active, id });
                var after = FindCompany(connection, id);
                var details = active
                    ? "Brought back. Former product and brand links were not recreated."
                    : $"Put away; unlinked {unlinkedIds.Count} product(s) [{string.Join(", ", unlinkedIds)}] and {unlinkedBrandIds.Count} brand(s) [{string.Join(", ", unlinkedBrandIds)}]. No product or historical record was deleted.";
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

    /// <summary>Managed salts for the product editor dropdown (Portion 8, #9).</summary>
    public ServiceResult<List<GenericFormula>> GetFormulas(bool activeOnly = true)
    {
        try
        {
            using var connection = _db.Open();
            var rows = _db.Query<GenericFormula>(connection, @"
SELECT f.id, f.name, f.name_key AS nameKey, f.active, f.sort_order AS sortOrder,
       f.created_at AS createdAt, f.updated_at AS updatedAt,
       (SELECT COUNT(1) FROM products p WHERE p.generic_formula_id = f.id) AS productCount
FROM generic_formulas f
" + (activeOnly ? "WHERE f.active = 1 " : string.Empty)
                + "ORDER BY f.sort_order, f.name COLLATE NOCASE, f.id;");
            return ServiceResult<List<GenericFormula>>.Success(rows);
        }
        catch (Exception exception)
        {
            _log.Error("The salt list could not be loaded.", exception);
            return ServiceResult<List<GenericFormula>>.Fail("The salt list could not be loaded.");
        }
    }

    /// <summary>Managed brands for the product editor dropdown (Portion 8, #11).</summary>
    public ServiceResult<List<BrandDefinition>> GetBrands(bool activeOnly = true)
    {
        try
        {
            using var connection = _db.Open();
            var rows = _db.Query<BrandDefinition>(connection, @"
SELECT b.id, b.company_id AS companyId, co.name AS companyName,
       b.name, b.name_key AS nameKey, b.active,
       b.sort_order AS sortOrder, b.created_at AS createdAt, b.updated_at AS updatedAt,
       (SELECT COUNT(1) FROM products p WHERE p.brand_id = b.id) AS productCount
FROM brands b
LEFT JOIN companies co ON co.id = b.company_id
" + (activeOnly ? "WHERE b.active = 1 " : string.Empty)
                + "ORDER BY b.sort_order, b.name COLLATE NOCASE, b.id;");
            return ServiceResult<List<BrandDefinition>>.Success(rows);
        }
        catch (Exception exception)
        {
            _log.Error("The brand list could not be loaded.", exception);
            return ServiceResult<List<BrandDefinition>>.Fail("The brand list could not be loaded.");
        }
    }

    public ServiceResult<int> AddFormula(User actor, string? name, int? sortOrder = null)
    {
        if (!actor.CanManage)
        {
            return ServiceResult<int>.Fail("Only the owner or a manager can change salts.");
        }

        var clean = CleanDisplayName(name);
        if (clean.Length == 0)
        {
            return ServiceResult<int>.Fail("Enter a salt name, for example Paracetamol.");
        }
        if (clean.Length > MaxCatalogNameLength)
        {
            return ServiceResult<int>.Fail("Keep the salt name under 120 characters.");
        }
        if (sortOrder is not null && !IsValidSortOrder(sortOrder.Value))
        {
            return ServiceResult<int>.Fail(SortOrderError);
        }

        try
        {
            return _db.Tx(connection =>
            {
                var key = CatalogKeyOf(clean);
                if (CatalogKeyTaken(connection, "generic_formulas", key, 0))
                {
                    return ServiceResult<int>.Fail("That salt is already on the list, including a salt put away.");
                }

                var order = sortOrder ?? NextSortOrder(connection, "generic_formulas");
                if (!IsValidSortOrder(order))
                {
                    return ServiceResult<int>.Fail(SortOrderError);
                }

                _db.Execute(connection, @"
INSERT INTO generic_formulas(name, name_key, sort_order, active) VALUES(@name, @key, @sortOrder, 1);",
                    new { name = clean, key, sortOrder = order });
                var id = Convert.ToInt32(_db.Scalar(connection,
                    "SELECT id FROM generic_formulas WHERE rowid = last_insert_rowid();") ?? 0);
                _audit.RecordChange(connection, actor, "Inventory", "formula_create", "generic_formulas",
                    id.ToString(), null, FindFormula(connection, id));
                return ServiceResult<int>.Success(id);
            });
        }
        catch (Exception exception)
        {
            _log.Error($"Salt '{clean}' could not be added.", exception);
            return ServiceResult<int>.Fail("The salt could not be saved. " + exception.Message);
        }
    }

    public ServiceResult<int> AddBrand(User actor, string? name, int? sortOrder = null)
    {
        if (!actor.CanManage)
        {
            return ServiceResult<int>.Fail("Only the owner or a manager can change brands.");
        }

        var clean = CleanDisplayName(name);
        if (clean.Length == 0)
        {
            return ServiceResult<int>.Fail("Enter a brand name, for example GSK.");
        }
        if (clean.Length > MaxCatalogNameLength)
        {
            return ServiceResult<int>.Fail("Keep the brand name under 120 characters.");
        }
        if (sortOrder is not null && !IsValidSortOrder(sortOrder.Value))
        {
            return ServiceResult<int>.Fail(SortOrderError);
        }

        try
        {
            return _db.Tx(connection =>
            {
                var key = CatalogKeyOf(clean);
                if (CatalogKeyTaken(connection, "brands", key, 0))
                {
                    return ServiceResult<int>.Fail("That brand is already on the list, including a brand put away.");
                }

                var order = sortOrder ?? NextSortOrder(connection, "brands");
                if (!IsValidSortOrder(order))
                {
                    return ServiceResult<int>.Fail(SortOrderError);
                }

                // company_id stays NULL: brand-to-company assignment is a later portion.
                _db.Execute(connection, @"
INSERT INTO brands(name, name_key, sort_order, active) VALUES(@name, @key, @sortOrder, 1);",
                    new { name = clean, key, sortOrder = order });
                var id = Convert.ToInt32(_db.Scalar(connection,
                    "SELECT id FROM brands WHERE rowid = last_insert_rowid();") ?? 0);
                _audit.RecordChange(connection, actor, "Inventory", "brand_create", "brands",
                    id.ToString(), null, FindBrand(connection, id));
                return ServiceResult<int>.Success(id);
            });
        }
        catch (Exception exception)
        {
            _log.Error($"Brand '{clean}' could not be added.", exception);
            return ServiceResult<int>.Fail("The brand could not be saved. " + exception.Message);
        }
    }

    /// <summary>Rename relabels explicitly linked products; legacy typed text is never touched.</summary>
    public ServiceResult UpdateFormula(User actor, int id, string? name, int sortOrder)
    {
        if (!actor.CanManage)
        {
            return ServiceResult.Fail("Only the owner or a manager can change salts.");
        }

        var clean = CleanDisplayName(name);
        if (id <= 0 || clean.Length == 0)
        {
            return ServiceResult.Fail("Pick a salt and enter its name.");
        }
        if (clean.Length > MaxCatalogNameLength)
        {
            return ServiceResult.Fail("Keep the salt name under 120 characters.");
        }
        if (!IsValidSortOrder(sortOrder))
        {
            return ServiceResult.Fail(SortOrderError);
        }

        try
        {
            return _db.Tx(connection =>
            {
                var before = FindFormula(connection, id);
                if (before is null)
                {
                    return ServiceResult.Fail("That salt no longer exists.");
                }

                var key = CatalogKeyOf(clean);
                if (CatalogKeyTaken(connection, "generic_formulas", key, id))
                {
                    return ServiceResult.Fail("That salt name is already used, including by a salt put away.");
                }

                var relabelled = 0;
                if (!string.Equals(before.Name, clean, StringComparison.Ordinal))
                {
                    relabelled = Convert.ToInt32(_db.Scalar(connection,
                        "SELECT COUNT(1) FROM products WHERE generic_formula_id = @id;", new { id }) ?? 0);
                    _db.Execute(connection, @"
UPDATE products SET generic = @clean, updated_at = @at WHERE generic_formula_id = @id;",
                        new { clean, at = Clock.Now(), id });
                }

                _db.Execute(connection, @"
UPDATE generic_formulas SET name = @clean, name_key = @key, sort_order = @order, updated_at = @at
WHERE id = @id;", new { clean, key, order = sortOrder, at = Clock.Now(), id });
                _audit.RecordChange(connection, actor, "Inventory", "formula_update", "generic_formulas",
                    id.ToString(), before, FindFormula(connection, id),
                    relabelled + " linked product(s) relabelled; legacy typed text untouched.");
                return ServiceResult.Success();
            });
        }
        catch (Exception exception)
        {
            _log.Error($"Salt {id} could not be updated.", exception);
            return ServiceResult.Fail("The salt could not be saved. " + exception.Message);
        }
    }

    /// <summary>Rename relabels explicitly linked products; legacy typed text is never touched.</summary>
    public ServiceResult UpdateBrand(User actor, int id, string? name, int sortOrder)
    {
        if (!actor.CanManage)
        {
            return ServiceResult.Fail("Only the owner or a manager can change brands.");
        }

        var clean = CleanDisplayName(name);
        if (id <= 0 || clean.Length == 0)
        {
            return ServiceResult.Fail("Pick a brand and enter its name.");
        }
        if (clean.Length > MaxCatalogNameLength)
        {
            return ServiceResult.Fail("Keep the brand name under 120 characters.");
        }
        if (!IsValidSortOrder(sortOrder))
        {
            return ServiceResult.Fail(SortOrderError);
        }

        try
        {
            return _db.Tx(connection =>
            {
                var before = FindBrand(connection, id);
                if (before is null)
                {
                    return ServiceResult.Fail("That brand no longer exists.");
                }

                var key = CatalogKeyOf(clean);
                if (CatalogKeyTaken(connection, "brands", key, id))
                {
                    return ServiceResult.Fail("That brand name is already used, including by a brand put away.");
                }

                var relabelled = 0;
                if (!string.Equals(before.Name, clean, StringComparison.Ordinal))
                {
                    relabelled = Convert.ToInt32(_db.Scalar(connection,
                        "SELECT COUNT(1) FROM products WHERE brand_id = @id;", new { id }) ?? 0);
                    _db.Execute(connection, @"
UPDATE products SET brand = @clean, updated_at = @at WHERE brand_id = @id;",
                        new { clean, at = Clock.Now(), id });
                }

                _db.Execute(connection, @"
UPDATE brands SET name = @clean, name_key = @key, sort_order = @order, updated_at = @at
WHERE id = @id;", new { clean, key, order = sortOrder, at = Clock.Now(), id });
                _audit.RecordChange(connection, actor, "Inventory", "brand_update", "brands",
                    id.ToString(), before, FindBrand(connection, id),
                    relabelled + " linked product(s) relabelled; legacy typed text untouched.");
                return ServiceResult.Success();
            });
        }
        catch (Exception exception)
        {
            _log.Error($"Brand {id} could not be updated.", exception);
            return ServiceResult.Fail("The brand could not be saved. " + exception.Message);
        }
    }

    /// <summary>Put-away unlinks current products but keeps their typed text; nothing is deleted.</summary>
    public ServiceResult SetFormulaActive(User actor, int id, bool active)
    {
        if (!actor.CanManage)
        {
            return ServiceResult.Fail("Only the owner or a manager can change salts.");
        }

        try
        {
            return _db.Tx(connection =>
            {
                var before = FindFormula(connection, id);
                if (before is null)
                {
                    return ServiceResult.Fail("That salt no longer exists.");
                }
                if (before.Active == active)
                {
                    return ServiceResult.Success();
                }
                if (active && CatalogKeyTaken(connection, "generic_formulas", before.NameKey, id))
                {
                    return ServiceResult.Fail("Another salt uses this name. Rename one before bringing it back.");
                }

                var unlinked = 0;
                if (!active)
                {
                    unlinked = Convert.ToInt32(_db.Scalar(connection,
                        "SELECT COUNT(1) FROM products WHERE generic_formula_id = @id;", new { id }) ?? 0);
                    _db.Execute(connection, @"
UPDATE products SET generic_formula_id = NULL, updated_at = @at WHERE generic_formula_id = @id;",
                        new { at = Clock.Now(), id });
                }

                _db.Execute(connection, "UPDATE generic_formulas SET active = @active WHERE id = @id;",
                    new { active, id });
                var details = active
                    ? "Brought back. Former product links were not recreated."
                    : "Put away; unlinked " + unlinked + " product(s). Typed salt text was kept; no product or historical record was deleted.";
                _audit.RecordChange(connection, actor, "Inventory",
                    active ? "formula_restore" : "formula_put_away", "generic_formulas", id.ToString(),
                    before, FindFormula(connection, id), details);
                return ServiceResult.Success();
            });
        }
        catch (Exception exception)
        {
            _log.Error($"Salt {id} active state could not be changed.", exception);
            return ServiceResult.Fail("The salt could not be changed. " + exception.Message);
        }
    }

    /// <summary>Put-away unlinks current products but keeps their typed text; nothing is deleted.</summary>
    public ServiceResult SetBrandActive(User actor, int id, bool active)
    {
        if (!actor.CanManage)
        {
            return ServiceResult.Fail("Only the owner or a manager can change brands.");
        }

        try
        {
            return _db.Tx(connection =>
            {
                var before = FindBrand(connection, id);
                if (before is null)
                {
                    return ServiceResult.Fail("That brand no longer exists.");
                }
                if (before.Active == active)
                {
                    return ServiceResult.Success();
                }
                if (active && CatalogKeyTaken(connection, "brands", before.NameKey, id))
                {
                    return ServiceResult.Fail("Another brand uses this name. Rename one before bringing it back.");
                }

                var unlinked = 0;
                if (!active)
                {
                    unlinked = Convert.ToInt32(_db.Scalar(connection,
                        "SELECT COUNT(1) FROM products WHERE brand_id = @id;", new { id }) ?? 0);
                    _db.Execute(connection, @"
UPDATE products SET brand_id = NULL, updated_at = @at WHERE brand_id = @id;",
                        new { at = Clock.Now(), id });
                }

                _db.Execute(connection, "UPDATE brands SET active = @active WHERE id = @id;",
                    new { active, id });
                var details = active
                    ? "Brought back. Former product links were not recreated."
                    : "Put away; unlinked " + unlinked + " product(s). Typed brand text was kept; no product or historical record was deleted.";
                _audit.RecordChange(connection, actor, "Inventory",
                    active ? "brand_restore" : "brand_put_away", "brands", id.ToString(),
                    before, FindBrand(connection, id), details);
                return ServiceResult.Success();
            });
        }
        catch (Exception exception)
        {
            _log.Error($"Brand {id} active state could not be changed.", exception);
            return ServiceResult.Fail("The brand could not be changed. " + exception.Message);
        }
    }

    /// <summary>
    /// Portion 9 (#11): links a brand to its company, or clears the link with a null company.
    /// The company name is display metadata; no product row is rewritten.
    /// </summary>
    public ServiceResult SetBrandCompany(User actor, int brandId, int? companyId)
    {
        if (!actor.CanManage)
        {
            return ServiceResult.Fail("Only the owner or a manager can change brands.");
        }

        if (brandId <= 0)
        {
            return ServiceResult.Fail("Pick a brand first.");
        }

        try
        {
            return _db.Tx(connection =>
            {
                var before = FindBrand(connection, brandId);
                if (before is null)
                {
                    return ServiceResult.Fail("That brand no longer exists.");
                }

                string? companyName = null;
                if (companyId is not null)
                {
                    var company = FindCompany(connection, companyId.Value);
                    if (company is null || !company.Active)
                    {
                        return ServiceResult.Fail("Choose a company that is on the active list.");
                    }

                    companyName = company.Name;
                }

                if (before.CompanyId == companyId)
                {
                    return ServiceResult.Success();
                }

                _db.Execute(connection, @"
UPDATE brands SET company_id = @companyId, updated_at = @at WHERE id = @brandId;",
                    new { companyId, at = Clock.Now(), brandId });
                _audit.RecordChange(connection, actor, "Inventory", "brand_update", "brands",
                    brandId.ToString(), before, FindBrand(connection, brandId),
                    companyId is null
                        ? "Company link cleared; the brand keeps its name and products."
                        : "Company set to " + companyName + "; no product row was rewritten.");
                return ServiceResult.Success();
            });
        }
        catch (Exception exception)
        {
            _log.Error($"Brand {brandId} company could not be changed.", exception);
            return ServiceResult.Fail("The brand company could not be changed. " + exception.Message);
        }
    }

    /// <summary>
    /// Portion 9 (#11): one product's directory rows — the preferred supplier plus every genuine
    /// purchase-history supplier. Preferred first, then by name. History is read, never written.
    /// </summary>
    public ServiceResult<List<ProductSupplierDirectoryRow>> GetProductSuppliers(int productId)
    {
        try
        {
            using var connection = _db.Open();
            var rows = _db.Query<ProductSupplierDirectoryRow>(connection, @"
SELECT d.product_id AS productId, d.supplier_id AS supplierId, s.name AS supplierName,
       d.is_preferred AS isPreferred, d.last_purchased_at AS lastPurchasedAt
FROM product_supplier_directory d
JOIN suppliers s ON s.id = d.supplier_id
WHERE d.product_id = @productId
ORDER BY d.is_preferred DESC, s.name COLLATE NOCASE, s.id;",
                new { productId });
            return ServiceResult<List<ProductSupplierDirectoryRow>>.Success(rows);
        }
        catch (Exception exception)
        {
            _log.Error($"Supplier directory for product {productId} could not be loaded.", exception);
            return ServiceResult<List<ProductSupplierDirectoryRow>>.Fail("The supplier directory could not be loaded.");
        }
    }

    /// <summary>
    /// Portion 9 (#11): every supplier the product filter can offer — preferred and/or genuine
    /// history, including put-away suppliers with history, counting active products only.
    /// </summary>
    public ServiceResult<List<ProductSupplierOption>> GetProductSupplierOptions()
    {
        try
        {
            using var connection = _db.Open();
            var rows = _db.Query<ProductSupplierOption>(connection, @"
SELECT s.id AS supplierId, s.name,
       COUNT(DISTINCT p.id) AS productCount
FROM product_supplier_directory d
JOIN suppliers s ON s.id = d.supplier_id
JOIN products p ON p.id = d.product_id AND p.active = 1
GROUP BY s.id, s.name
ORDER BY s.name COLLATE NOCASE, s.id;");
            return ServiceResult<List<ProductSupplierOption>>.Success(rows);
        }
        catch (Exception exception)
        {
            _log.Error("The supplier filter list could not be loaded.", exception);
            return ServiceResult<List<ProductSupplierOption>>.Fail("The supplier filter list could not be loaded.");
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
        // Extra categories are filter tags, not a financial category. Refuse a new tag set without
        // a primary rather than guessing which tag should own historical/reporting totals. Products
        // without any categories remain valid; untouched legacy tag sets are never auto-rewritten.
        if (primaryCategoryId is null && clean.Count > 0)
        {
            throw new ValidationException("Choose a primary category before adding additional category tags.");
        }
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

    /// <summary>Portion 9: the preferred supplier is an explicit pick of a live supplier row.</summary>
    private void EnsureActiveSupplier(SqliteConnection connection, int? supplierId)
    {
        if (supplierId is null)
        {
            return;
        }

        var exists = Convert.ToInt32(_db.Scalar(connection,
            "SELECT COUNT(1) FROM suppliers WHERE id = @supplierId AND active = 1;",
            new { supplierId }) ?? 0);
        if (exists == 0)
        {
            throw new ValidationException("Choose a supplier that is on the active list.");
        }
    }

    private void EnsureActiveCatalogLinks(SqliteConnection connection, int? categoryId, int? companyId, int? formulaId = null, int? brandId = null)
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

        if (formulaId is not null)
        {
            var exists = Convert.ToInt32(_db.Scalar(connection,
                "SELECT COUNT(1) FROM generic_formulas WHERE id = @formulaId AND active = 1;",
                new { formulaId }) ?? 0);
            if (exists == 0)
            {
                throw new ValidationException("Choose a salt that is on the active list.");
            }
        }

        if (brandId is not null)
        {
            var exists = Convert.ToInt32(_db.Scalar(connection,
                "SELECT COUNT(1) FROM brands WHERE id = @brandId AND active = 1;",
                new { brandId }) ?? 0);
            if (exists == 0)
            {
                throw new ValidationException("Choose a brand that is on the active list.");
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

    private bool CatalogKeyTaken(SqliteConnection connection, string table, string key, int exceptId)
    {
        // Only four fixed callers pass a table name; never accept a table name from the UI.
        var sql = table == "generic_formulas"
            ? "SELECT COUNT(1) FROM generic_formulas WHERE name_key = @key AND id <> @exceptId;"
            : "SELECT COUNT(1) FROM brands WHERE name_key = @key AND id <> @exceptId;";
        return Convert.ToInt32(_db.Scalar(connection, sql, new { key, exceptId }) ?? 0) > 0;
    }

    private GenericFormula? FindFormula(SqliteConnection connection, int id) =>
        _db.QuerySingle<GenericFormula>(connection, @"
SELECT f.id, f.name, f.name_key AS nameKey, f.active, f.sort_order AS sortOrder,
       f.created_at AS createdAt, f.updated_at AS updatedAt,
       (SELECT COUNT(1) FROM products p WHERE p.generic_formula_id = f.id) AS productCount
FROM generic_formulas f WHERE f.id = @id;", new { id });

    private BrandDefinition? FindBrand(SqliteConnection connection, int id) =>
        _db.QuerySingle<BrandDefinition>(connection, @"
SELECT b.id, b.company_id AS companyId, b.name, b.name_key AS nameKey, b.active,
       b.sort_order AS sortOrder, b.created_at AS createdAt, b.updated_at AS updatedAt,
       (SELECT COUNT(1) FROM products p WHERE p.brand_id = b.id) AS productCount
FROM brands b WHERE b.id = @id;", new { id });

    private string CatalogNameOf(SqliteConnection connection, string table, int id) =>
        Convert.ToString(_db.Scalar(connection,
            table == "generic_formulas"
                ? "SELECT name FROM generic_formulas WHERE id = @id;"
                : "SELECT name FROM brands WHERE id = @id;", new { id }) ?? string.Empty) ?? string.Empty;

    /// <summary>
    /// Portion 8: a link always wins over typing. Empty text is filled from the catalog; text that
    /// names a different salt/brand is refused instead of guessed. Returns the text to store.
    /// </summary>
    private string? LinkedCatalogText(SqliteConnection connection, string table, int? id, string? typed, string label)
    {
        if (id is null)
        {
            return NullIfEmpty(typed);
        }

        var name = CatalogNameOf(connection, table, id.Value);
        if (name.Length == 0)
        {
            throw new ValidationException("The picked " + label + " is no longer on the list.");
        }

        if (!string.IsNullOrWhiteSpace(typed) && CatalogKeyOf(typed) != CatalogKeyOf(name))
        {
            throw new ValidationException(
                "The typed " + label + " does not match the picked " + label + " '" + name +
                "'. Pick again, or clear the typed text and let the pick fill it.");
        }

        return name;
    }

    private bool LinkedNameMatches(SqliteConnection connection, string table, int id, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var name = CatalogNameOf(connection, table, id);
        return name.Length > 0 && CatalogKeyOf(text) == CatalogKeyOf(name);
    }

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
            "generic_formulas" => "SELECT IFNULL(MAX(sort_order), 0) + 10 FROM generic_formulas;",
            "brands" => "SELECT IFNULL(MAX(sort_order), 0) + 10 FROM brands;",
            _ => throw new ArgumentOutOfRangeException(nameof(table))
        };
        return Convert.ToInt32(_db.Scalar(connection, sql) ?? 10);
    }

    private static string CleanCatalogName(string? name) => (name ?? string.Empty).Trim();

    /// <summary>Display spelling: trimmed with single spaces, original casing kept.</summary>
    private static string CleanDisplayName(string? value) =>
        string.Join(" ", (value ?? string.Empty).Trim()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Uniqueness key: trimmed, single-spaced, lowercased. The schema stores it as name_key.</summary>
    private static string CatalogKeyOf(string? value) => NormalizeUnitName(value);

    private static bool IsValidSortOrder(int value) => value is >= 0 and <= MaxCatalogSortOrder;

    /// <summary>
    /// Portion 7: two variants of one medicine share a name by design (50ml / 100ml), so a
    /// repeated name is a warning, never a refusal. The message names the existing variants, so
    /// the owner sees at a glance whether the new row is really a new variant or a double entry.
    /// A warning must never fail a save, so a read problem here stays silent.
    /// </summary>
    private string[] SameNameWarnings(int newId, string name)
    {
        try
        {
            using var connection = _db.Open();
            var siblings = _db.Query<SiblingRow>(connection, @"
SELECT strength_value AS strengthValue, strength_unit AS strengthUnit, pack_size_label AS packSizeLabel
FROM products
WHERE active = 1 AND id <> @id AND name = @name COLLATE NOCASE
ORDER BY id
LIMIT 4;", new { id = newId, name });
            if (siblings.Count == 0)
            {
                return Array.Empty<string>();
            }

            var labels = siblings.Select(sibling =>
            {
                var parts = new List<string>();
                if (sibling.StrengthValue > 0 && !string.IsNullOrWhiteSpace(sibling.StrengthUnit))
                {
                    parts.Add(Utils.Qty.Format(sibling.StrengthValue) + " " + sibling.StrengthUnit!.Trim());
                }
                if (!string.IsNullOrWhiteSpace(sibling.PackSizeLabel))
                {
                    parts.Add(sibling.PackSizeLabel.Trim());
                }
                return parts.Count == 0 ? "no strength or pack noted" : string.Join(", ", parts);
            }).ToList();

            return siblings.Count == 1
                ? new[] { "Another medicine shares the name '" + name + "' (" + labels[0] + "). Check strength and pack before selling." }
                : new[] { siblings.Count + " other medicines share the name '" + name + "' (" + string.Join("; ", labels) + "). Check strength and pack before selling." };
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private sealed class SiblingRow
    {
        public double StrengthValue { get; set; }

        public string? StrengthUnit { get; set; }

        public string? PackSizeLabel { get; set; }
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
