namespace Area11.Core.Utils;

/// <summary>Local synchronous form guard, NOT durable identity/commit evidence/authorization.
/// Only a service's explicit no-commit failure permits retry. Acknowledged work remains blocked
/// until the UI has consumed its original inputs; a later callback exception must still say SAVED.</summary>
public sealed class PostingAttemptGuard
{
    public PostingAttemptState State { get; private set; }
    public bool WasAcknowledged { get; private set; }
    public bool InFlight => State == PostingAttemptState.InFlight;
    public bool Ready => State == PostingAttemptState.Ready;

    public bool TryBegin()
    {
        if (!Ready) return false;
        WasAcknowledged = false;
        State = PostingAttemptState.InFlight;
        return true;
    }

    public void CancelBeforeWrite()
    {
        Require(PostingAttemptState.InFlight);
        State = PostingAttemptState.Ready;
    }

    public void ObserveFailure(ServiceResult failure)
    {
        Require(PostingAttemptState.InFlight);
        if (failure.Ok) throw new InvalidOperationException("A success is not a failed posting.");
        State = failure.NoCommitConfirmed ? PostingAttemptState.Ready : PostingAttemptState.NeedsVerification;
    }

    public void AcknowledgeCommit()
    {
        Require(PostingAttemptState.InFlight);
        WasAcknowledged = true;
        State = PostingAttemptState.CommittedNeedsReset;
    }

    public void CompleteReset()
    {
        Require(PostingAttemptState.CommittedNeedsReset);
        State = PostingAttemptState.Ready;
    }

    public void ObserveException()
    {
        if (InFlight) State = PostingAttemptState.NeedsVerification;
    }

    private void Require(PostingAttemptState state)
    {
        if (State != state) throw new InvalidOperationException("Invalid posting guard transition.");
    }
}

public enum PostingAttemptState { Ready, InFlight, NeedsVerification, CommittedNeedsReset }
