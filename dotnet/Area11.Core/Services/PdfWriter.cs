using System.Text;

namespace Area11.Core.Services;

/// <summary>
/// Portion 12: the shop's smallest honest PDF writer. No library, the way the native workbook
/// writes its own Open XML: A4 pages, one monospaced Courier face, the paper text line by line.
///
/// Two limits are deliberate and documented, not bugs. First, the only font is Courier (a PDF
/// base font, present in every reader), so text wraps like a wide receipt and carries no bold,
/// no boxes and no logo. Second, Courier has no Urdu glyphs: a character outside the Windows
/// code page becomes `?` on the PDF. The WhatsApp *text* posted alongside always carries the
/// full names correctly - the PDF is the file copy, the message is the readable one (D-00 keeps
/// the shop's own slips in English, but a customer's name may be typed in Urdu).
/// </summary>
public static class PdfWriter
{
    /// <summary>Characters per line at 9pt Courier inside A4 margins.</summary>
    public const int Columns = 95;

    /// <summary>Text lines per page at 12pt leading inside A4 margins.</summary>
    public const int LinesPerPage = 60;

    /// <summary>One A4 page of plain text, with the title on top - pure, no files touched.</summary>
    public static byte[] Write(string title, IEnumerable<string> lines)
    {
        var clean = (title ?? string.Empty).Trim();
        var wrapped = Wrap(lines ?? Enumerable.Empty<string>()).ToList();
        if (clean.Length == 0)
        {
            clean = "Area11";
        }

        wrapped.Insert(0, string.Empty);
        wrapped.Insert(0, clean);

        var pages = new List<List<string>>();
        for (var start = 0; start < wrapped.Count; start += LinesPerPage)
        {
            pages.Add(wrapped.Skip(start).Take(LinesPerPage).ToList());
        }

        if (pages.Count == 0)
        {
            pages.Add(new List<string> { clean });
        }

        // Object numbers: 1 = catalog, 2 = pages, then per page (page, content), then the font.
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [" + string.Join(" ", pages.Select((_, index) => (3 + index * 2) + " 0 R"))
                + "] /Count " + pages.Count + " >>"
        };

        var fontNumber = 3 + pages.Count * 2;
        for (var index = 0; index < pages.Count; index++)
        {
            var content = new StringBuilder();
            content.Append("BT /F1 9 Tf 12 TL 40 800 Td\n");
            foreach (var line in pages[index])
            {
                content.Append('(').Append(Escape(line)).Append(") Tj T*\n");
            }

            content.Append("ET");
            var stream = content.ToString();

            objects.Add("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] "
                + "/Resources << /Font << /F1 " + fontNumber + " 0 R >> >> "
                + "/Contents " + (3 + index * 2 + 1) + " 0 R >>");
            objects.Add("<< /Length " + Encoding.ASCII.GetByteCount(stream) + " >>\nstream\n" + stream + "\nendstream");
        }

        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>");

        var output = new StringBuilder();
        output.Append("%PDF-1.4\n");
        var offsets = new List<long>();
        for (var number = 0; number < objects.Count; number++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(output.ToString()));
            output.Append(number + 1).Append(" 0 obj\n").Append(objects[number]).Append("\nendobj\n");
        }

        var xrefAt = Encoding.ASCII.GetByteCount(output.ToString());
        output.Append("xref\n0 ").Append(objects.Count + 1).Append('\n');
        output.Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            output.Append(offset.ToString("D10")).Append(" 00000 n \n");
        }

        output.Append("trailer\n<< /Size ").Append(objects.Count + 1).Append(" /Root 1 0 R >>\n");
        output.Append("startxref\n").Append(xrefAt).Append("\n%%EOF");

        return Encoding.ASCII.GetBytes(output.ToString());
    }

    /// <summary>Hard-wraps every line at <see cref="Columns"/>; a blank line stays a blank line.</summary>
    public static List<string> Wrap(IEnumerable<string> lines)
    {
        var wrapped = new List<string>();
        foreach (var raw in lines ?? Enumerable.Empty<string>())
        {
            var line = (raw ?? string.Empty).Replace('\t', ' ');
            if (line.Length == 0)
            {
                wrapped.Add(string.Empty);
                continue;
            }

            for (var start = 0; start < line.Length; start += Columns)
            {
                wrapped.Add(line.Substring(start, Math.Min(Columns, line.Length - start)));
            }
        }

        return wrapped;
    }

    private static string Escape(string line)
    {
        var output = new StringBuilder(line.Length);
        foreach (var ch in line)
        {
            if (ch == '\\')
            {
                output.Append("\\\\");
            }
            else if (ch == '(')
            {
                output.Append("\\(");
            }
            else if (ch == ')')
            {
                output.Append("\\)");
            }
            else if (ch is '\r' or '\n')
            {
                output.Append(' ');
            }
            else if (ch is >= ' ' and <= '~')
            {
                output.Append(ch);
            }
            else if (ch is >= '\u00A0' and <= '\u00FF')
            {
                // Latin-1 byte as octal: the Rs sign some slips carry survives the trip.
                output.Append('\\').Append(Convert.ToString(ch, 8).PadLeft(3, '0'));
            }
            else
            {
                // No Urdu (or other) glyph in Courier: one honest placeholder, never a lost line.
                output.Append('?');
            }
        }

        return output.ToString();
    }
}
