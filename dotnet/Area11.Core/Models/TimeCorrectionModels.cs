namespace Area11.Core.Models;

/// <summary>
/// One bill that was stamped with the wrong clock (v0.68.0, owner's word Nov 2026).
///
/// Why a row of its own: the fix lists the wrongly-stamped bills TOGETHER and the owner accepts
/// them in ONE action - so every row carries both the wrong stamp and the time the fix would give
/// it, and the owner sees the whole truth before he presses anything.
/// </summary>
public sealed class TimeCorrectionBill
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    /// <summary>The stamp the bill carries today - the wrong one ("2026-11-09 14:05:00").</summary>
    public string WrongDate { get; set; } = string.Empty;

    /// <summary>What the fix would write into `sales.date` - the reporting time.</summary>
    public string ProposedDate { get; set; } = string.Empty;

    /// <summary>"+2.0 days" - how far this bill would move, in the owner's words.</summary>
    public string ShiftLabel { get; set; } = string.Empty;

    public long TotalPaisa { get; set; }

    public string TotalLabel { get; set; } = string.Empty;

    /// <summary>
    /// False for bills written before v0.68.0: nobody recorded which clock stamped them, so the fix
    /// assumes the computer clock and says so on the screen.
    /// </summary>
    public bool KnowsOffset { get; set; }
}

/// <summary>What one press of the fix button did: how many bills moved, how many were already right.</summary>
public sealed class TimeCorrectionResult
{
    public int Fixed { get; set; }

    public int Skipped { get; set; }
}
