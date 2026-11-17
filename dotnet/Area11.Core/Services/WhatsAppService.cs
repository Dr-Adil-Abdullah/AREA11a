using System.IO;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>
/// Portion 12: one shared road to WhatsApp. Every page that can send - the order pad, a bill, a
/// statement, a report - builds the same native-app link, prepares the same PDF file, and tells
/// the owner where the file sits so she can attach it by hand. Opening WhatsApp is never evidence
/// of delivery: the owner presses Send herself, and when the app is missing the page says so.
///
/// Money note: this service only *describes* documents that already exist. It never posts, pays
/// or changes a balance - a share that moved money would be a second, invisible counter.
/// </summary>
public sealed class WhatsAppService
{
    private readonly SettingsService _settings;
    private readonly LogService _log;

    public WhatsAppService(SettingsService settings, LogService log)
    {
        _settings = settings;
        _log = log;
    }

    /// <summary>
    /// The digits WhatsApp dials. A mobile typed the Pakistani way - `0300-1234567` - becomes
    /// `923001234567`; `+92 ...` and `92...` are accepted as they are; null or blank means "let
    /// the owner pick the contact". Same rule as the reorder link (T-09), one spelling everywhere.
    /// </summary>
    public static string? NormalizePhone(string? phone)
    {
        var digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
        {
            return null;
        }

        if (digits.StartsWith("92", StringComparison.Ordinal))
        {
            return digits;
        }

        if (digits.StartsWith("0", StringComparison.Ordinal) && digits.Length > 1)
        {
            return "92" + digits[1..];
        }

        return digits;
    }

    /// <summary>The native-app link: opens WhatsApp Desktop (or the phone app) with the text ready.</summary>
    public static string BuildAppLink(string? phone, string text)
    {
        var number = NormalizePhone(phone);
        var link = "whatsapp://send?text=" + Uri.EscapeDataString(text ?? string.Empty);
        return number is null ? link : "whatsapp://send?phone=" + number + "&text=" + Uri.EscapeDataString(text ?? string.Empty);
    }

    /// <summary>The web fallback: the same text through the browser when the app is missing.</summary>
    public static string BuildWebLink(string? phone, string text)
    {
        var number = NormalizePhone(phone);
        var escaped = Uri.EscapeDataString(text ?? string.Empty);
        return number is null
            ? "https://wa.me/?text=" + escaped
            : "https://wa.me/" + number + "?text=" + escaped;
    }

    /// <summary>
    /// The customer's profile as WhatsApp text: who, where to call, what is owed. Short on purpose -
    /// the statement carries the movements; this is the nudge the owner forwards.
    /// </summary>
    public string BuildCustomerProfile(string name, string? phone, long balancePaisa, long limitPaisa)
    {
        var lines = new List<string>
        {
            "Salam, " + (name ?? string.Empty).Trim() + ",",
            "Your khata at " + ShopName() + ": " + Money.Format(balancePaisa)
                + (balancePaisa > 0 ? " outstanding." : balancePaisa < 0 ? " advance with us." : " settled, thank you.")
        };

        if (limitPaisa > 0)
        {
            lines.Add("Credit limit: " + Money.Format(limitPaisa) + ".");
        }

        if (!string.IsNullOrWhiteSpace(phone))
        {
            lines.Add("Shop: " + ShopPhone());
        }

        lines.Add("Please visit or reply to clear your balance. Thank you.");
        return string.Join("\n", lines);
    }

    /// <summary>The supplier's profile as WhatsApp text: who, and what the shop still has to pay.</summary>
    public string BuildSupplierProfile(string name, string? agency, long payablePaisa)
    {
        var who = (name ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(agency))
        {
            who += " (" + agency!.Trim() + ")";
        }

        return string.Join("\n", new[]
        {
            "Salam, " + who + ",",
            ShopName() + " payable: " + Money.Format(payablePaisa) + ".",
            "Please confirm the balance. Thank you."
        });
    }

    public bool PreparePdfEnabled => _settings.GetBool(MasterFoundationSettings.WhatsAppPreparePdf, true);

    /// <summary>
    /// Where the PDF files land: the owner's folder when she set one, else the shop's `exports`
    /// folder beside the data. The folder is created on use, never on reading.
    /// </summary>
    public string ExportDirectory
    {
        get
        {
            var chosen = (_settings.GetString(MasterFoundationSettings.WhatsAppExportDirectory) ?? string.Empty).Trim();
            return chosen.Length > 0 ? chosen : AppPaths.ExportsFolder;
        }
    }

    /// <summary>
    /// Writes the document as a PDF file and hands back its full path, so the page can tell the
    /// owner exactly what to attach. Overwrites a file of the same name: re-preparing a bill must
    /// not litter the folder with numbered twins.
    /// </summary>
    public ServiceResult<string> PreparePdf(string title, IEnumerable<string> lines, string fileName)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return ServiceResult<string>.Fail("The PDF needs a title.");
        }

        var safe = SanitizeFileName(fileName);
        if (safe.Length == 0)
        {
            return ServiceResult<string>.Fail("The PDF needs a file name.");
        }

        try
        {
            var folder = ExportDirectory;
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, safe);
            File.WriteAllBytes(path, PdfWriter.Write(title, lines ?? Enumerable.Empty<string>()));
            return ServiceResult<string>.Success(path);
        }
        catch (Exception exception)
        {
            _log.Error("The PDF file could not be prepared.", exception);
            return ServiceResult<string>.Fail("The PDF file could not be prepared: " + exception.Message);
        }
    }

    /// <summary>Shop name for the message text; never blank on a message the owner forwards.</summary>
    private string ShopName()
    {
        var shop = (_settings.GetString("store.name") ?? string.Empty).Trim();
        return shop.Length > 0 ? shop : "Area11";
    }

    private string ShopPhone()
    {
        var phone = (_settings.GetString("store.phone") ?? string.Empty).Trim();
        return phone.Length > 0 ? phone : "the shop";
    }

    private static string SanitizeFileName(string? fileName)
    {
        var raw = (fileName ?? string.Empty).Trim();
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var clean = new string(raw.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        if (!clean.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            clean += ".pdf";
        }

        return clean;
    }
}
