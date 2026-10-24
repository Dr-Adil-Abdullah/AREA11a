namespace Area11.Desktop.ViewModels;

/// <summary>
/// One tick-box of the hardware test (F-46). The statement and the help come from
/// <see cref="Area11.Core.Models.HardwareCheck"/>; this row only carries the tick and tells the page
/// to recount itself, so the "3 of 6 passed" line follows the mouse without a Save.
/// </summary>
public sealed class HardwareCheckRow : ObservableObject
{
    private readonly Action _changed;
    private bool _passed;

    public HardwareCheckRow(string key, string text, string hint, bool passed, Action changed)
    {
        Key = key;
        Text = text;
        Hint = hint;
        _passed = passed;
        _changed = changed;
    }

    /// <summary>The stable name the saved answer is written with ("printed", "fits", ...).</summary>
    public string Key { get; }

    public string Text { get; }

    /// <summary>What to do when the tick is missing - said once, in small print.</summary>
    public string Hint { get; }

    public bool Passed
    {
        get => _passed;
        set
        {
            if (!SetProperty(ref _passed, value))
            {
                return;
            }

            _changed();
        }
    }
}
