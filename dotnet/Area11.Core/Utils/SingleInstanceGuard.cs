namespace Area11.Core.Utils;

/// <summary>
/// One shop, one copy (owner's word, Nov 2026): the second Area11 must say "already running" and
/// close instead of opening beside the first. A named system mutex is the lock - the first copy
/// holds it from startup to exit, and a second copy finds it taken.
///
/// Why a named mutex and not a flag in the database: the database cannot see a copy that is still
/// starting, and a crashed copy would leave a flag behind for ever. The mutex dies with the
/// process instead - including a Task Manager kill, which the next start takes over as an
/// abandoned lock rather than a refusal (a dead copy must never bar the shop door).
///
/// The name is session-local (`Local\`), so two Windows accounts signed in at once are not each
/// other's "second copy". It carries no version: two different builds open at once are the same
/// danger as two of the same build.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    /// <summary>The one name every Area11 copy asks for. Tests use their own throwaway names.</summary>
    public const string ApplicationName = @"Local\Area11_Shop";

    private Mutex? _mutex;
    private bool _disposed;

    private SingleInstanceGuard(Mutex? mutex, bool isFirst)
    {
        _mutex = mutex;
        IsFirst = isFirst;
    }

    /// <summary>True when this copy holds the lock and may open; false means close with a sentence.</summary>
    public bool IsFirst { get; }

    public static SingleInstanceGuard TryAcquire(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("The lock needs a name.", nameof(name));
        }

        var mutex = new Mutex(false, name);

        try
        {
            bool first;

            try
            {
                first = mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                // The previous holder died without letting go (Task Manager, power cut, crash).
                // The wait still hands us the ownership - that IS the recovery.
                first = true;
            }

            if (!first)
            {
                mutex.Dispose();
                mutex = null;
            }

            return new SingleInstanceGuard(mutex, first);
        }
        catch
        {
            mutex.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Dispose must never throw: the application calls it on its way out, when an exception
        // would only hide the reason it was already leaving.
        if (_mutex is { } mutex)
        {
            _mutex = null;

            try
            {
                if (IsFirst)
                {
                    mutex.ReleaseMutex();
                }
            }
            catch
            {
                // The lock is going away with the process either way.
            }

            mutex.Dispose();
        }
    }
}
