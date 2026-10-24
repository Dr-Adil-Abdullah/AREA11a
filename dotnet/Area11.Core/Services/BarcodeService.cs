using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>
/// Several barcodes for one medicine (F-43, Spec 8.1.5, 35.6, R-09).
///
/// **Why a table and not one more column.** A pharmacy sells the same medicine by the tablet, by the
/// strip and by the box, and each package carries its own printed code. One `products.barcode`
/// column can only remember one of them, so the person at the counter has to look the medicine up by
/// name while a scanner sits unused. `product_barcodes` gives each pack its own code, and the code
/// says which pack was scanned - scanning a box adds a box, not one tablet.
///
/// **The old column is still read** (migration 001 wrote it and existing databases still hold it):
/// a scan checks the new table first and the old single code second, so a shop that never opens this
/// window loses nothing. The window itself never writes the old column - one place owns a barcode.
///
/// **Nothing is deleted** (D-00b): a code that is no longer printed is put away; the row stays, the
/// old boxes on the shelf stay explainable, and the code can be given to a new box later.
///
/// The scanner is a keyboard (R-09): it types the code and presses Enter into the counter's search
/// box. Nothing here depends on a driver, so a missing scanner never stops the shop (Z-07).
/// </summary>
public sealed class BarcodeService
{
    public const int MinLength = 3;

    public const int MaxLength = 64;

    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    public BarcodeService(
        Database db,
        AuditService audit,
        PermissionService permissions,
        LogService log)
    {
        _db = db;
        _audit = audit;
        _permissions = permissions;
        _log = log;
    }

    /// <summary>Every code of one medicine, in the order it was added; put-away ones only if asked.</summary>
    public ServiceResult<List<ProductBarcode>> List(User? user, int productId, bool includeInactive = false)
    {
        if (!_permissions.Can(user, Modules.Products))
        {
            return ServiceResult<List<ProductBarcode>>.Fail(
                "You do not have permission to change barcodes.");
        }

        try
        {
            using var connection = _db.Open();

            var rows = _db.Query<ProductBarcode>(connection, @"
SELECT id AS id, product_id AS productId, barcode AS barcode, pack AS pack, note AS note,
       active AS active, created_at AS createdAt
FROM product_barcodes
WHERE product_id = @productId AND (@includeInactive = 1 OR active = 1)
ORDER BY pack, id;",
                new { productId, includeInactive = includeInactive ? 1 : 0 });

            return ServiceResult<List<ProductBarcode>>.Success(rows);
        }
        catch (Exception exception)
        {
            _log.Error($"The barcodes of product {productId} could not be listed.", exception);
            return ServiceResult<List<ProductBarcode>>.Fail(
                "The barcodes could not be loaded. " + exception.Message);
        }
    }

    /// <summary>Adds one printed code to one pack of one medicine.</summary>
    public ServiceResult<ProductBarcode> Add(
        User? user,
        int productId,
        string? code,
        string? pack,
        string? note)
    {
        if (!_permissions.Can(user, Modules.Products))
        {
            return ServiceResult<ProductBarcode>.Fail(
                "You do not have permission to add a barcode.");
        }

        var clean = (code ?? string.Empty).Trim();
        var cleanPack = BarcodePack.Clean(pack);

        if (clean.Length < MinLength)
        {
            return ServiceResult<ProductBarcode>.Fail(
                "A barcode needs at least " + MinLength + " letters or digits.");
        }

        if (clean.Length > MaxLength)
        {
            return ServiceResult<ProductBarcode>.Fail(
                "That code is too long - it must be under " + MaxLength + " characters.");
        }

        if (clean.Any(char.IsWhiteSpace))
        {
            return ServiceResult<ProductBarcode>.Fail(
                "A barcode has no spaces in it. Check the scanner - it may have read only half the code.");
        }

        if (!BarcodePack.IsKnown(cleanPack))
        {
            return ServiceResult<ProductBarcode>.Fail(
                "Say which pack this code is printed on: unit, strip or box.");
        }

        try
        {
            return _db.Tx(connection =>
            {
                var product = _db.QuerySingle<string>(connection,
                    "SELECT name FROM products WHERE id = @id AND active = 1;", new { id = productId });

                if (product is null)
                {
                    return ServiceResult<ProductBarcode>.Fail(
                        "That medicine is not in the list any more.");
                }

                // The same digits on two medicines would make the counter a coin toss, so a live code
                // belongs to exactly one product - including the old single-barcode column.
                if (CodeIsTaken(connection, clean, exceptBarcodeId: 0, exceptProductId: 0))
                {
                    return ServiceResult<ProductBarcode>.Fail(
                        "That barcode already belongs to a medicine. Check the box again.");
                }

                _db.Execute(connection, @"
INSERT INTO product_barcodes(product_id, barcode, pack, note, active, created_at)
VALUES(@productId, @barcode, @pack, @note, 1, @at);",
                    new
                    {
                        productId,
                        barcode = clean,
                        pack = cleanPack,
                        note = string.IsNullOrWhiteSpace(note) ? null : note!.Trim(),
                        at = Clock.Now()
                    });

                var id = (int)_db.NextId(connection);

                _audit.Record(connection, user, Modules.Products, "barcode_add",
                    "product_barcodes", id.ToString(), newValue: clean + " · " + cleanPack,
                    details: product);

                return ServiceResult<ProductBarcode>.Success(new ProductBarcode
                {
                    Id = id,
                    ProductId = productId,
                    Barcode = clean,
                    Pack = cleanPack,
                    Note = string.IsNullOrWhiteSpace(note) ? null : note!.Trim(),
                    Active = true,
                    CreatedAt = Clock.Now()
                });
            });
        }
        catch (Exception exception)
        {
            _log.Error($"A barcode ({clean}) could not be added to product {productId}.", exception);
            return ServiceResult<ProductBarcode>.Fail(
                "The barcode could not be added. " + exception.Message);
        }
    }

