using System.Text;
using Area11.Core.Models;

namespace Area11.Core.Services;

/// <summary>
/// Converts a finished <see cref="ReceiptDocument"/> into ESC/POS bytes (Spec 14.3).
///
/// Why bytes and not a print driver dialog: the owner's rule D-05/D-23 is that pressing print must
/// print at once, with no Windows dialog. ESC/POS is the language every 58 mm and 80 mm thermal
/// printer understands; sending the bytes straight to the spooler prints exactly the same on both.
///
/// The command set used here is deliberately the smallest common one - initialise, align, bold,
/// double height, line feed, cut - because cheap printers differ in the exotic commands but agree
/// on these. The encoding is Latin-1: Urdu receipts will be printed as images later (Spec 4.6).
/// </summary>
public static class EscPosBuilder
{
    public const byte Esc = 0x1B;
    public const byte Gs = 0x1D;

    /// <summary>ESC @ - reset the printer to a known state.</summary>
    public static readonly byte[] Initialise = { Esc, (byte)'@' };

    /// <summary>GS V 66 0 - feed a little, then cut (used when printer.autoCut is on).</summary>
    public static readonly byte[] Cut = { Gs, (byte)'V', 66, 0 };

    public static byte[] Build(ReceiptDocument document, Encoding? encoding = null)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        var text = encoding ?? Encoding.Latin1;
        var copies = Math.Clamp(document.Copies <= 0 ? 1 : document.Copies, 1, 3);
        var buffer = new List<byte>();

        for (var copy = 0; copy < copies; copy++)
        {
            buffer.AddRange(Initialise);

            foreach (var line in document.Lines)
            {
                buffer.AddRange(AlignCommand(line.Align));
                buffer.AddRange(BoldCommand(line.Bold));
                buffer.AddRange(SizeCommand(line.DoubleHeight));

                buffer.AddRange(text.GetBytes(line.Text));
                buffer.Add(0x0A);

                // Turn every style off again so one bold line cannot colour the rest of the paper.
                buffer.AddRange(BoldCommand(false));
                buffer.AddRange(SizeCommand(false));
            }

            buffer.Add(0x0A);
            buffer.Add(0x0A);

            if (document.AutoCut)
            {
                buffer.AddRange(Cut);
            }
        }

        return buffer.ToArray();
    }

    /// <summary>ESC a n - 0 left, 1 centre, 2 right.</summary>
    public static byte[] AlignCommand(ReceiptAlign align)
    {
        var mode = align switch
        {
            ReceiptAlign.Center => (byte)1,
            ReceiptAlign.Right => (byte)2,
            _ => (byte)0
        };

        return new[] { Esc, (byte)'a', mode };
    }

    /// <summary>ESC E n - emphasised on / off.</summary>
    public static byte[] BoldCommand(bool on) => new[] { Esc, (byte)'E', on ? (byte)1 : (byte)0 };

    /// <summary>ESC ! n - 0x10 = double height (used for the shop name only).</summary>
    public static byte[] SizeCommand(bool doubleHeight) => new[] { Esc, (byte)'!', doubleHeight ? (byte)0x10 : (byte)0x00 };
}
