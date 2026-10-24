using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>
/// The reorder pad (F-38, Spec 10.5 &amp; 20.3): what is running out, and the message the owner sends
/// the supplier on WhatsApp.
///
/// **Where the numbers come from.** Each medicine carries one minimum (`products.reorder_level`).
/// The three bands the spec asks for are read off that single number - out of stock, at/below the
/// minimum, and within half the minimum - so the shop never has to keep a second scale in step with
/// the first (D-55).
///
/// **What is suggested.** Enough to reach **twice** the minimum, rounded up to a whole tablet. The
/// owner can type any quantity on the pad; the suggestion is a starting number, not a promise.
/// A medicine with no minimum and an empty shelf is still shown (it is out of stock - that must
/// never be hidden), and it suggests one unit so it can go on the pad.
///
/// **Who to order from.** The supplier of the **last purchase** of that medicine. Products carry no
/// supplier column, so inventing one would create a second, disagreeing source of truth.
///
/// **WhatsApp is free** (Spec 3.8.9, T-09): the program builds a `wa.me` link and opens it in the
/// browser/WhatsApp desktop. No API key, no internet in the shop's own database.
/// </summary>
public sealed class ReorderService
{
    /// <summary>"Running low" reaches this far above the minimum (half of it).</summary>
    public const double LowBand = 1.5;

    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly SettingsService _settings;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    public ReorderService(
        Database db,
        AuditService audit,
        SettingsService settings,
        PermissionService permissions,
        LogService log)
    {
        _db = db;
        _audit = audit;
        _settings = settings;
        _permissions = permissions;
        _log = log;
    }