    /// <summary>Puts a code away (the box is no longer printed) or brings it back. The row stays.</summary>
    public ServiceResult SetActive(User? user, int barcodeId, bool active)
    {
        if (!_permissions.Can(user, Modules.Products))
        {
            return ServiceResult.Fail("You do not have permission to change barcodes.");
        }

        try
        {
            return _db.Tx(connection =>
            {
                var row = Read(connection, barcodeId);

                if (row is null)
                {
                    return ServiceResult.Fail("That barcode is not in the list any more.");
                }

                if (row.Active == active)
                {
                    return ServiceResult.Success();
                }

                if (active)
                {
                    // Bringing a code back must not create a second live owner for the same digits.
                    if (CodeIsTaken(connection, row.Barcode, exceptBarcodeId: row.Id, exceptProductId: row.ProductId))
                    {
                        return ServiceResult.Fail(
                            "These digits are already in use - by another medicine, or by the old barcode box on the Products page. Change that first.");
                    }
                }

                _db.Execute(connection, "UPDATE product_barcodes SET active = @active WHERE id = @id;",
                    new { active = active ? 1 : 0, id = barcodeId });

                _audit.Record(connection, user, Modules.Products,
                    active ? "barcode_on" : "barcode_off", "product_barcodes", barcodeId.ToString(),
                    oldValue: row.Active ? "works" : "put away",
                    newValue: active ? "works" : "put away",
                    details: row.Barcode);

                return ServiceResult.Success();
            });
        }
        catch (Exception exception)
        {
            _log.Error($"The barcode {barcodeId} could not be switched.", exception);
            return ServiceResult.Fail("The barcode could not be changed. " + exception.Message);
        }
    }

    /// <summary>
    /// What a scanned code means. The counter calls this; the old single-column code is honoured so
    /// a shop that never opened the barcode window can still scan.
    /// </summary>
    public ServiceResult<BarcodeMatch> Find(User? user, string? code)
    {
        if (!_permissions.Can(user, Modules.Pos))
        {
            return ServiceResult<BarcodeMatch>.Fail(
                "You do not have permission to scan at the counter.");
        }

        var clean = (code ?? string.Empty).Trim();

        if (clean.Length == 0)
        {
            return ServiceResult<BarcodeMatch>.Fail("Scan a barcode or type its digits.");
        }

        try
        {
            using var connection = _db.Open();

            var found = _db.QuerySingle<BarcodeMatch>(connection, @"
SELECT b.id AS barcodeId, b.product_id AS productId, p.name AS productName,
       b.barcode AS barcode, b.pack AS pack
FROM product_barcodes b
JOIN products p ON p.id = b.product_id
WHERE b.active = 1 AND p.active = 1 AND LOWER(b.barcode) = LOWER(@code);",
                new { code = clean });

            if (found is not null)
            {
                return ServiceResult<BarcodeMatch>.Success(found);
            }

            var legacy = _db.QuerySingle<BarcodeMatch>(connection, @"
SELECT 0 AS barcodeId, p.id AS productId, p.name AS productName,
       TRIM(p.barcode) AS barcode, 'base' AS pack
FROM products p
WHERE p.active = 1 AND p.barcode IS NOT NULL AND LOWER(TRIM(p.barcode)) = LOWER(@code);",
                new { code = clean });

            return legacy is null
                ? ServiceResult<BarcodeMatch>.Fail(
                    "No medicine has the barcode \"" + clean + "\". Check the digits, or add the code on the Products page.")
                : ServiceResult<BarcodeMatch>.Success(legacy);
        }
        catch (Exception exception)
        {
            _log.Error($"The barcode {clean} could not be looked up.", exception);
            return ServiceResult<BarcodeMatch>.Fail(
                "The barcode could not be checked. " + exception.Message);
        }
    }

