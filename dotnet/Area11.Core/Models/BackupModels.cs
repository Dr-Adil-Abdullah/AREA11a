namespace Area11.Core.Models;

/// <summary>
/// One backup file on disk, as the Backup page lists it. Only the numbers the owner needs: when it
/// was made, how big it is, and whether the program or the owner made it.
/// </summary>
public sealed class BackupFile
{
    public string FileName { get; set; } = string.Empty;

    public string FullPath { get; set; } = string.Empty;

    /// <summary>Sorted newest first by the name; the ISO stamp in it sorts by itself.</summary>
    public string Stamp { get; set; } = string.Empty;

    /// <summary>True for the file the program made by itself (the daily one).</summary>
    public bool IsAutomatic { get; set; }

    /// <summary>True for the full, visible recovery zip made immediately before a restore.</summary>
    public bool IsSafetyCopy { get; set; }

    /// <summary>Manifest time, when an old or hand-made zip supplies one.</summary>
    public string MadeAt { get; set; } = string.Empty;

    /// <summary>Area11 version that wrote the zip, so an old format is visible before restoring.</summary>
    public string BackupVersion { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    /// <summary>"412 KB" - a number of bytes means nothing to a shopkeeper.</summary>
    public string SizeLabel => SizeBytes >= 1024 * 1024
        ? $"{(SizeBytes / 1024.0 / 1024.0):0.0} MB"
        : $"{Math.Max(1, SizeBytes / 1024)} KB";

    /// <summary>How it was made, in the words the owner used: by hand or by the program.</summary>
    public string KindLabel => IsSafetyCopy ? "Before restore" : IsAutomatic ? "Automatic" : "By hand";

    public string Caption => $"{Stamp}  ·  {SizeLabel}  ·  {KindLabel}";
}

/// <summary>
/// The answer after a restore. It names the safety copy as well, because the first question after a
/// restore goes wrong is "can I go back?" - and the answer must already be on the screen.
/// </summary>
public sealed class RestoreResult
{
    public string BackupName { get; set; } = string.Empty;

    /// <summary>When the backup itself was made (read from the manifest inside it).</summary>
    public string BackupMadeAt { get; set; } = string.Empty;

    /// <summary>The Area11 version that made the backup, so an old file is noticed.</summary>
    public string BackupVersion { get; set; } = string.Empty;

    /// <summary>The copy of the shop's own database taken just before the swap. Never deleted here.</summary>
    public string SafetyCopy { get; set; } = string.Empty;

    public int Products { get; set; }

    public int Sales { get; set; }

    public int Photos { get; set; }

    public string SummaryText =>
        $"{BackupName} restored. {Products} medicine(s), {Sales} bill(s), {Photos} photo(s). " +
        $"The copy taken before the swap is {SafetyCopy}.";
}
