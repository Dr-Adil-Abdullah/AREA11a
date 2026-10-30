using System.Globalization;
using System.IO;
using System.IO.Compression;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;
using Microsoft.Data.Sqlite;

namespace Area11.Core.Services;

/// <summary>
/// Backup and restore (F-36, Spec 23). One file for the whole shop: the database **and** the photos,
/// in a single `.zip` the owner can copy to a USB stick.
///
/// Three rules shape this class, and each one comes from a way real backups fail:
///
///   1. **A backup is a copy taken by SQLite itself** (<c>VACUUM INTO</c>), never a file copy of a
///      database that is being written. A file copy taken mid-bill can be a torn file that looks
///      fine until the day it is needed.
///   2. **Restore never happens without a safety copy.** The shop's own database is copied aside
///      first, so a wrong click costs one afternoon, not the shop's whole history.
///   3. **The program only ever deletes its own automatic files.** The owner's own backups are kept
///      for ever, however many there are: an old zip costs a few hundred kilobytes, losing one can
///      cost the shop years.
///
/// The file layout inside the zip is fixed and readable by a human:
///   area11.db              the complete database
///   photos/…               the photos, in their folders
///   area11-backup.txt      when it was made, by which version, how many photos
/// </summary>
public sealed class BackupService
{
    /// <summary>Every file this service makes starts with this, so it can never touch a foreign file.</summary>
    public const string FilePrefix = "area11-backup-";

    /// <summary>The safety copy taken before a restore. Deliberately a different prefix: never trimmed.</summary>
    public const string SafetyPrefix = "area11-before-restore-";

    private const string DatabaseEntry = "area11.db";
    private const string ManifestEntry = "area11-backup.txt";
    private const string PhotoFolderEntry = "photos/";
    // Milliseconds, because two backups in one second must still sort in the right order: the list is
    // sorted by name and the trimming deletes the LAST rows. One second of resolution let a fresh
    // backup be deleted instead of yesterday's.
    private const string StampFormat = "yyyyMMdd-HHmmss-fff";

    private const string LegacyStampFormat = "yyyyMMdd-HHmmss";
    private const string TimeFormat = "yyyy-MM-dd HH:mm:ss";

    private readonly Database _db;
    private readonly SettingsService _settings;
    private readonly AuditService _audit;
    private readonly PermissionService _permissions;
    private readonly LogService _log;
    private readonly string _photosFolder;
    private readonly string _backupsFolder;

    /// <param name="photosFolder">The shop's photo folder; null means the real one (tests pass their own).</param>
    /// <param name="backupsFolder">Where the zips go; null means the real one (tests pass their own).</param>
    public BackupService(
        Database db,
        SettingsService settings,
        AuditService audit,
        PermissionService permissions,
        LogService log,
        string? photosFolder = null,
        string? backupsFolder = null)
    {
        _db = db;
        _settings = settings;
        _audit = audit;
        _permissions = permissions;
        _log = log;

        _photosFolder = string.IsNullOrWhiteSpace(photosFolder) ? AppPaths.PhotosFolder : photosFolder;
        _backupsFolder = string.IsNullOrWhiteSpace(backupsFolder) ? AppPaths.BackupsFolder : backupsFolder;

        Directory.CreateDirectory(_backupsFolder);
    }

    /// <summary>The folder the zips live in - shown on the page so the owner can open it.</summary>
    public string BackupsFolder => _backupsFolder;

    /// <summary>How many automatic backups the program keeps. Clamped: 0 would delete today's backup.</summary>
    public int KeepCount => Math.Clamp(_settings.GetInt("backup.keep", 7), 1, 500);

    public bool AutomaticEnabled => _settings.GetBool("backup.autoEnabled");

    public int EveryHours => Math.Clamp(_settings.GetInt("backup.everyHours", 24), 1, 24 * 30);

    /// <summary>When the last backup was made, in words for the page. Empty = never.</summary>
    public string LastBackupText
    {
        get
        {
            var last = _settings.GetString("backup.lastFile");
            return string.IsNullOrWhiteSpace(last) ? "No backup yet." : last;
        }
    }

    /// <summary>Recovery instructions survive the restart that follows a restore.</summary>
    public string LastRestoreSummary => _settings.GetString("backup.lastRestoreSummary");

