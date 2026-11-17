using System.Diagnostics;
using Area11.Core.Services;

namespace Area11.Desktop;

/// <summary>
/// Portion 12: the one shared WhatsApp send-flow. Every page prepares the same PDF file (when the
/// owner keeps `whatsapp.preparePdf` on), opens the same native-app link with the same text, and
/// tells the owner exactly which file to attach by hand. Opening is never evidence of delivery -
/// the owner presses Send herself - and a missing app is a named error, never a silent nothing.
/// </summary>
internal static class WhatsAppFlow
{
    /// <summary>
    /// Prepares the PDF and opens WhatsApp with the text. Returns the status line the page shows;
    /// `Ok` false means nothing was opened (the PDF may still be ready - the message says so).
    /// </summary>
    public static (bool Ok, string Message) Send(
        AppServices services,
        string? phone,
        string text,
        string pdfTitle,
        IEnumerable<string> pdfLines,
        string pdfFileName,
        bool withPdf = true)
    {
        string? pdfPath = null;
        if (withPdf && services.WhatsApp.PreparePdfEnabled)
        {
            var pdf = services.WhatsApp.PreparePdf(pdfTitle, pdfLines, pdfFileName);
            if (!pdf.Ok || string.IsNullOrWhiteSpace(pdf.Value))
            {
                return (false, pdf.Error ?? "The PDF file could not be prepared.");
            }

            pdfPath = pdf.Value;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = WhatsAppService.BuildAppLink(phone, text),
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            // The usual cause is WhatsApp Desktop not being installed: the message names the cause
            // and the ready file, so the owner can attach it from the browser instead.
            return (false, "WhatsApp could not be opened (" + exception.Message + "). "
                + (pdfPath is null
                    ? "Install WhatsApp Desktop, or copy the text above into WhatsApp Web."
                    : "The PDF is ready at " + pdfPath + " - attach it from WhatsApp Web by hand."));
        }

        return (true, "WhatsApp opened with the text. "
            + (pdfPath is null
                ? "Press Send there."
                : "Attach the prepared file by hand: " + pdfPath));
    }
}
