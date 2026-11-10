using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Area11.Core.Models;

/// <summary>A workbook is only named sheets and rows of text; business rules stay in the services.</summary>
public sealed class XlsxWorkbook
{
    public List<XlsxSheet> Sheets { get; init; } = new();

    public XlsxSheet? Sheet(string name) =>
        Sheets.FirstOrDefault(sheet => string.Equals(sheet.Name, name, StringComparison.OrdinalIgnoreCase));
}

public sealed class XlsxSheet
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Row zero is Excel row 1. Empty rows inside the sheet keep their original positions.</summary>
    public List<List<string>> Rows { get; init; } = new();
}

/// <summary>
/// A deliberately small, dependency-free Office Open XML reader/writer (T-10, Spec 24).
///
/// This is not a spreadsheet calculation engine. It reads the ordinary worksheets Excel writes -
/// shared strings, inline strings, numbers, booleans and real date cells - and writes simple inline-
/// string worksheets. Every exported value is text, so a barcode keeps its leading zeroes and a name
/// beginning with '=' is never turned into a formula. XML entities, DTDs, external relationships,
/// path tricks and oversized ZIPs are refused. No Excel installation or NuGet package is required.
/// </summary>
public static class XlsxFile
{
    private const int MaxFileBytes = 20 * 1024 * 1024;
    private const int MaxEntryBytes = 16 * 1024 * 1024;
    private const int MaxTotalBytes = 64 * 1024 * 1024;
    private const int MaxRows = 100_000;
    private const int MaxColumns = 256;
    private const int MaxCellsPerSheet = 1_000_000;

    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace DocumentRelations = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelations = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace ContentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";

    public static byte[] Write(XlsxWorkbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ValidateSheets(workbook.Sheets);

        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteDocument(zip, "[Content_Types].xml", ContentTypesDocument(workbook.Sheets.Count));
            WriteDocument(zip, "_rels/.rels", RootRelationshipsDocument());
            WriteDocument(zip, "xl/workbook.xml", WorkbookDocument(workbook.Sheets));
            WriteDocument(zip, "xl/_rels/workbook.xml.rels", WorkbookRelationshipsDocument(workbook.Sheets.Count));
            WriteDocument(zip, "xl/styles.xml", XDocument.Parse(StylesXml));

            for (var index = 0; index < workbook.Sheets.Count; index++)
            {
                WriteWorksheet(zip, $"xl/worksheets/sheet{index + 1}.xml", workbook.Sheets[index]);
            }
        }

