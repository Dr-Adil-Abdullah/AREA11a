namespace Area11.Desktop.ViewModels;

/// <summary>
/// One line of "what the scanner has typed so far" (F-46). It is kept even when the code is not on
/// the shop's list: the point of the test is that the scanner *typed*, not that the medicine exists.
/// </summary>
public sealed class HardwareScanRow
{
    public HardwareScanRow(string text, bool found)
    {
        Text = text;
        Found = found;
    }

    public string Text { get; }

    /// <summary>True when the code belongs to a medicine on this shop's list.</summary>
    public bool Found { get; }
}
