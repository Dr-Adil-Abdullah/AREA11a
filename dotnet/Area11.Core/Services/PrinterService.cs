using System.Runtime.InteropServices;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>
/// Sends raw ESC/POS bytes straight to a Windows printer queue (owner rules Z-07, D-05, D-23).
///
/// Why the raw spooler API: a normal print job would open a dialog and print a page formatted by
/// Windows, which is wrong for a 58 mm thermal roll. The spooler accepts "RAW" data, so the bytes
/// built by <see cref="EscPosBuilder"/> reach the printer unchanged and the paper comes out at once.
///
/// A missing or switched-off printer must never lose a bill (Z-07): every failure is logged and
/// returned as a message, and the sale has already been saved before printing is attempted.
/// </summary>
public sealed class PrinterService
{
    private readonly SettingsService _settings;
    private readonly LogService _log;

    public PrinterService(SettingsService settings, LogService log)
    {
        _settings = settings;
        _log = log;
    }

    /// <summary>
    /// Builds the receipt bytes and prints them. An empty setting <c>printer.name</c> means
    /// "whatever printer Windows has set as default", which is how most shops run.
    /// </summary>
    public ServiceResult Print(ReceiptDocument document)
    {
        try
        {
            var data = EscPosBuilder.Build(document);
            return SendRaw(data, document.Title);
        }
        catch (Exception exception)
        {
            _log.Error("The receipt could not be printed.", exception);
            return ServiceResult.Fail("The receipt could not be printed: " + exception.Message);
        }
    }

    /// <summary>Sends already-built bytes. Used by the receipt, and later by the return slip.</summary>
    public ServiceResult SendRaw(byte[] data, string documentName = "Area11")
    {
        if (!OperatingSystem.IsWindows())
        {
            return ServiceResult.Fail("Printing is only available on Windows.");
        }

        if (data.Length == 0)
        {
            return ServiceResult.Fail("There is nothing to print.");
        }

        var printerName = _settings.GetString("printer.name");
        var target = string.IsNullOrWhiteSpace(printerName) ? null : printerName.Trim();

        var handle = IntPtr.Zero;

        try
        {
            if (!OpenPrinter(target, out handle, IntPtr.Zero))
            {
                var error = Marshal.GetLastWin32Error();
                _log.Error($"OpenPrinter failed for '{target ?? "(default)"}' with code {error}.");
                return ServiceResult.Fail(
                    "No printer answered. Check that the thermal printer is switched on and that " +
                    "the default printer is set in Windows.");
            }

            var info = new DocInfo
            {
                DocumentName = documentName,
                DataType = "RAW"
            };

            if (StartDocPrinter(handle, 1, info) == 0)
            {
                var error = Marshal.GetLastWin32Error();
                return ServiceResult.Fail($"The print job was refused by Windows (code {error}).");
            }

            try
            {
                if (!StartPagePrinter(handle))
                {
                    return ServiceResult.Fail("The printer did not accept the page.");
                }

                try
                {
                    if (!WritePrinter(handle, data, data.Length, out var written) || written != data.Length)
                    {
                        var error = Marshal.GetLastWin32Error();
                        return ServiceResult.Fail($"Only {written} of {data.Length} bytes reached the printer (code {error}).");
                    }
                }
                finally
                {
                    EndPagePrinter(handle);
                }
            }
            finally
            {
                EndDocPrinter(handle);
            }

            _log.Info($"Printed '{documentName}' ({data.Length} bytes) on {target ?? "the default printer"}.");
            return ServiceResult.Success();
        }
        catch (Exception exception)
        {
            _log.Error("Sending bytes to the printer failed.", exception);
            return ServiceResult.Fail("The printer could not be reached: " + exception.Message);
        }
        finally
        {
            if (handle != IntPtr.Zero)
            {
                ClosePrinter(handle);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class DocInfo
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? DocumentName;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? OutputFile;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? DataType;
    }

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool OpenPrinter(string? printerName, out IntPtr handle, IntPtr defaults);

    [DllImport("winspool.drv", EntryPoint = "ClosePrinter", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr handle);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int StartDocPrinter(IntPtr handle, int level, [In] DocInfo info);

    [DllImport("winspool.drv", EntryPoint = "EndDocPrinter", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr handle);

    [DllImport("winspool.drv", EntryPoint = "StartPagePrinter", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr handle);

    [DllImport("winspool.drv", EntryPoint = "EndPagePrinter", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr handle);

    [DllImport("winspool.drv", EntryPoint = "WritePrinter", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr handle, byte[] bytes, int count, out int written);
}