        return output.ToArray();
    }

    public static XlsxWorkbook Read(byte[] bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            throw new InvalidDataException("The Excel workbook is empty.");
        }

        if (bytes.Length > MaxFileBytes)
        {
            throw new InvalidDataException("The Excel workbook is larger than the 20 MB import limit.");
        }

        try
        {
            using var input = new MemoryStream(bytes, writable: false);
            using var zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
            ValidateZip(zip);

            var workbookDocument = ReadDocument(zip, "xl/workbook.xml");
            var relationshipsDocument = ReadDocument(zip, "xl/_rels/workbook.xml.rels");
            var relationships = WorksheetRelationships(relationshipsDocument);
            var sharedStrings = ReadSharedStrings(zip);
            var dateStyles = ReadDateStyles(zip);
            var workbook = new XlsxWorkbook();

            foreach (var element in workbookDocument.Descendants(Main + "sheet"))
            {
                var name = (string?)element.Attribute("name") ?? string.Empty;
                var relationId = (string?)element.Attribute(DocumentRelations + "id") ?? string.Empty;

                if (string.IsNullOrWhiteSpace(name) || !relationships.TryGetValue(relationId, out var target))
                {
                    throw new InvalidDataException("A worksheet relationship is missing from the workbook.");
                }

                var sheet = new XlsxSheet { Name = name };
                ReadWorksheet(zip, target, sharedStrings, dateStyles, sheet.Rows);
                workbook.Sheets.Add(sheet);
            }

            if (workbook.Sheets.Count == 0)
            {
                throw new InvalidDataException("The workbook has no worksheets.");
            }

            ValidateSheets(workbook.Sheets);
            return workbook;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is XmlException or IOException or ArgumentException)
        {
            throw new InvalidDataException("This is not a readable Excel .xlsx workbook. " + exception.Message, exception);
        }
    }

    /// <summary>Feeds an existing CSV-based new-record preview without changing its business rules.</summary>
    public static string SheetAsCsv(XlsxSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var rows = sheet.Rows.SkipWhile(row => row.All(string.IsNullOrWhiteSpace)).ToList();
        return rows.Count == 0
            ? string.Empty
            : CsvWriter.Document(rows[0], rows.Skip(1));
    }

    private static void ValidateSheets(IReadOnlyList<XlsxSheet> sheets)
    {
        if (sheets.Count is 0 or > 64)
        {
            throw new InvalidDataException("A workbook must have between 1 and 64 worksheets.");
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sheet in sheets)
        {
            var name = sheet.Name ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name) || name.Length > 31
                || name.IndexOfAny(new[] { '[', ']', ':', '*', '?', '/', '\\' }) >= 0
                || !names.Add(name))
            {
                throw new InvalidDataException("Worksheet names must be unique, at most 31 characters, and not contain [ ] : * ? / \\.");
            }

            if (sheet.Rows.Count > MaxRows)
            {
                throw new InvalidDataException($"Worksheet '{name}' has more than {MaxRows} rows.");
            }
        }
    }

    private static void ValidateZip(ZipArchive zip)
    {
        if (zip.Entries.Count > 256)
        {
            throw new InvalidDataException("The workbook has too many ZIP entries.");
        }

        long total = 0;
        foreach (var entry in zip.Entries)
        {
            if (entry.Length > MaxEntryBytes)
            {
                throw new InvalidDataException("A workbook part is larger than the 16 MB safety limit.");
            }

            total += entry.Length;
            if (total > MaxTotalBytes)
            {
                throw new InvalidDataException("The expanded workbook is larger than the 64 MB safety limit.");
            }
        }
    }

    private static XDocument ContentTypesDocument(int sheetCount)
    {
        var root = new XElement(ContentTypes + "Types",
            new XElement(ContentTypes + "Default",
                new XAttribute("Extension", "rels"),
                new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
            new XElement(ContentTypes + "Default",
                new XAttribute("Extension", "xml"),
                new XAttribute("ContentType", "application/xml")),
            new XElement(ContentTypes + "Override",
                new XAttribute("PartName", "/xl/workbook.xml"),
                new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
            new XElement(ContentTypes + "Override",
                new XAttribute("PartName", "/xl/styles.xml"),
                new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml")));

        for (var index = 1; index <= sheetCount; index++)
        {
            root.Add(new XElement(ContentTypes + "Override",
                new XAttribute("PartName", $"/xl/worksheets/sheet{index}.xml"),
                new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")));
        }

        return Document(root);
    }

    private static XDocument RootRelationshipsDocument() =>
        Document(new XElement(PackageRelations + "Relationships",
            new XElement(PackageRelations + "Relationship",
                new XAttribute("Id", "rId1"),
                new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                new XAttribute("Target", "xl/workbook.xml"))));

    private static XDocument WorkbookDocument(IEnumerable<XlsxSheet> sheets) =>
        Document(new XElement(Main + "workbook",
            new XAttribute(XNamespace.Xmlns + "r", DocumentRelations.NamespaceName),
            new XElement(Main + "sheets",
                sheets.Select((sheet, index) => new XElement(Main + "sheet",
                    new XAttribute("name", sheet.Name),
                    new XAttribute("sheetId", index + 1),
                    new XAttribute(DocumentRelations + "id", $"rId{index + 1}"))))));

    private static XDocument WorkbookRelationshipsDocument(int sheetCount)
    {
        var root = new XElement(PackageRelations + "Relationships");
        for (var index = 1; index <= sheetCount; index++)
        {
            root.Add(new XElement(PackageRelations + "Relationship",
                new XAttribute("Id", $"rId{index}"),
                new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                new XAttribute("Target", $"worksheets/sheet{index}.xml")));
        }

        root.Add(new XElement(PackageRelations + "Relationship",
            new XAttribute("Id", $"rId{sheetCount + 1}"),
            new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"),
            new XAttribute("Target", "styles.xml")));
        return Document(root);
    }

    private static XDocument Document(XElement root) =>
        new(new XDeclaration("1.0", "UTF-8", "yes"), root);

    private static void WriteDocument(ZipArchive zip, string path, XDocument document)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, WriterSettings());
        document.WriteTo(writer);
    }

    private static void WriteWorksheet(ZipArchive zip, string path, XlsxSheet sheet)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, WriterSettings());
        writer.WriteStartDocument();
        writer.WriteStartElement("worksheet", Main.NamespaceName);
        writer.WriteStartElement("sheetData");

        var cells = 0;
        for (var rowIndex = 0; rowIndex < sheet.Rows.Count; rowIndex++)
        {
            var row = sheet.Rows[rowIndex];
            if (row.Count > MaxColumns)
            {
                throw new InvalidDataException($"Worksheet '{sheet.Name}' has more than {MaxColumns} columns.");
            }

            writer.WriteStartElement("row");
            writer.WriteAttributeString("r", (rowIndex + 1).ToString(CultureInfo.InvariantCulture));
            for (var columnIndex = 0; columnIndex < row.Count; columnIndex++)
            {
                var value = row[columnIndex] ?? string.Empty;
                if (value.Length == 0)
                {
                    continue;
                }

                if (value.Length > 32_767 || ++cells > MaxCellsPerSheet)
                {
                    throw new InvalidDataException($"Worksheet '{sheet.Name}' exceeds the cell safety limit.");
                }

                writer.WriteStartElement("c");
                writer.WriteAttributeString("r", ColumnName(columnIndex + 1) + (rowIndex + 1).ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("t", "inlineStr");
                if (rowIndex == 0)
                {
                    writer.WriteAttributeString("s", "1");
                }

                writer.WriteStartElement("is");
                writer.WriteStartElement("t");
                if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
                {
                    writer.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
                }

                writer.WriteString(value);
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static XmlWriterSettings WriterSettings() => new()
    {
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        Indent = false,
        CloseOutput = false
    };

    private static Dictionary<string, string> WorksheetRelationships(XDocument document)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var relationship in document.Descendants(PackageRelations + "Relationship"))
        {
            var type = (string?)relationship.Attribute("Type") ?? string.Empty;
            if (!type.EndsWith("/worksheet", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals((string?)relationship.Attribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("External worksheet relationships are not allowed.");
            }

            var id = (string?)relationship.Attribute("Id") ?? string.Empty;
            var target = (string?)relationship.Attribute("Target") ?? string.Empty;
            result[id] = NormaliseTarget(target);
        }

        return result;
    }

    private static string NormaliseTarget(string target)
    {
        if (string.IsNullOrWhiteSpace(target) || target.Contains('\\') || target.Contains('%'))
        {
            throw new InvalidDataException("A worksheet has an invalid internal target.");
        }

        var combined = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
        var parts = combined.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(part => part is "." or ".."))
        {
            throw new InvalidDataException("A worksheet target tries to leave the workbook folder.");
        }

        return string.Join("/", parts);
    }

    private static List<string> ReadSharedStrings(ZipArchive zip)
    {
        var entry = FindEntry(zip, "xl/sharedStrings.xml");
        if (entry is null)
        {
            return new List<string>();
        }

        var document = LoadDocument(entry);
        return document.Descendants(Main + "si")
            .Select(item => string.Concat(item.Descendants(Main + "t").Select(text => text.Value)))
            .ToList();
    }

    private static HashSet<int> ReadDateStyles(ZipArchive zip)
    {
        var result = new HashSet<int>();
        var entry = FindEntry(zip, "xl/styles.xml");
        if (entry is null)
        {
            return result;
        }

        var document = LoadDocument(entry);
        var customFormats = document.Descendants(Main + "numFmt")
            .Select(format => new
            {
                Id = (int?)format.Attribute("numFmtId") ?? 0,
                Code = (string?)format.Attribute("formatCode") ?? string.Empty
            })
            .ToDictionary(format => format.Id, format => format.Code);

        var cellFormats = document.Element(Main + "styleSheet")?.Element(Main + "cellXfs");
        if (cellFormats is null)
        {
            return result;
        }

        var style = 0;
        foreach (var format in cellFormats.Elements(Main + "xf"))
        {
            var id = (int?)format.Attribute("numFmtId") ?? 0;
            if (id is >= 14 and <= 22
                || customFormats.TryGetValue(id, out var code) && LooksLikeDateFormat(code))
            {
                result.Add(style);
            }

            style++;
        }

        return result;
    }

    private static bool LooksLikeDateFormat(string code)
    {
        // Quoted literals and bracketed colours/conditions are not date letters.
        var cleaned = System.Text.RegularExpressions.Regex.Replace(code, "\"[^\"]*\"|\\[[^]]*\\]", string.Empty);
        return cleaned.IndexOfAny(new[] { 'y', 'Y', 'd', 'D' }) >= 0;
    }

    private static void ReadWorksheet(
        ZipArchive zip,
        string path,
        IReadOnlyList<string> sharedStrings,
        IReadOnlySet<int> dateStyles,
        List<List<string>> rows)
    {
        var document = ReadDocument(zip, path);
        var data = document.Descendants(Main + "sheetData").FirstOrDefault();
        if (data is null)
        {
            return;
        }

        var expectedRow = 1;
        var cellCount = 0;
        foreach (var sourceRow in data.Elements(Main + "row"))
        {
            var rowNumber = (int?)sourceRow.Attribute("r") ?? expectedRow;
            if (rowNumber < expectedRow || rowNumber > MaxRows)
            {
                throw new InvalidDataException("Worksheet rows are out of order or exceed the row limit.");
            }

            while (rows.Count < rowNumber - 1)
            {
                rows.Add(new List<string>());
            }

            var row = new List<string>();
            var nextColumn = 1;
            foreach (var cell in sourceRow.Elements(Main + "c"))
            {
                var reference = (string?)cell.Attribute("r");
                var column = string.IsNullOrWhiteSpace(reference) ? nextColumn : ColumnIndex(reference);
                if (column < nextColumn || column > MaxColumns || ++cellCount > MaxCellsPerSheet)
                {
                    throw new InvalidDataException("Worksheet cells are out of order or exceed the cell limit.");
                }

                while (row.Count < column)
                {
                    row.Add(string.Empty);
                }

                row[column - 1] = CellValue(cell, sharedStrings, dateStyles);
                nextColumn = column + 1;
            }

            while (row.Count > 0 && row[^1].Length == 0)
            {
                row.RemoveAt(row.Count - 1);
            }

            rows.Add(row);
            expectedRow = rowNumber + 1;
        }
    }

    private static string CellValue(
        XElement cell,
        IReadOnlyList<string> sharedStrings,
        IReadOnlySet<int> dateStyles)
    {
        var type = (string?)cell.Attribute("t") ?? string.Empty;
        if (type == "inlineStr")
        {
            return string.Concat(cell.Descendants(Main + "t").Select(text => text.Value));
        }

        var value = cell.Element(Main + "v")?.Value ?? string.Empty;
        if (type == "s")
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
                || index < 0 || index >= sharedStrings.Count)
            {
                throw new InvalidDataException("A shared-string index is invalid.");
            }

            return sharedStrings[index];
        }

        if (type == "b")
        {
            return value == "1" ? "true" : "false";
        }

        if (type == "e")
        {
            throw new InvalidDataException("The workbook contains an Excel error cell. Replace it with a value first.");
        }

        if (type == "d" && DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var isoDate))
        {
            return isoDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        var style = (int?)cell.Attribute("s") ?? 0;
        if ((type.Length == 0 || type == "n") && dateStyles.Contains(style)
            && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial)
            && double.IsFinite(serial))
        {
            try
            {
                return DateTime.FromOADate(serial).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            catch (ArgumentException)
            {
                // Leave an out-of-range number as a number; the business preview will name the bad date.
            }
        }

        return value;
    }

    private static XDocument ReadDocument(ZipArchive zip, string path)
    {
        var entry = FindEntry(zip, path)
            ?? throw new InvalidDataException("The workbook is missing its " + path + " part.");
        return LoadDocument(entry);
    }

    private static ZipArchiveEntry? FindEntry(ZipArchive zip, string path) =>
        zip.Entries.FirstOrDefault(entry => string.Equals(entry.FullName, path, StringComparison.Ordinal))
        ?? zip.Entries.FirstOrDefault(entry => string.Equals(entry.FullName, path, StringComparison.OrdinalIgnoreCase));

    private static XDocument LoadDocument(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaxEntryBytes,
            MaxCharactersFromEntities = 0,
            CloseInput = false
        });
        return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
    }

    private static int ColumnIndex(string reference)
    {
        var column = 0;
        foreach (var character in reference)
        {
            if (!char.IsAsciiLetter(character))
            {
                break;
            }

            column = checked(column * 26 + char.ToUpperInvariant(character) - 'A' + 1);
        }

        return column;
    }

    private static string ColumnName(int column)
    {
        var name = string.Empty;
        while (column > 0)
        {
            column--;
            name = (char)('A' + column % 26) + name;
            column /= 26;
        }

        return name;
    }

    private const string StylesXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <fonts count="2">
            <font><sz val="11"/><name val="Calibri"/></font>
            <font><b/><sz val="11"/><name val="Calibri"/></font>
          </fonts>
          <fills count="2">
            <fill><patternFill patternType="none"/></fill>
            <fill><patternFill patternType="gray125"/></fill>
          </fills>
          <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
          <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
          <cellXfs count="2">
            <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
            <xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/>
          </cellXfs>
          <cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
        </styleSheet>
        """;
}
