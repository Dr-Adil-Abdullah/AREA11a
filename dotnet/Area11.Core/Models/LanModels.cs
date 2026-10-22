using Area11.Core.Utils;

namespace Area11.Core.Models;

/// <summary>
/// What the second-counter page shows (F-45, Spec §34.9): which mode this PC is in, where the shop's
/// file is, whether it can be read **and written** right now, and which PCs have connected lately.
/// </summary>
public sealed class LanStatus
{
    public ShopMode Mode { get; set; } = ShopMode.Single;

    public string ModeLabel { get; set; } = "One computer";

    /// <summary>Where `area11.db` sits for this running program.</summary>
    public string DatabaseFolder { get; set; } = string.Empty;

    /// <summary>The little file that is read at start-up - shown so the owner can find it.</summary>
    public string IniFile { get; set; } = string.Empty;

    public bool IsNetwork { get; set; }

    /// <summary>True when the file can be read and a write lock can be taken.</summary>
    public bool Reachable { get; set; }

    public string CheckMessage { get; set; } = string.Empty;

    /// <summary>What SQLite says it is using: WAL on a disk, the rollback journal on a share.</summary>
    public string JournalMode { get; set; } = "?";

    public string ThisPc { get; set; } = string.Empty;

    /// <summary>True after Prepare/Join until the program is restarted.</summary>
    public bool RestartPending { get; set; }

    public bool IsLan => Mode != ShopMode.Single;

    public string ReachLabel => Reachable ? "reachable" : "NOT reachable";

    public string FolderLabel => DatabaseFolder.Length == 0 ? "-" : DatabaseFolder;

    public string RestartLabel => RestartPending
        ? "Restart Area11 on this PC to start using the new database."
        : "This PC is using the folder above.";

    public List<LanConnectionRow> Connections { get; set; } = new();
}

/// <summary>One line from the black box: a counter that connected to the shop's file.</summary>
public sealed class LanConnectionRow
{
    public string? At { get; set; }

    /// <summary>Who pressed the button on that PC (the owner).</summary>
    public string? UserName { get; set; }

    /// <summary>The PC name, written by the counter that joined.</summary>
    public string? Details { get; set; }

    public string WhenLabel => string.IsNullOrWhiteSpace(At)
        ? "-"
        : At!.Length > 16 ? At[..16] : At!;

    public string PcLabel => string.IsNullOrWhiteSpace(Details) ? "-" : Details!.Trim();

    public string UserLabel => string.IsNullOrWhiteSpace(UserName) ? "-" : UserName!.Trim();
}
