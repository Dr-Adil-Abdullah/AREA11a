namespace Area11.Core.Services;

/// <summary>
/// Portion 16: the Windows start-up entry, behind a seam. The real one writes the machine-wide Run
/// list (every Windows account on this PC, by the owner's word); the tests use a memory fake, so
/// the suite never touches the registry of the computer it runs on.
/// </summary>
public interface IAutoStartStore
{
    /// <summary>Start <paramref name="exePath"/> at Windows sign-in (every account or this one, per store).</summary>
    void Enable(string exePath);

    /// <summary>Remove the entry. Missing already means done, not an error.</summary>
    void Disable();

    /// <summary>True when an entry exists. Never throws: a question must not become a failure.</summary>
    bool IsEnabled();
}
