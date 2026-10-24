using System.Text;

namespace Area11.Core.Models;

/// <summary>
/// The reading half of <see cref="CsvWriter"/>: RFC 4180, and forgiving where a real shop's file is
/// not perfect.
///
/// Why it is written by hand instead of a library: the shop's files come out of Excel on a Windows
/// PC, and the four things Excel does are exactly the four things to get right -
///
///   • **UTF-8 BOM** at the start (Excel writes one; a naive split leaves it glued to the first
///     heading, so "Name" stops being "Name" and every row is skipped as "no name");
///   • **CRLF** line endings, and a last line that may or may not end with one;
///   • **quotes** around a value that holds a comma, a quote or a line break - including a line
///     break INSIDE a quoted value, which is how a two-line address arrives;
///   • **trailing empty columns** that Excel adds when a row was once wider.
///
/// It never throws on bad input: a file written by hand half-way through is a row with an error,
/// not a crashed program.
/// </summary>
public static class CsvReader
{
    /// <summary>
    /// Splits a whole file into rows of fields. Blank lines are dropped: Excel leaves one at the end
    /// of every export, and an empty row is not a mistake the operator should be shown.
    /// </summary>
    public static List<List<string>> Parse(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();

        var quoted = false;
        var rowHasContent = false;

        // A BOM is a character, not a byte, once the text is a string: strip it or the first heading
        // silently becomes "\uFEFFName".
        var start = text.Length > 0 && text[0] == '\uFEFF' ? 1 : 0;

        for (var index = start; index < text.Length; index++)
        {
            var character = text[index];

            if (quoted)
            {
                if (character == '"')
                {
                    // Two quotes inside a quoted value are one real quote.
                    if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    field.Append(character);
                }

                rowHasContent = true;
                continue;
            }

            switch (character)
            {
                case '"' when field.Length == 0:
                    quoted = true;
                    rowHasContent = true;
                    break;

                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    rowHasContent = true;
                    break;

                case '\r':
                    // CRLF: the \n after it ends the row. A lone CR is treated the same way, because
                    // an old Mac export is still a file the owner may bring in.
                    if (index + 1 < text.Length && text[index + 1] == '\n')
                    {
                        index++;
                    }

                    EndRow(rows, row, field, rowHasContent || field.Length > 0);
                    rowHasContent = false;
                    break;

                case '\n':
                    EndRow(rows, row, field, rowHasContent || field.Length > 0);
                    rowHasContent = false;
                    break;

                default:
                    field.Append(character);
                    rowHasContent = true;
                    break;
            }
        }

        // The last line of a file usually has no line ending of its own.
        if (rowHasContent || field.Length > 0 || row.Count > 0)
        {
            EndRow(rows, row, field, rowHasContent || field.Length > 0);
        }

        return rows;
    }

    private static void EndRow(List<List<string>> rows, List<string> row, StringBuilder field, bool hasContent)
    {
        row.Add(field.ToString());
        field.Clear();

        if (hasContent)
        {
            rows.Add(new List<string>(row));
        }

        row.Clear();
    }

    /// <summary>
    /// Reads a column by its heading, not by its position: the owner may move a column in Excel, and
    /// a file that still has "Retail" in it must still import correctly.
    /// </summary>
    public static Dictionary<string, int> HeaderMap(IReadOnlyList<string> header)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < header.Count; index++)
        {
            var name = Normalise(header[index]);

            if (name.Length > 0 && !map.ContainsKey(name))
            {
                map[name] = index;
            }
        }

        return map;
    }

    /// <summary>
    /// "  Retail Price (Rs) " → "retailprice": case, spaces, punctuation and bracketed units are
    /// dropped, so a heading typed as "Retail (Rs)" and one typed "retail" are the same column.
    /// </summary>
    public static string Normalise(string? heading)
    {
        if (string.IsNullOrWhiteSpace(heading))
        {
            return string.Empty;
        }

        var text = heading.Trim();

        var bracket = text.IndexOf('(');
        if (bracket > 0)
        {
            text = text[..bracket];
        }

        var builder = new StringBuilder(text.Length);

        foreach (var character in text)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    /// <summary>The field at a column, or an empty string when the row is shorter than the header.</summary>
    public static string At(IReadOnlyList<string> row, int index) =>
        index >= 0 && index < row.Count ? row[index].Trim() : string.Empty;
}