    /// <summary>
    /// Every live code with its medicine and pack - the counter loads this once, so typing one digit
    /// filters instantly without asking the database on every key (Spec 12.1.1, R-09).
    /// </summary>
    public ServiceResult<List<BarcodeMatch>> All(User? user)
    {
        if (!_permissions.Can(user, Modules.Pos))
        {
            return ServiceResult<List<BarcodeMatch>>.Fail(
                "You do not have permission to scan at the counter.");
        }

        try
        {
            using var connection = _db.Open();

            var rows = _db.Query<BarcodeMatch>(connection, @"
SELECT b.id AS barcodeId, b.product_id AS productId, p.name AS productName,
       b.barcode AS barcode, b.pack AS pack
FROM product_barcodes b
JOIN products p ON p.id = b.product_id
WHERE b.active = 1 AND p.active = 1
ORDER BY p.name COLLATE NOCASE, b.pack;");

            // The old single code is one more barcode of one unit, unless the same digits are already
            // in the new table (then the table is the truth).
            var legacy = _db.Query<BarcodeMatch>(connection, @"
SELECT 0 AS barcodeId, p.id AS productId, p.name AS productName,
       TRIM(p.barcode) AS barcode, 'base' AS pack
FROM products p
WHERE p.active = 1 AND p.barcode IS NOT NULL AND TRIM(p.barcode) <> ''
  AND NOT EXISTS (
      SELECT 1 FROM product_barcodes b
      WHERE b.active = 1 AND LOWER(b.barcode) = LOWER(TRIM(p.barcode))
  )
ORDER BY p.name COLLATE NOCASE;");

            rows.AddRange(legacy);

            return ServiceResult<List<BarcodeMatch>>.Success(rows);
        }
        catch (Exception exception)
        {
            _log.Error("The barcode list could not be loaded.", exception);
            return ServiceResult<List<BarcodeMatch>>.Fail(
                "The barcodes could not be loaded. " + exception.Message);
        }
    }

    /// <summary>
    /// True when the digits already mean something. Adding a code refuses even the same product's
    /// old column (one code entered twice is one meaning written twice). Switching a code back on
    /// ignores its own row and its own product, because migration 022 copied the old column into
    /// this table - otherwise the owner could never bring that row back.
    /// </summary>
    private bool CodeIsTaken(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        string code,
        int exceptBarcodeId,
        int exceptProductId)
    {
        var inTable = Convert.ToInt32(_db.Scalar(connection, @"
SELECT COUNT(1) FROM product_barcodes
WHERE active = 1 AND LOWER(barcode) = LOWER(@code) AND id <> @exceptId;",
            new { code, exceptId = exceptBarcodeId }) ?? 0) > 0;

        if (inTable)
        {
            return true;
        }

        return Convert.ToInt32(_db.Scalar(connection, @"
SELECT COUNT(1) FROM products
WHERE id <> @exceptProductId AND barcode IS NOT NULL AND TRIM(barcode) <> ''
  AND LOWER(TRIM(barcode)) = LOWER(@code);",
            new { code, exceptProductId }) ?? 0) > 0;
    }

    private ProductBarcode? Read(Microsoft.Data.Sqlite.SqliteConnection connection, int barcodeId)
    {
        return _db.QuerySingle<ProductBarcode>(connection, @"
SELECT id AS id, product_id AS productId, barcode AS barcode, pack AS pack, note AS note,
       active AS active, created_at AS createdAt
FROM product_barcodes WHERE id = @id;", new { id = barcodeId });
    }
}