    /// <summary>Every medicine that is out, at its minimum, or running low - biggest problem first.</summary>
    public ServiceResult<ReorderReport> Report(User? user)
    {
        if (!_permissions.Can(user, Modules.Reorder))
        {
            return ServiceResult<ReorderReport>.Fail("You do not have permission to see the reorder pad.");
        }

        var report = new ReorderReport();

        try
        {
            if (!_settings.GetBool("reorder.enabled", true))
            {
                report.IsSwitchedOff = true;
                return ServiceResult<ReorderReport>.Success(report);
            }

            using var connection = _db.Open();

            var products = _db.Query<ProductRow>(connection, @"
SELECT p.id AS productId, p.name AS name, p.generic AS generic,
       (SELECT c.name FROM companies c WHERE c.id = p.company_id) AS company,
       p.base_unit AS baseUnit, p.reorder_level AS levelQty,
       p.box_strips AS boxStrips, p.strip_tablets AS stripTablets,
       IFNULL((SELECT SUM(b.qty_base) FROM batches b WHERE b.product_id = p.id AND b.active = 1), 0) AS stockQty
FROM products p
WHERE p.active = 1
ORDER BY p.name COLLATE NOCASE;");

            // The supplier of the last purchase of each medicine. ROW_NUMBER keeps it one query for
            // the whole shop instead of one query per row (the page opens on every visit).
            var lastSupply = _db.Query<LastSupplyRow>(connection, @"
SELECT productId, supplierId, supplierName, phone, lastDate, costPaisa FROM (
    SELECT pi.product_id AS productId, pu.supplier_id AS supplierId, s.name AS supplierName,
           s.phone AS phone, pu.date AS lastDate, pi.cost_paisa AS costPaisa,
           ROW_NUMBER() OVER (PARTITION BY pi.product_id ORDER BY pu.date DESC, pu.id DESC, pi.id DESC) AS rowNo
    FROM purchase_items pi
    JOIN purchases pu ON pu.id = pi.purchase_id
    JOIN suppliers s ON s.id = pu.supplier_id
    WHERE pu.supplier_id IS NOT NULL
)
WHERE rowNo = 1;")
                .GroupBy(row => row.ProductId)
                .ToDictionary(group => group.Key, group => group.First());

            foreach (var product in products)
            {
                var tier = TierFor(product.StockQty, product.LevelQty);

                // A watched medicine that is comfortably above its minimum is not on this page.
                if (tier is null)
                {
                    continue;
                }

                var row = new ReorderRow
                {
                    ProductId = product.ProductId,
                    Name = product.Name,
                    Generic = product.Generic,
                    Company = product.Company,
                    BaseUnit = product.BaseUnit,
                    BoxStrips = product.BoxStrips,
                    StripTablets = product.StripTablets,
                    StockQty = product.StockQty,
                    LevelQty = product.LevelQty,
                    SuggestQty = SuggestFor(product.StockQty, product.LevelQty),
                    Tier = tier
                };

                if (lastSupply.TryGetValue(product.ProductId, out var supply))
                {
                    row.SupplierId = supply.SupplierId;
                    row.SupplierName = supply.SupplierName;
                    row.SupplierPhone = supply.Phone;
                    row.LastPurchaseDate = supply.LastDate;
                    row.LastCostPaisa = supply.CostPaisa;
                }

                report.Rows.Add(row);
            }

            // Red first, then amber, then the quiet ones; name breaks every tie so the list never
            // shuffles between two reads.
            report.Rows.Sort((left, right) =>
            {
                var byTier = TierOrder(left.Tier).CompareTo(TierOrder(right.Tier));

                return byTier != 0
                    ? byTier
                    : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            });

            return ServiceResult<ReorderReport>.Success(report);
        }
        catch (Exception exception)
        {
            _log.Error("The reorder list could not be built.", exception);
            return ServiceResult<ReorderReport>.Fail(
                "The reorder list could not be built. " + exception.Message);
        }
    }

    /// <summary>
    /// Saves the owner's minimum for one medicine. Only this one column moves; cost, rates, pack and
    /// rack are untouched (Spec 8.1.4.1 - a partial edit must never zero the rest).
    /// </summary>
    public ServiceResult SetLevel(User? user, int productId, double level)
    {
        if (!_permissions.Can(user, Modules.Reorder))
        {
            return ServiceResult.Fail("You do not have permission to change a minimum.");
        }

        if (level < 0)
        {
            return ServiceResult.Fail("A minimum quantity cannot be less than zero.");
        }

        if (level > 100000)
        {
            return ServiceResult.Fail("That minimum is too large. Check the number and the unit.");
        }

        try
        {
            return _db.Tx(connection =>
            {
                var before = _db.QuerySingle<double?>(connection,
                    "SELECT reorder_level FROM products WHERE id = @id;", new { id = productId });

                if (before is null)
                {
                    return ServiceResult.Fail("That medicine is not in the product list any more.");
                }

                if (Math.Abs(before.Value - level) < 0.0005)
                {
                    return ServiceResult.Success();
                }

                _db.Execute(connection, @"
UPDATE products SET reorder_level = @level, updated_at = @at WHERE id = @id;",
                    new { level, at = Clock.Now(), id = productId });

                _audit.RecordChange(connection, user, Modules.Reorder, "level_change", "products",
                    productId.ToString(), new { reorder_level = before.Value }, new { reorder_level = level },
                    "Minimum quantity (reorder pad)");

                return ServiceResult.Success();
            });
        }
        catch (Exception exception)
        {
            _log.Error("A reorder minimum could not be saved.", exception);
            return ServiceResult.Fail("The minimum could not be saved. " + exception.Message);
        }
    }

    /// <summary>
    /// Writes the one line the black box needs when an order leaves the shop: who it went to and
    /// how many medicines it asked for. The text itself is not stored - it can be long, and it is
    /// rebuilt from the same numbers any time.
    /// </summary>
    public void RecordOrder(User? user, string? supplierName, int itemCount)
    {
        var who = string.IsNullOrWhiteSpace(supplierName) ? "no supplier chosen" : supplierName!.Trim();

        _audit.RecordStandalone(user, Modules.Reorder, "order_text",
            who + " - " + itemCount + " medicine(s) on the pad");
    }

    // ------------------------------------------------------------------ text

    /// <summary>
    /// The message a supplier reads. Plain English with one line per medicine, because the shop's
    /// screens and slips are English (D-00) and a supplier's manager may not read Urdu script.
    /// </summary>
    public static string BuildText(
        IEnumerable<ReorderLine> lines,
        string? supplierName = null,
        string? note = null,
        string? storeName = null)
    {
        var chosen = lines.Where(line => line.Quantity > 0).ToList();
        var store = string.IsNullOrWhiteSpace(storeName) ? "Area11" : storeName!.Trim();
        var body = new List<string>
        {
            string.IsNullOrWhiteSpace(supplierName) ? "Salam," : "Salam, " + supplierName!.Trim(),
            store + " order:",
            string.Empty
        };

        var index = 1;

        foreach (var line in chosen)
        {
            var extra = new[] { line.Company, line.Generic }
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Select(part => part!.Trim())
                .ToList();

            var head = index + ". " + line.Name.Trim();
            if (extra.Count > 0)
            {
                head += " (" + string.Join(" - ", extra) + ")";
            }

            body.Add(head + " - " + line.QuantityLabel);
            index++;
        }

        body.Add(string.Empty);
        body.Add("Total: " + chosen.Count + " item(s).");

        if (!string.IsNullOrWhiteSpace(note))
        {
            body.Add(note!.Trim());
        }

        body.Add("Please confirm the items and the rate. Thank you.");

        return string.Join("\n", body);
    }

    /// <summary>
    /// `wa.me` link with the text already inside (Spec 20.3, T-09). A mobile typed the Pakistani way -
    /// `0300-1234567` - becomes `923001234567`; `+92 ...` and `92...` are accepted as they are.
    /// With no number the link still opens WhatsApp and lets the owner pick the contact.
    /// </summary>
    public static string BuildLink(string? phone, string text)
    {
        var digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());

        if (digits.StartsWith("0092", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }

        var number = digits.StartsWith("92", StringComparison.Ordinal) && digits.Length >= 12
            ? digits
            : digits.Length == 11 && digits.StartsWith('0')
                ? "92" + digits[1..]
                : digits;

        var head = number.Length > 0 ? "https://wa.me/" + number : "https://wa.me/";

        return head + "?text=" + Uri.EscapeDataString(text ?? string.Empty);
    }

    // ------------------------------------------------------------------ bands

    /// <summary>Which band a shelf is in, or null when it is comfortably stocked.</summary>
    public static string? TierFor(double stock, double level)
    {
        if (stock <= 0)
        {
            return ReorderTier.Out;
        }

        if (level <= 0)
        {
            return null;
        }

        if (stock <= level)
        {
            return ReorderTier.Critical;
        }

        return stock <= level * LowBand ? ReorderTier.Low : null;
    }

    /// <summary>How much to ask for: enough to reach twice the minimum, at least one unit.</summary>
    public static double SuggestFor(double stock, double level)
    {
        var target = level > 0 ? level * 2 : 1;
        var need = target - stock;

        return need <= 0 ? 1 : Math.Ceiling(need);
    }

    private static int TierOrder(string tier) => tier switch
    {
        ReorderTier.Out => 0,
        ReorderTier.Critical => 1,
        _ => 2
    };

    // ------------------------------------------------------------------ rows

    private sealed class ProductRow
    {
        public int ProductId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Generic { get; set; }

        public string? Company { get; set; }

        public string BaseUnit { get; set; } = "unit";

        public int BoxStrips { get; set; }

        public int StripTablets { get; set; }

        public double StockQty { get; set; }

        public double LevelQty { get; set; }
    }

    private sealed class LastSupplyRow
    {
        public int ProductId { get; set; }

        public int SupplierId { get; set; }

        public string SupplierName { get; set; } = string.Empty;

        public string? Phone { get; set; }

        public string? LastDate { get; set; }

        public long CostPaisa { get; set; }
    }
}
