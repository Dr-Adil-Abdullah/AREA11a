namespace Area11.WPF.Services;

/// <summary>
/// The shop's editable configuration. Every value can be changed from Settings → nothing is final.
/// Keys are grouped by the prefix before the dot.
/// </summary>
public static class SettingsDefaults
{
    public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["store.name"] = "Area11 Pharmacy",
        ["store.address"] = string.Empty,
        ["store.phone"] = string.Empty,
        ["store.footerNote"] = "Thank you for shopping with us",
        ["store.terms"] = "Returns accepted within 7 days with receipt.",
        ["brand.appName"] = "Area11",
        ["brand.shortName"] = "Area11",
        ["brand.primaryColor"] = "#047857",
        ["owner.name"] = string.Empty,

        ["bill.salePrefix"] = "INV-",
        ["bill.purchasePrefix"] = "PINV-",
        ["bill.returnPrefix"] = "RET-",
        ["bill.provisionalPrefix"] = "PR-",
        ["bill.quarantinePrefix"] = "Q-",
        ["bill.supplierReturnPrefix"] = "SR-",
        ["bill.stockTakePrefix"] = "ST-",
        ["bill.numberPadding"] = "4",
        ["bill.roundMode"] = "down10",
        ["bill.roundTo"] = "10",

        ["tax.enabled"] = "false",
        ["tax.percent"] = "0",
        ["tax.label"] = "Sales Tax",

        ["discount.enabled"] = "true",
        ["discount.mode"] = "margin",
        ["discount.maxPercentCashier"] = "5",
        ["discount.maxPercentManager"] = "20",
        ["discount.blockBelowCost"] = "true",

        ["stock.allowNegative"] = "true",
        ["stock.warnNegative"] = "true",

        ["returns.requireInvoice"] = "true",
        ["returns.maxDays"] = "7",

        ["credit.blockOverLimit"] = "false",
        ["credit.managerCanOverride"] = "true",

        ["expiry.warnDays"] = "365",
        ["expiry.criticalDays"] = "90",

        ["reorder.enabled"] = "true",

        ["loyalty.enabled"] = "false",
        ["loyalty.rupeesPerPoint"] = "100",

        ["printer.width"] = "80",
        ["printer.autoCut"] = "true",
        ["printer.copies"] = "1",
        ["receipt.datePosition"] = "top",
        ["receipt.showDiscount"] = "true",
        ["receipt.showSavings"] = "true",
        ["receipt.showCostColumns"] = "false",

        ["security.autoLockMinutes"] = "15",
        ["security.sessionHours"] = "12",

        ["backup.autoEnabled"] = "false",
        ["backup.everyHours"] = "24",
        ["backup.keep"] = "7",

        ["sync.enabled"] = "false"
    };
}
