namespace Area11.Core.Data;

/// <summary>Per-call observation, not a durable commit receipt. Do not share across calls/threads.</summary>
public sealed class TransactionOutcome
{
    public bool WorkStarted { get; internal set; }
    public bool CommitAttempted { get; internal set; }
    public bool RollbackConfirmed { get; internal set; }
    public bool NoCommitConfirmed => !WorkStarted || (!CommitAttempted && RollbackConfirmed);
}
