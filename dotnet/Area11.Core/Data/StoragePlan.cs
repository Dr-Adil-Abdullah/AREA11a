using Area11.Core.Utils;

namespace Area11.Core.Data;

/// <summary>
/// How SQLite is told to behave for **this** database file (F-45).
///
/// The one hard rule of the LAN feature: **WAL does not work across a network share.** WAL keeps a
/// `-shm` file that two Windows PCs cannot share reliably, and a dropped link leaves the file in a
/// state SQLite cannot repair by itself. So:
///   * **local disk** - WAL, 5 s busy timeout (the shop's own default since v0.1.0);
///   * **network share** - the old rollback journal (`DELETE`), `synchronous=FULL` (the file must
///     survive a cable being pulled) and a 15 s timeout, because a second counter sometimes waits
///     behind the first one's bill.
///
/// The plan is decided from the path alone, which makes it testable without a network.
/// </summary>
public sealed class StoragePlan
{
    private StoragePlan(bool isNetwork, bool shared)
    {
        IsNetwork = isNetwork;
        IsShared = shared;
    }

    public bool IsNetwork { get; }

    /// <summary>
    /// True when **another PC is meant to open this file**: a network path, or a main counter that
    /// has shared its folder (D-62). The main counter must keep the rollback journal **after** a
    /// restart too - otherwise the next start would quietly turn WAL back on and break the second
    /// counter's connection.
    /// </summary>
    public bool IsShared { get; }

    /// <summary>True on a local disk that no other PC opens. False on a share - see the class comment.</summary>
    public bool UseWal => !IsShared;

    /// <summary>How long a statement waits for another counter's lock before it gives up.</summary>
    public int BusyTimeoutMs => UseWal ? 5000 : 15000;

    /// <summary>Written to the disk on every commit when another counter may read the file.</summary>
    public bool SynchronousFull => !UseWal;

    /// <param name="path">The file the program is about to open.</param>
    /// <param name="mode">
    /// The shop's mode. **Every** LAN mode is treated as shared, even when the path looks local:
    /// the main counter's own disk is opened by the second PC, and a second counter can be pointed
    /// at a mapped drive by hand. WAL is never asked for in either case.
    /// </param>
    public static StoragePlan For(string? path, ShopMode mode = ShopMode.Single) =>
        new(ShopConfig.IsUnc(path), ShopConfig.IsUnc(path) || mode != ShopMode.Single);
}
