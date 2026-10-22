using System.Runtime.InteropServices;
using System.Text;
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

        // Why this is not simply "pass NULL and let Windows choose": OpenPrinter(NULL) opens a handle
        // to the local print SERVER, not to the default printer, and StartDocPrinter then refuses that
        // handle with code 6 (ERROR_INVALID_HANDLE). That was the shop's first printer defect: a fresh
        // install with no printer name could never print, and the message blamed the cable.
        var chosen = (_settings.GetString("printer.name") ?? string.Empty).Trim();
        var target = chosen.Length > 0 ? chosen : DefaultPrinterName();

        if (target.Length == 0)
        {
            _log.Error("No printer to print on: printer.name is empty and Windows has no default printer.");
            return ServiceResult.Fail(
                "This PC has no printer chosen and Windows has no default printer. Open SYSTEM -> " +
                "Printer & scanner, pick your receipt printer from the list, and press Print test slip " +
                "again. If the list is empty, the printer's driver is not installed in Windows yet.");
        }

        var handle = IntPtr.Zero;

        try
        {
            if (!OpenPrinter(target, out handle, IntPtr.Zero))
            {
                var error = Marshal.GetLastWin32Error();
                _log.Error($"OpenPrinter failed for '{target}' with code {error}.");
                return ServiceResult.Fail(Explain(error) + " The printer Area11 tried: " + target + ".");
            }

            var info = new DocInfo
            {
                DocumentName = documentName,
                DataType = "RAW"
            };

            if (StartDocPrinter(handle, 1, info) == 0)
            {
                var error = Marshal.GetLastWin32Error();
                _log.Error($"StartDocPrinter failed for '{target}' with code {error}.");
                return ServiceResult.Fail(Explain(error) + " The printer Area11 tried: " + target + ".");
            }

            try
            {
                if (!StartPagePrinter(handle))
                {
                    return ServiceResult.Fail(Explain(Marshal.GetLastWin32Error()));
                }

                try
                {
                    if (!WritePrinter(handle, data, data.Length, out var written) || written != data.Length)
                    {
                        var error = Marshal.GetLastWin32Error();
                        return ServiceResult.Fail(
                            $"Only {written} of {data.Length} bytes reached the printer. " + Explain(error));
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

            _log.Info($"Printed '{documentName}' ({data.Length} bytes) on {target}.");
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

    /// <summary>
    /// The printer Windows itself would use. Empty when the PC has none (which is the state that used
    /// to make every print fail with code 6 - see SendRaw).
    /// </summary>
    public static string DefaultPrinterName()
    {
        if (!OperatingSystem.IsWindows())
        {
            return string.Empty;
        }

        try
        {
            var size = 0;
            GetDefaultPrinter(null, ref size);

            if (size <= 1)
            {
                return string.Empty;
            }

            var buffer = new StringBuilder(size);
            return GetDefaultPrinter(buffer, ref size) ? buffer.ToString().Trim() : string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Every printer Windows knows on this PC, in the order it lists them. The Printer &amp; scanner page
    /// shows these names, because "type the printer's name" is a question the owner cannot answer from
    /// memory and a wrong name is error 1801.
    /// </summary>
    public static IReadOnlyList<string> InstalledPrinters()
    {
        if (!OperatingSystem.IsWindows())
        {
            return Array.Empty<string>();
        }

        try
        {
            var flags = PrinterEnumLocal | PrinterEnumConnections;
            uint needed = 0;
            uint returned = 0;

            EnumPrinters(flags, null, 4, IntPtr.Zero, 0, ref needed, ref returned);

            if (needed == 0)
            {
                return Array.Empty<string>();
            }

            var buffer = Marshal.AllocHGlobal((int)needed);

            try
            {
                if (!EnumPrinters(flags, null, 4, buffer, needed, ref needed, ref returned))
                {
                    return Array.Empty<string>();
                }

                var names = new List<string>();
                var size = Marshal.SizeOf<PrinterInfo4>();

                for (var index = 0; index < (int)returned; index++)
                {
                    var item = Marshal.PtrToStructure<PrinterInfo4>(buffer + (index * size));
                    var name = item.PrinterName?.Trim();

                    if (!string.IsNullOrEmpty(name) && !names.Contains(name, StringComparer.OrdinalIgnoreCase))
                    {
                        names.Add(name!);
                    }
                }

                return names;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (Exception)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// A Windows printer error code in a sentence the owner can act on (D-05). Code 6 is in this
    /// program's history: it is the "no printer chosen and no default" state, not a broken cable.
    /// </summary>
    public static string Explain(int code) => code switch
    {
        2 => "Windows cannot find that printer (code 2). Check that it is switched on and still installed.",
        5 => "Windows refused this account the right to print (code 5). An administrator has to allow it.",
        6 => "Windows was given a printer handle it does not recognise (code 6). That happens when no "
             + "printer is chosen and Windows has no default printer - pick the receipt printer on the "
             + "Printer & scanner page.",
        87 => "Windows did not accept the print job (code 87). Check the printer's driver.",
        1722 => "The Windows print service (spooler) is not running (code 1722). Start it, then print again.",
        1801 => "The printer's name is wrong (code 1801). Pick the exact name from the list on the "
                + "Printer & scanner page.",
        3012 => "Windows cannot find the printer's port (code 3012). Reinstall the printer in Windows.",
        _ => $"Windows refused the print job (code {code})."
    };

    private const uint PrinterEnumLocal = 0x00000002;
    private const uint PrinterEnumConnections = 0x00000004;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PrinterInfo4
    {
        public string? PrinterName;

        public string? ServerName;

        public uint Attributes;
    }

    [DllImport("winspool.drv", EntryPoint = "EnumPrintersW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool EnumPrinters(
        uint flags, string? name, uint level, IntPtr buffer, uint size, ref uint needed, ref uint returned);

    [DllImport("winspool.drv", EntryPoint = "GetDefaultPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetDefaultPrinter(StringBuilder? buffer, ref int size);

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