    // ------------------------------ Creating ------------------------------

    /// <summary>
    /// Makes one zip and returns it. <paramref name="includePhotos"/> false makes a database-only
    /// backup, which is smaller but loses every photo (Spec 23.1 offers the choice; the page says so).
    /// </summary>
    /// <param name="destinationFile">
    /// Optional owner-chosen zip path. The live database never moves, and an existing file is never
    /// overwritten: a suffix is added instead. Automatic backups always use the managed folder.
    /// </param>
    public ServiceResult<BackupFile> Create(User? user, bool includePhotos = true, bool automatic = false,
        string? destinationFile = null)
    {
        // The program's own daily backup has no signed-in person; anybody else must hold the
        // permission. The menu hides the page, this line is what actually refuses.
        if (!automatic && !CanUse(user))
        {
            return ServiceResult<BackupFile>.Fail(
                "Only the owner can make a backup. Ask the owner to sign in.");
        }

        var tempDatabase = Path.Combine(_backupsFolder, $".{Guid.NewGuid():N}.db");
        string? path = null;

        try
        {
            var stamp = TimeSource.Now.ToString(StampFormat, CultureInfo.InvariantCulture);
            var name = $"{FilePrefix}{stamp}{(automatic ? "-auto" : string.Empty)}.zip";
            var folder = _backupsFolder;

            if (destinationFile is not null)
            {
                if (automatic)
                {
                    return ServiceResult<BackupFile>.Fail(
                        "An automatic backup must stay in the managed backup folder.");
                }

                var requested = Path.GetFullPath(destinationFile);

                if (!string.Equals(Path.GetExtension(requested), ".zip", StringComparison.OrdinalIgnoreCase))
                {
                    return ServiceResult<BackupFile>.Fail("Choose an Area11 backup file ending in .zip.");
                }

                if (string.Equals(requested, _db.FilePath, StringComparison.OrdinalIgnoreCase))
                {
                    return ServiceResult<BackupFile>.Fail("The live shop database cannot be used as a backup file.");
                }

                folder = Path.GetDirectoryName(requested)
                    ?? throw new InvalidOperationException("The backup needs a destination folder.");
                Directory.CreateDirectory(folder);
                name = Path.GetFileName(requested);
            }

            path = UniquePath(Path.Combine(folder, name));

            // 1. A consistent copy of the database, taken by SQLite while other people keep billing.
            CopyDatabase(tempDatabase);

            // 2. The photos that actually exist - a shop with none gets a 0-photo backup, not an error.
            var photos = includePhotos ? CollectPhotos() : new List<string>();

            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(tempDatabase, DatabaseEntry, CompressionLevel.Optimal);

                foreach (var photo in photos)
                {
                    // The zip keeps the photos/ folder and any sub-folder, so a restore puts every
                    // picture back exactly where the database says it is.
                    var entryName = PhotoFolderEntry + RelativeToPhotos(photo).Replace('\\', '/');
                    zip.CreateEntryFromFile(photo, entryName, CompressionLevel.Optimal);
                }

                WriteManifest(zip, photos.Count);
            }

            var size = new FileInfo(path).Length;
            var file = new BackupFile
            {
                FileName = Path.GetFileName(path),
                FullPath = path,
                Stamp = PrettyStamp(stamp),
                IsAutomatic = automatic,
                IsSafetyCopy = name.StartsWith(SafetyPrefix, StringComparison.Ordinal),
                BackupVersion = BuildInfo.Version,
                SizeBytes = size
            };

            _audit.RecordStandalone(
                user,
                "Backup",
                automatic ? "backup_auto" : "backup_create",
                $"{file.FileName} ({photos.Count} photo(s), database{(includePhotos ? " + photos" : " only")})");

            _log.Info($"Backup made: {file.FileName} ({file.SizeLabel}, {photos.Count} photos).");

            // Only the program's own files are trimmed; anything the owner made stays.
            var removed = TrimAutomatic();
            if (removed > 0)
            {
                _log.Info($"{removed} old automatic backup(s) removed (keep {KeepCount}).");
            }

            return ServiceResult<BackupFile>.Success(file);
        }
        catch (Exception exception)
        {
            // A half-written zip is a trap: it looks like a backup and opens as a broken file. It is
            // removed, so the list only ever shows files that were finished.
            TryDelete(path ?? string.Empty);

            _log.Error("The backup could not be made.", exception);
            return ServiceResult<BackupFile>.Fail(
                "The backup could not be made. The log file has the details: " + exception.Message);
        }
        finally
        {
            TryDelete(tempDatabase);
        }
    }

    /// <summary>Every backup and visible pre-restore recovery zip, newest first.</summary>
    public IReadOnlyList<BackupFile> List()
    {
        var files = new List<BackupFile>();

        if (!Directory.Exists(_backupsFolder))
        {
            return files;
        }

        foreach (var path in Directory.EnumerateFiles(_backupsFolder, "area11-*.zip"))
        {
            try
            {
                var name = Path.GetFileName(path);
                var regular = name.StartsWith(FilePrefix, StringComparison.Ordinal);
                var safety = name.StartsWith(SafetyPrefix, StringComparison.Ordinal);

                if (!regular && !safety)
                {
                    continue;
                }

                var manifest = ReadManifestInfo(path);

                files.Add(new BackupFile
                {
                    FileName = name,
                    FullPath = path,
                    Stamp = StampOf(name),
                    IsAutomatic = regular && name.Contains("-auto", StringComparison.Ordinal),
                    IsSafetyCopy = safety,
                    MadeAt = manifest.MadeAt,
                    BackupVersion = manifest.Version,
                    SizeBytes = new FileInfo(path).Length
                });
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A file that vanished between listing and reading is not a reason to fail a page.
                _log.Warn($"A backup file could not be read: {path}");
            }
        }

        // Regular and safety names have different prefixes, so compare their real stamps rather
        // than sorting the prefix text and accidentally putting every safety copy at one end.
        return files.OrderByDescending(SortTime).ThenByDescending(file => file.FileName, StringComparer.Ordinal).ToList();
    }

    private static DateTime SortTime(BackupFile file)
    {
        if (DateTime.TryParseExact(file.MadeAt, TimeFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var manifestTime))
        {
            return manifestTime;
        }

        var name = file.FileName;

        if (name.StartsWith(FilePrefix, StringComparison.Ordinal))
        {
            name = name[FilePrefix.Length..];
        }
        else if (name.StartsWith(SafetyPrefix, StringComparison.Ordinal))
        {
            name = name[SafetyPrefix.Length..];
        }

        return ReadStamp(name) ?? DateTime.MinValue;
    }

    /// <summary>Human age for the red restore panel; an old hand-made zip may not record one.</summary>
    public string BackupAge(BackupFile file)
    {
        if (!DateTime.TryParseExact(file.MadeAt, TimeFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var madeAt))
        {
            return "age not recorded";
        }

        var age = TimeSource.Now - madeAt;

        if (age < TimeSpan.Zero)
        {
            return "backup clock is ahead";
        }

        if (age.TotalDays >= 1)
        {
            return $"{(int)age.TotalDays} day(s) old";
        }

        return age.TotalHours >= 1
            ? $"{(int)age.TotalHours} hour(s) old"
            : "less than one hour old";
    }

    /// <summary>A clear warning when the live books contain work newer than the selected backup.</summary>
    public string RestoreWarning(BackupFile file)
    {
        if (!DateTime.TryParseExact(file.MadeAt, TimeFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var madeAt))
        {
            return string.Empty;
        }

        string? latestText;

        try
        {
            latestText = _db.Tx(connection => _db.Scalar(connection,
                "SELECT MAX(at) FROM audit_logs WHERE COALESCE(module, '') <> 'Backup';")?.ToString());
        }
        catch (Exception exception) when (exception is SqliteException or InvalidOperationException)
        {
            _log.Warn("The newer-work check before restore could not be read.");
            return " The newer-work check is unavailable; keep the full recovery zip after restoring.";
        }

        if (DateTime.TryParseExact(latestText, TimeFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var latest) && latest > madeAt)
        {
            return $" Newer work exists in the live shop (latest change {latestText}). " +
                   "It will be preserved in the full recovery zip before anything is replaced.";
        }

        return string.Empty;
    }

    /// <summary>Reads only the small manifest entry; large photo entries are never loaded for the list.</summary>
    private static (string MadeAt, string Version) ReadManifestInfo(string path)
    {
        try
        {
            using var zip = ZipFile.OpenRead(path);
            var entry = zip.Entries.FirstOrDefault(item =>
                string.Equals(item.FullName, ManifestEntry, StringComparison.OrdinalIgnoreCase));

            if (entry is null)
            {
                return (string.Empty, string.Empty);
            }

            var madeAt = string.Empty;
            var version = string.Empty;
            using var reader = new StreamReader(entry.Open());

            while (reader.ReadLine() is { } line)
            {
                if (line.StartsWith("madeAt=", StringComparison.Ordinal))
                {
                    madeAt = line[7..].Trim();
                }
                else if (line.StartsWith("version=", StringComparison.Ordinal))
                {
                    version = line[8..].Trim();
                }
            }

            return (madeAt, version);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return (string.Empty, string.Empty);
        }
    }

    /// <summary>
    /// Runs the daily backup if it is switched on and due. Safe to call at every sign-in and every
    /// time the backup page opens: it does nothing until the interval has passed.
    ///
    /// Nothing is thrown and nothing is shown here - a failed automatic backup must never stop a
    /// cashier from making a bill. The failure is in the log and on the backup page.
    /// </summary>
    public BackupFile? RunAutomaticIfDue()
    {
        try
        {
            if (!AutomaticEnabled)
            {
                return null;
            }

            var last = _settings.GetString("backup.lastRunAt");

            if (DateTime.TryParseExact(last, TimeFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var when)
                && TimeSource.Now < when.AddHours(EveryHours))
            {
                return null;
            }

            var created = Create(null, includePhotos: true, automatic: true);

            if (!created.Ok || created.Value is null)
            {
                _log.Warn("The automatic backup did not run: " + (created.Error ?? "unknown reason"));
                return null;
            }

            // Bookkeeping, not a business change: the quiet door keeps the black box readable. The
            // backup itself already wrote its own line ("backup_auto").
            _settings.SetUiOnly("backup.lastRunAt", Clock.Now());
            _settings.SetUiOnly("backup.lastFile", created.Value.FileName);

            return created.Value;
        }
        catch (Exception exception)
        {
            _log.Error("The automatic backup did not run.", exception);
            return null;
        }
    }

    // ------------------------------ Restoring -----------------------------

    /// <summary>
    /// Puts a backup back (Spec 23.2). The file is checked first, the shop's current database is
    /// copied aside second, and only then is anything replaced.
    ///
    /// After this call the database file on disk is the old one, so the program must be started
    /// again: every open page holds numbers read from the file that no longer exists. The desktop
    /// does that by itself (see <c>AppRestart</c>) - the owner only sees the window come back.
    /// </summary>
    public ServiceResult<RestoreResult> Restore(string sourcePath, User? user)
    {
        if (!CanUse(user))
        {
            _audit.RecordStandalone(user, "Backup", "denied", "restore");
            return ServiceResult<RestoreResult>.Fail(
                "Only the owner can restore a backup. Nothing was changed.");
        }

        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return ServiceResult<RestoreResult>.Fail("That file was not found. Nothing was changed.");
        }

        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();

        if (extension is not (".zip" or ".db"))
        {
            return ServiceResult<RestoreResult>.Fail(
                "Choose an Area11 backup (.zip) or a database file (.db). Nothing was changed.");
        }

        var unpacked = Path.Combine(Path.GetTempPath(), $"area11-restore-{Guid.NewGuid():N}.db");
        var stagedPhotos = Path.Combine(Path.GetTempPath(), $"area11-restore-photos-{Guid.NewGuid():N}");
        string? safetyPath = null;
        var databaseReplaced = false;
        var photosTouched = false;
        var previousPhotos = new List<string>();
        var photosInside = 0;

        try
        {
            var manifest = new Dictionary<string, string>(StringComparer.Ordinal);

            if (extension == ".zip")
            {
                var read = Unpack(sourcePath, unpacked, manifest);

                if (!read.Ok)
                {
                    return ServiceResult<RestoreResult>.Fail(read.Error ?? "The zip could not be read.");
                }

                photosInside = PhotosInside(sourcePath);
            }
            else
            {
                File.Copy(sourcePath, unpacked, overwrite: true);
            }

            var checkedDatabase = Validate(unpacked);

            if (!checkedDatabase.Ok)
            {
                return ServiceResult<RestoreResult>.Fail(
                    (checkedDatabase.Error ?? "The file is not an Area11 database.") + " Nothing was changed.");
            }

            // Read and stage the source before creating the safety copy: automatic retention may
            // trim an old source zip, but it can no longer interrupt this restore halfway through.
            if (extension == ".zip" && photosInside > 0)
            {
                Directory.CreateDirectory(stagedPhotos);
                ExtractPhotos(sourcePath, stagedPhotos);
            }

            // A full recovery zip, not a hidden database-only copy. If this cannot be made, stop here.
            var safetyName = SafetyPrefix + TimeSource.Now.ToString(StampFormat, CultureInfo.InvariantCulture) + ".zip";
            var requestedSafety = Path.Combine(_backupsFolder, safetyName);
            var safety = Create(user, includePhotos: true, automatic: false, destinationFile: requestedSafety);

            if (!safety.Ok || safety.Value is null)
            {
                return ServiceResult<RestoreResult>.Fail(
                    "A full safety backup could not be made, so nothing was restored. " +
                    (safety.Error ?? "See the log for details."));
            }

            safetyPath = safety.Value.FullPath;

            if (Directory.Exists(stagedPhotos))
            {
                previousPhotos = CollectPhotos();
            }

            ReplaceDatabase(unpacked);
            databaseReplaced = true;

            if (Directory.Exists(stagedPhotos))
            {
                photosTouched = true;
                CopyPhotoTree(stagedPhotos, _photosFolder);
            }

            _settings.Reload();

            var result = new RestoreResult
            {
                BackupName = Path.GetFileName(sourcePath),
                BackupMadeAt = manifest.TryGetValue("madeAt", out var made) ? made : string.Empty,
                BackupVersion = manifest.TryGetValue("version", out var version) ? version : string.Empty,
                SafetyCopy = Path.GetFileName(safetyPath),
                Products = checkedDatabase.Products,
                Sales = checkedDatabase.Sales,
                Photos = photosInside
            };

            var remembered = _settings.SetUiOnly("backup.lastRestoreSummary", result.SummaryText);

            if (!remembered.Ok)
            {
                throw new InvalidOperationException(remembered.Error ?? "Recovery details could not be saved.");
            }

            _audit.RecordStandalone(user, "Backup", "backup_restore",
                $"{result.BackupName} restored; full recovery zip {result.SafetyCopy}");

            _log.Info($"Restored {result.BackupName}; full recovery zip {result.SafetyCopy}.");

            return ServiceResult<RestoreResult>.Success(result);
        }
        catch (Exception exception)
        {
            var liveTouched = databaseReplaced || photosTouched;
            var recovered = TryRollback(safetyPath, databaseReplaced, photosTouched, previousPhotos);
            _log.Error("The backup could not be restored safely.", exception);

            var state = !liveTouched
                ? "Nothing in the live shop was replaced."
                : recovered
                    ? "The original shop data was put back from the recovery zip."
                    : "Recovery may be required; use the named full recovery zip before doing more work.";
            var safetyName = safetyPath is null ? "No recovery zip was created." :
                "Recovery zip: " + Path.GetFileName(safetyPath) + ".";

            return ServiceResult<RestoreResult>.Fail(
                $"The backup could not be restored safely. {state} {safetyName} {exception.Message}");
        }
        finally
        {
            TryDelete(unpacked);
            TryDeleteDirectory(stagedPhotos);
        }
    }

    /// <summary>Same-volume staged replacement keeps the old file intact until the new one is ready.</summary>
    private void ReplaceDatabase(string sourcePath)
    {
        var folder = Path.GetDirectoryName(_db.FilePath)
            ?? throw new InvalidOperationException("The live database has no containing folder.");
        var staged = Path.Combine(folder, $".area11-live-{Guid.NewGuid():N}.db");

        var replaced = false;

        try
        {
            SqliteConnection.ClearAllPools();
            File.Copy(sourcePath, staged, overwrite: true);
            File.Move(staged, _db.FilePath, overwrite: true);
            replaced = true;
        }
        finally
        {
            TryDelete(staged);

            // A failed copy or locked destination must leave the CURRENT database and its WAL /
            // journal companions untouched. Only after the same-volume swap succeeds are those
            // companions stale: deleting them earlier could discard committed work while the
            // restore error still says that nothing in the live shop was replaced.
            if (replaced)
            {
                RemoveSidecars(_db.FilePath);
            }
        }
    }

    private bool TryRollback(string? safetyPath, bool databaseReplaced, bool photosTouched,
        IReadOnlyCollection<string> previousPhotos)
    {
        if (!databaseReplaced && !photosTouched)
        {
            return true;
        }

        if (safetyPath is null || !File.Exists(safetyPath))
        {
            return false;
        }

        var rollbackDatabase = Path.Combine(Path.GetTempPath(), $"area11-rollback-{Guid.NewGuid():N}.db");

        try
        {
            if (databaseReplaced)
            {
                var manifest = new Dictionary<string, string>(StringComparer.Ordinal);
                var unpacked = Unpack(safetyPath, rollbackDatabase, manifest);

                if (!unpacked.Ok)
                {
                    throw new InvalidDataException(unpacked.Error ?? "The recovery database could not be read.");
                }

                ReplaceDatabase(rollbackDatabase);
            }

            if (photosTouched)
            {
                var keep = new HashSet<string>(previousPhotos, StringComparer.OrdinalIgnoreCase);

                foreach (var extra in CollectPhotos().Where(path => !keep.Contains(path)))
                {
                    TryDelete(extra);
                }

                ExtractPhotos(safetyPath, _photosFolder);
            }

            _settings.Reload();
            return true;
        }
        catch (Exception exception)
        {
            _log.Error("The backup restore rollback also failed.", exception);
            return false;
        }
        finally
        {
            TryDelete(rollbackDatabase);
        }
    }

    private static void CopyPhotoTree(string sourceFolder, string destinationFolder)
    {
        Directory.CreateDirectory(destinationFolder);

        foreach (var source in Directory.EnumerateFiles(sourceFolder, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceFolder, source);
            var destination = Path.Combine(destinationFolder, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? destinationFolder);
            File.Copy(source, destination, overwrite: true);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A temporary folder is harmless; the operating system can sweep it later.
        }
    }

    /// <summary>
    /// The copy of the shop database taken before a restore or (later) before a cloud pull
    /// (Spec 23.4). Public because the sync feature will need the same guarantee.
    /// </summary>
    public ServiceResult<string> SafetyCopy(string tag)
    {
        try
        {
            var clean = string.IsNullOrWhiteSpace(tag) ? "safety" : tag.Trim().ToLowerInvariant();
            var name = $"area11-before-{clean}-{TimeSource.Now.ToString(StampFormat, CultureInfo.InvariantCulture)}.db";
            var path = UniquePath(Path.Combine(_backupsFolder, name));

            CopyDatabase(path);

            _log.Info($"Safety copy made: {Path.GetFileName(path)}.");
            return ServiceResult<string>.Success(path);
        }
        catch (Exception exception)
        {
            _log.Error("The safety copy could not be made.", exception);
            return ServiceResult<string>.Fail("The safety copy could not be made.");
        }
    }

    // ------------------------------ Helpers -------------------------------

    /// <summary>Owner (or somebody the owner trusted with backup) - the module, not the role.</summary>
    private bool CanUse(User? user) => user is not null && _permissions.Can(user, Modules.Backup);

    /// <summary>SQLite's own consistent copy. Works while bills are being made.</summary>
    private void CopyDatabase(string target)
    {
        if (File.Exists(target))
        {
            File.Delete(target);
        }

        using var connection = _db.Open();
        using var command = connection.CreateCommand();

        // A path with a quote in it would end the string early; doubling is SQLite's escape.
        command.CommandText = "VACUUM INTO '" + target.Replace("'", "''") + "';";
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Checks that a file really is an Area11 database before it is allowed anywhere near the shop's
    /// own. <c>quick_check</c> catches a truncated file; the three table names catch "wrong file".
    /// </summary>
    private static ValidationResult Validate(string path)
    {
        try
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            };

            using var connection = new SqliteConnection(builder.ToString());
            connection.Open();

            using (var check = connection.CreateCommand())
            {
                check.CommandText = "PRAGMA quick_check;";
                var answer = check.ExecuteScalar()?.ToString();

                if (!string.Equals(answer, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    return ValidationResult.Fail("The file is damaged - SQLite says: " + (answer ?? "no answer"));
                }
            }

            using (var tables = connection.CreateCommand())
            {
                tables.CommandText =
                    "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' " +
                    "AND name IN ('products', 'sales', '_migrations');";

                if (Convert.ToInt64(tables.ExecuteScalar() ?? 0L) < 3)
                {
                    return ValidationResult.Fail("The file is a database, but not an Area11 one.");
                }
            }

            long Count(string sql)
            {
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                return Convert.ToInt64(command.ExecuteScalar() ?? 0L);
            }

            return ValidationResult.Success((int)Count("SELECT COUNT(1) FROM products;"),
                (int)Count("SELECT COUNT(1) FROM sales;"));
        }
        catch (Exception exception) when (exception is SqliteException or InvalidOperationException)
        {
            return ValidationResult.Fail("The file could not be opened as a database: " + exception.Message);
        }
    }

    /// <summary>Pulls area11.db out of the zip. The manifest is read only to describe the backup.</summary>
    private static ServiceResult<string> Unpack(string zipPath, string target, Dictionary<string, string> manifest)
    {
        try
        {
            using var zip = ZipFile.OpenRead(zipPath);

            var database = zip.Entries.FirstOrDefault(entry =>
                string.Equals(entry.FullName, DatabaseEntry, StringComparison.OrdinalIgnoreCase));

            if (database is null)
            {
                return ServiceResult<string>.Fail(
                    "The zip does not contain area11.db - it is not an Area11 backup.");
            }

            database.ExtractToFile(target, overwrite: true);

            var text = zip.Entries.FirstOrDefault(entry =>
                string.Equals(entry.FullName, ManifestEntry, StringComparison.OrdinalIgnoreCase));

            if (text is not null)
            {
                using var reader = new StreamReader(text.Open());

                while (reader.ReadLine() is { } line)
                {
                    var split = line.IndexOf('=');
                    if (split > 0)
                    {
                        manifest[line[..split].Trim()] = line[(split + 1)..].Trim();
                    }
                }
            }

            return ServiceResult<string>.Success(target);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return ServiceResult<string>.Fail("The zip could not be opened: " + exception.Message);
        }
    }

    private static int PhotosInside(string zipPath)
    {
        try
        {
            using var zip = ZipFile.OpenRead(zipPath);
            return zip.Entries.Count(entry =>
                entry.FullName.StartsWith(PhotoFolderEntry, StringComparison.OrdinalIgnoreCase)
                && !entry.FullName.EndsWith("/", StringComparison.Ordinal));
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            return 0;
        }
    }

    private static void ExtractPhotos(string zipPath, string destinationFolder)
    {
        Directory.CreateDirectory(destinationFolder);
        var root = Path.GetFullPath(destinationFolder);

        using var zip = ZipFile.OpenRead(zipPath);

        foreach (var entry in zip.Entries)
        {
            if (!entry.FullName.StartsWith(PhotoFolderEntry, StringComparison.OrdinalIgnoreCase)
                || entry.FullName.EndsWith("/", StringComparison.Ordinal))
            {
                continue;
            }

            var relative = entry.FullName[PhotoFolderEntry.Length..].Replace('/', Path.DirectorySeparatorChar);
            var full = Path.GetFullPath(Path.Combine(root, relative));
            var fromRoot = Path.GetRelativePath(root, full);

            // StartsWith(root) is not enough: "photos-old" also starts with "photos". Reject both
            // parent traversal and rooted paths before any live photo or database is touched.
            if (Path.IsPathRooted(fromRoot) || fromRoot == ".."
                || fromRoot.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"A photo in the backup pointed outside the photo folder: {entry.FullName}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(full) ?? root);
            entry.ExtractToFile(full, overwrite: true);
        }
    }

    private List<string> CollectPhotos()
    {
        if (!Directory.Exists(_photosFolder))
        {
            return new List<string>();
        }

        return Directory.EnumerateFiles(_photosFolder, "*", SearchOption.AllDirectories)
            .Where(path => !Path.GetFileName(path).StartsWith('.'))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string RelativeToPhotos(string path) =>
        Path.GetRelativePath(_photosFolder, path);

    /// <summary>
    /// Deletes the oldest of the program's own automatic backups, keeping <see cref="KeepCount"/>.
    /// Manual backups - anything without "-auto" - are never touched here.
    /// </summary>
    private int TrimAutomatic()
    {
        var keep = KeepCount;

        // List() is newest first (the name is an ISO stamp), so everything past "keep" is the oldest.
        var automaticFiles = List()
            .Where(file => file.IsAutomatic)
            .ToList();

        var removed = 0;

        foreach (var file in automaticFiles.Skip(keep))
        {
            try
            {
                File.Delete(file.FullPath);
                removed++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _log.Warn($"An old automatic backup could not be removed: {file.FileName}");
            }
        }

        return removed;
    }

    private void WriteManifest(ZipArchive zip, int photos)
    {
        var entry = zip.CreateEntry(ManifestEntry, CompressionLevel.NoCompression);
        using var writer = new StreamWriter(entry.Open());

        writer.WriteLine("area11-backup");
        writer.WriteLine("version=" + BuildInfo.Version);
        writer.WriteLine("codeName=" + BuildInfo.CodeName);
        writer.WriteLine("madeAt=" + Clock.Now());
        writer.WriteLine("photos=" + photos.ToString(CultureInfo.InvariantCulture));
        writer.WriteLine("store=" + _settings.GetString("store.name"));
    }

    /// <summary>Two backups in the same second must not overwrite each other.</summary>
    private static string UniquePath(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var folder = Path.GetDirectoryName(path) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);

        for (var round = 2; round < 100; round++)
        {
            var candidate = Path.Combine(folder, $"{name}-{round}{extension}");

            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(folder, $"{name}-{Guid.NewGuid():N}{extension}");
    }

    private static void RemoveSidecars(string databasePath)
    {
        foreach (var suffix in new[] { "-wal", "-shm", "-journal" })
        {
            TryDelete(databasePath + suffix);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A leftover temporary file is harmless; the next backup makes another one.
        }
    }

    /// <summary>"…-20261022-143005-123.zip" → "22 Oct 2026, 14:30" for the list. Bad names show as they are.</summary>
    private static string StampOf(string fileName)
    {
        var bare = fileName;

        if (bare.StartsWith(FilePrefix, StringComparison.Ordinal))
        {
            bare = bare[FilePrefix.Length..];
        }
        else if (bare.StartsWith(SafetyPrefix, StringComparison.Ordinal))
        {
            bare = bare[SafetyPrefix.Length..];
        }

        if (bare.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            bare = bare[..^4];
        }

        var made = ReadStamp(bare);

        return made is null
            ? bare
            : made.Value.ToString("dd MMM yyyy, HH:mm", CultureInfo.InvariantCulture);
    }

    private static string PrettyStamp(string stamp)
    {
        var made = ReadStamp(stamp);

        return made is null
            ? stamp
            : made.Value.ToString("dd MMM yyyy, HH:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Reads the leading stamp of a file name. The shorter format is the one v0.8.0's first draft
    /// used and a shop may still hold such a file - an old backup must list, not throw.
    /// </summary>
    private static DateTime? ReadStamp(string text)
    {
        var formats = new[] { StampFormat, LegacyStampFormat };

        foreach (var format in formats)
        {
            // A format string's literal characters are exactly the length that is needed here.
            if (text.Length >= format.Length
                && DateTime.TryParseExact(text[..format.Length], format, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var made))
            {
                return made;
            }
        }

        return null;
    }

    /// <summary>What the check of a candidate file found; the three numbers are shown after a restore.</summary>
    private sealed class ValidationResult
    {
        public bool Ok { get; private init; }

        public string? Error { get; private init; }

        public int Products { get; private init; }

        public int Sales { get; private init; }

        public static ValidationResult Success(int products, int sales) =>
            new() { Ok = true, Products = products, Sales = sales };

        public static ValidationResult Fail(string error) => new() { Ok = false, Error = error };
    }
}
