namespace Area11.Core.Models;

/// <summary>How a receipt line sits inside the paper width.</summary>
public enum ReceiptAlign
{
    Left,
    Center,
    Right
}

/// <summary>
/// One printed line. Alignment, bold and double height are kept as data - not as ESC/POS bytes -
/// so the layout can be tested without a printer (which is exactly what the tests do).
/// </summary>
public sealed class ReceiptLine
{
    public ReceiptLine(string text, ReceiptAlign align = ReceiptAlign.Left, bool bold = false, bool doubleHeight = false)
    {
        Text = text ?? string.Empty;
        Align = align;
        Bold = bold;
        DoubleHeight = doubleHeight;
    }

    public string Text { get; }

    public ReceiptAlign Align { get; }

    public bool Bold { get; }

    public bool DoubleHeight { get; }

    public static ReceiptLine Sep(int width) => new(new string('-', width));
}

/// <summary>
/// A finished receipt: the exact characters of every line plus how the printer should behave.
/// Width is 32 characters for a 58 mm roll and 48 for an 80 mm roll (Spec 14.2).
/// </summary>
public sealed class ReceiptDocument
{
    public string Title { get; init; } = "Receipt";

    public int Width { get; init; } = 32;

    public List<ReceiptLine> Lines { get; init; } = new();

    public bool AutoCut { get; set; } = true;

    public int Copies { get; set; } = 1;

    /// <summary>The text form - what a text-only printer or the on-screen preview shows.</summary>
    public string PlainText => string.Join(Environment.NewLine, Lines.Select(line => line.Text));
}
