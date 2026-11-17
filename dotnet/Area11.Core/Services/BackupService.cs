using System.Text.RegularExpressions;
using System.Buffers.Binary;
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
/// in a password-encrypted `.a11bak` the owner can copy to a USB stick.
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
    private readonly IBackupPasswordStore? _passwordStore;
    private readonly string _stagingFolder;
    private const int MaxArchiveEntries = 60000;
    private const long MaxEntryBytes = 8L * 1024 * 1024 * 1024;

    /// <param name="photosFolder">The shop's photo folder; null means the real one (tests pass their own).</param>
    /// <param name="backupsFolder">Where the zips go; null means the real one (tests pass their own).</param>
    public BackupService(
        Database db,
        SettingsService settings,
        AuditService audit,
        PermissionService permissions,
        LogService log,
        string? photosFolder = null,
        string? backupsFolder = null,
        IBackupPasswordStore? passwordStore = null,
        string? stagingFolder = null)
    {
        _db = db;
        _settings = settings;
        _audit = audit;
        _permissions = permissions;
        _log = log;
        _passwordStore = passwordStore;
        _stagingFolder = stagingFolder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Area11", "private-backup-stage");

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

    public string LastFailure { get; private set; } = string.Empty;

    public bool PasswordConfigured
    {
        get { try { return !string.IsNullOrEmpty(_passwordStore?.Load()); } catch (Exception) { return false; } }
    }

    public ServiceResult ConfigurePassword(User? user, string password, string confirmation)
    {
        try
        {
            if (user is not { Active: true, IsOwner: true }) return ServiceResult.Fail("Only the current owner can set a backup password.");
            using var connection = _db.Open();
            OwnerAccess.Require(_db, connection, user);
            if (!string.Equals(password, confirmation, StringComparison.Ordinal)) return ServiceResult.Fail("The backup password confirmation does not match.");
            BackupPackage.ValidatePassword(password);
            if (_passwordStore is null) return ServiceResult.Fail("Protected backup credential storage is unavailable.");
            _passwordStore.Save(password);
            _audit.RecordStandalone(user, "Backup", "backup_password_configured", "changed: yes; older packages still need their original password");
            return ServiceResult.Success();
        }
        catch (ArgumentException) { return ServiceResult.Fail("Use a backup password of 12 to 1024 characters."); }
        catch (Exception) { return ServiceResult.Fail("The backup password could not be stored. Check the Windows account and protected storage."); }
    }

    private string StoredPassword()
    {
        var password = _passwordStore?.Load();
        if (string.IsNullOrEmpty(password))
            throw new ValidationException("Set up the backup password on this Windows account first. No unencrypted backup was made.");
        return password;
    }

    private string StageDirectory()
    {
        var folder = Path.Combine(_stagingFolder, Guid.NewGuid().ToString("N"));
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(folder); // inherits current user's profile ACL
        else Directory.CreateDirectory(folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return folder;
    }

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
        if (!CanUse(user)) return ServiceResult<BackupFile>.Fail("Current Backup permission is required to make a backup.");
        return CreateCore(user, includePhotos, automatic, destinationFile);
    }

    // Only the scheduler invokes this without an actor; public Create never trusts an automatic flag.
    private ServiceResult<BackupFile> CreateCore(User? user, bool includePhotos, bool automatic,
        string? destinationFile = null, string? passwordOverride = null)
    {
        string? stage = null;
        string? pending = null;
        string? path = null;
        try
        {
            var password = passwordOverride ?? StoredPassword();
            stage = StageDirectory();
            var tempDatabase = Path.Combine(stage, DatabaseEntry);
            var archivePath = Path.Combine(stage, "payload.zip");
            var stamp = TimeSource.Now.ToString(StampFormat, CultureInfo.InvariantCulture);
            var name = $"{FilePrefix}{stamp}{(automatic ? "-auto" : string.Empty)}{BackupPackage.Extension}";
            var folder = _backupsFolder;

            if (destinationFile is not null)
            {
                if (automatic)
                {
                    return ServiceResult<BackupFile>.Fail(
                        "An automatic backup must stay in the managed backup folder.");
                }

                var requested = Path.GetFullPath(destinationFile);

                if (!string.Equals(Path.GetExtension(requested), BackupPackage.Extension, StringComparison.OrdinalIgnoreCase))
                {
                    return ServiceResult<BackupFile>.Fail("Choose an encrypted Area11 backup file ending in .a11bak.");
                }

                if (string.Equals(requested, _db.FilePath, StringComparison.OrdinalIgnoreCase))
                {
                    return ServiceResult<BackupFile>.Fail("The live shop database cannot be used as a backup file.");
                }

                folder = Path.GetDirectoryName(requested)
                    ?? throw new InvalidOperationException("The backup needs a destination folder.");
                Directory.CreateDirectory(folder);
                name = Path.GetFileName(requested);
                if (IsManagedAutomaticName(name))
                    return ServiceResult<BackupFile>.Fail("The -auto file name is reserved for managed automatic backups. Choose a manual name.");
            }

            path = UniquePath(Path.Combine(folder, name));

            // 1. A consistent copy of the database, taken by SQLite while other people keep billing.
            CopyDatabase(tempDatabase);

            // 2. The photos that actually exist - a shop with none gets a 0-photo backup, not an error.
            var photos = includePhotos ? CollectPhotos() : new List<string>();
            long expanded = new FileInfo(tempDatabase).Length;
            if (expanded > MaxEntryBytes || photos.Count + 2 > MaxArchiveEntries) throw new InvalidDataException("Backup source limit exceeded.");
            foreach (var photo in photos)
            {
                var length = new FileInfo(photo).Length;
                expanded = checked(expanded + length);
                if (length > MaxEntryBytes || expanded > BackupPackage.MaxPayloadBytes - 65536)
                    throw new InvalidDataException("Backup source size limit exceeded.");
            }

            using (var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                WriteArchiveFile(zip, tempDatabase, DatabaseEntry);

                foreach (var photo in photos)
                {
                    // The zip keeps the photos/ folder and any sub-folder, so a restore puts every
                    // picture back exactly where the database says it is.
                    var entryName = PhotoFolderEntry + RelativeToPhotos(photo).Replace('\\', '/');
                    WriteArchiveFile(zip, photo, entryName);
                }

                WriteManifest(zip, photos.Count);
            }

            ValidateArchive(archivePath);
            pending = path + "." + Guid.NewGuid().ToString("N") + ".partial";
            using (var source = File.OpenRead(archivePath))
            using (var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                BackupPackage.Encrypt(source, output, password);
                output.Flush(flushToDisk: true);
            }
            File.Move(pending, path); // same-folder atomic publish, never overwrite an older backup
            pending = null;
            var size = new FileInfo(path).Length;
            var file = new BackupFile
            {
                FileName = Path.GetFileName(path),
                FullPath = path,
                Stamp = PrettyStamp(stamp),
                IsAutomatic = automatic,
                IsSafetyCopy = name.StartsWith(SafetyPrefix, StringComparison.Ordinal),
                BackupVersion = BuildInfo.Version,
                IsEncrypted = true,
                MadeAt = Clock.Now(),
                SizeBytes = size
            };

            _audit.RecordStandalone(
                user,
                "Backup",
                automatic ? "backup_auto" : "backup_create",
                $"{file.FileName} ({photos.Count} photo(s), database{(includePhotos ? " + photos" : " only")})");

            _log.Info($"Backup made: {file.FileName} ({file.SizeLabel}, {photos.Count} photos).");

            // Only the program's own files are trimmed; anything the owner made stays.
            var removed = TrimAutomatic(automatic ? path : null);
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
            TryDelete(pending ?? string.Empty); // never remove a pre-existing destination on collision
            _log.Warn("Encrypted backup creation failed; no plaintext fallback was used.");
            return ServiceResult<BackupFile>.Fail(exception is ValidationException
                ? "Set up the backup password on this Windows account first. No unencrypted backup was made."
                : "The encrypted backup could not be made. Check storage space and protected credential access.");
        }
        finally
        {
            if (stage is not null) TryDeleteDirectory(stage);
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

        foreach (var path in Directory.EnumerateFiles(_backupsFolder, "area11-*").Where(path =>
                     Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase)
                     || Path.GetExtension(path).Equals(BackupPackage.Extension, StringComparison.OrdinalIgnoreCase)
                     || Path.GetExtension(path).Equals(".db", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var name = Path.GetFileName(path);
                var regular = name.StartsWith(FilePrefix, StringComparison.Ordinal);
                var safety = name.StartsWith(SafetyPrefix, StringComparison.Ordinal)
                    || (name.StartsWith("area11-before-", StringComparison.Ordinal) && Path.GetExtension(path).Equals(".db", StringComparison.OrdinalIgnoreCase));

                if (!regular && !safety)
                {
                    continue;
                }

                var encrypted = Path.GetExtension(path).Equals(BackupPackage.Extension, StringComparison.OrdinalIgnoreCase);
                var nameTime = SortTime(new BackupFile { FileName = name }); // filename hint ONLY, not authenticated metadata

                files.Add(new BackupFile
                {
                    FileName = name,
                    FullPath = path,
                    IsEncrypted = encrypted,
                    Stamp = StampOf(name),
                    IsAutomatic = regular && IsManagedAutomaticName(name),
                    IsSafetyCopy = safety,
                    MadeAt = nameTime == DateTime.MinValue ? string.Empty : nameTime.ToString(TimeFormat, CultureInfo.InvariantCulture),
                    BackupVersion = string.Empty, // not known until password-authenticated restore
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
            return " The newer-work check is unavailable; keep the full encrypted recovery package after restoring.";
        }

        if (DateTime.TryParseExact(latestText, TimeFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var latest) && latest > madeAt)
        {
            return $" Newer work exists in the live shop (latest change {latestText}). " +
                   "It will be preserved in the full encrypted recovery package before anything is replaced.";
        }

        return string.Empty;
    }

    /// <summary>Reads only the small manifest entry; large photo entries are never loaded for the list.</summary>
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

            _ = StoredPassword(); // missing/inaccessible setup is conspicuous even before the next interval
            var last = _settings.GetString("backup.lastRunAt");

            if (DateTime.TryParseExact(last, TimeFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var when)
                && TimeSource.Now < when.AddHours(EveryHours))
            {
                return null;
            }

            var created = CreateCore(null, includePhotos: true, automatic: true);

            if (!created.Ok || created.Value is null)
            {
                LastFailure = created.Error ?? "Automatic encrypted backup failed.";
                _log.Warn("The automatic encrypted backup did not run. Check protected password setup and storage.");
                return null;
            }

            // Bookkeeping, not a business change: the quiet door keeps the black box readable. The
            // backup itself already wrote its own line ("backup_auto").
            LastFailure = string.Empty;
            _settings.SetUiOnly("backup.lastRunAt", Clock.Now());
            _settings.SetUiOnly("backup.lastFile", created.Value.FileName);

            return created.Value;
        }
        catch (Exception)
        {
            LastFailure = "Automatic encrypted backup failed. Check backup password setup and storage.";
            _log.Warn("The automatic encrypted backup did not run.");
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
    public ServiceResult<RestoreResult> Restore(string sourcePath, User? user, string? password = null)
    {
        if (!CanUse(user)) return ServiceResult<RestoreResult>.Fail("Current Backup permission is required. Nothing was changed.");
        string? stage = null;
        try
        {
            if (!Path.GetExtension(sourcePath).Equals(BackupPackage.Extension, StringComparison.OrdinalIgnoreCase))
                return ServiceResult<RestoreResult>.Fail("Choose an encrypted .a11bak package. Legacy ZIP/DB needs the explicit owner-only legacy recovery action.");
            var recoveryPassword = StoredPassword(); // needed for the new pre-restore safety copy
            stage = StageDirectory();
            var archive = Path.Combine(stage, "verified.zip");
            using (var input = File.OpenRead(sourcePath))
            using (var output = new FileStream(archive, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                BackupPackage.Decrypt(input, output, password ?? recoveryPassword);
            ValidateArchive(archive);
            return RestoreArchive(archive, user, Path.GetFileName(sourcePath), recoveryPassword, stage);
        }
        catch (Exception)
        {
            return ServiceResult<RestoreResult>.Fail("Backup verification failed: wrong password, damaged/unsupported package, unavailable protected credentials or unsafe archive. Nothing was changed.");
        }
        finally { if (stage is not null) TryDeleteDirectory(stage); }
    }

    public ServiceResult<RestoreResult> RestoreLegacy(string sourcePath, User? user, bool acknowledgeUnverified)
    {
        if (!acknowledgeUnverified || user is not { Active: true, IsOwner: true })
            return ServiceResult<RestoreResult>.Fail("Only the current owner can explicitly accept an unauthenticated legacy backup.");
        string? stage = null;
        try
        {
            using (var connection = _db.Open()) OwnerAccess.Require(_db, connection, user);
            var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            if (extension is not (".zip" or ".db")) return ServiceResult<RestoreResult>.Fail("Choose a legacy .zip or .db file.");
            if (new FileInfo(sourcePath).Length > MaxEntryBytes) throw new InvalidDataException("Legacy input exceeds the limit.");
            var recoveryPassword = StoredPassword();
            stage = StageDirectory();
            var staged = Path.Combine(stage, "legacy" + extension);
            File.Copy(sourcePath, staged);
            if (extension == ".zip") ValidateArchive(staged);
            return RestoreArchive(staged, user, Path.GetFileName(sourcePath), recoveryPassword, stage);
        }
        catch (Exception) { return ServiceResult<RestoreResult>.Fail("Legacy recovery could not be prepared. Check owner access, password setup, storage and the unverified source. Nothing was changed."); }
        finally { if (stage is not null) TryDeleteDirectory(stage); }
    }

    private ServiceResult<RestoreResult> RestoreArchive(string sourcePath, User? user, string originalName,
        string recoveryPassword, string stage)
    {
        if (!CanUse(user))
        {
            _audit.RecordStandalone(user, "Backup", "denied", "restore");
            return ServiceResult<RestoreResult>.Fail(
                "Current Backup permission is required. Nothing was changed.");
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

        var unpacked = Path.Combine(stage, "restore.db");
        var stagedPhotos = Path.Combine(stage, "photos");
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

            // A full encrypted recovery package, not a hidden database-only copy. If this cannot be made, stop here.
            var safetyName = SafetyPrefix + TimeSource.Now.ToString(StampFormat, CultureInfo.InvariantCulture) + BackupPackage.Extension;
            var requestedSafety = Path.Combine(_backupsFolder, safetyName);
            var safety = CreateCore(user, includePhotos: true, automatic: false,
                destinationFile: requestedSafety, passwordOverride: recoveryPassword);

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

            if (!CanUse(user)) throw new ValidationException("Backup access changed. Nothing was replaced.");
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
                BackupName = originalName,
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
                $"{result.BackupName} restored; full encrypted recovery package {result.SafetyCopy}");

            _log.Info($"Restored {result.BackupName}; full encrypted recovery package {result.SafetyCopy}.");

            return ServiceResult<RestoreResult>.Success(result);
        }
        catch (Exception exception)
        {
            var liveTouched = databaseReplaced || photosTouched;
            var recovered = TryRollback(safetyPath, databaseReplaced, photosTouched, previousPhotos, recoveryPassword, stage);
            _log.Error("The backup could not be restored safely.", exception);

            var state = !liveTouched
                ? "Nothing in the live shop was replaced."
                : recovered
                    ? "The original shop data was put back from the encrypted recovery package."
                    : "Recovery may be required; use the named full encrypted recovery package before doing more work.";
            var safetyName = safetyPath is null ? "No encrypted recovery package was created." :
                "Encrypted recovery package: " + Path.GetFileName(safetyPath) + ".";

            return ServiceResult<RestoreResult>.Fail(
                $"The backup could not be restored safely. {state} {safetyName}");
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
        IReadOnlyCollection<string> previousPhotos, string recoveryPassword, string stage)
    {
        if (!databaseReplaced && !photosTouched)
        {
            return true;
        }

        if (safetyPath is null || !File.Exists(safetyPath))
        {
            return false;
        }

        var rollbackDatabase = Path.Combine(stage, "rollback.db");
        var recoveryArchive = Path.Combine(stage, "recovery.zip");

        try
        {
            using (var input = File.OpenRead(safetyPath))
            using (var output = new FileStream(recoveryArchive, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                BackupPackage.Decrypt(input, output, recoveryPassword);
            ValidateArchive(recoveryArchive);
            if (databaseReplaced)
            {
                var manifest = new Dictionary<string, string>(StringComparer.Ordinal);
                var unpacked = Unpack(recoveryArchive, rollbackDatabase, manifest);

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

                ExtractPhotos(recoveryArchive, _photosFolder);
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
            TryDelete(recoveryArchive);
        }
    }

    private static void CopyPhotoTree(string sourceFolder, string destinationFolder)
    {
        Directory.CreateDirectory(destinationFolder);

        foreach (var source in Directory.EnumerateFiles(sourceFolder, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceFolder, source);
            var destination = Path.Combine(destinationFolder, relative);
            EnsureNoLinks(destination);
            Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? destinationFolder);
            File.Copy(source, destination, overwrite: true);
        }
    }

    private static void EnsureNoLinks(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current))
                && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Photo links/junctions are not supported during restore.");
    }

    private void TryDeleteDirectory(string path)
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
            // Cleanup is best effort, NOT secure erasure; a crash/lock can leave private plaintext staging.
            _log.Warn("Private backup staging cleanup was incomplete. Check local profile backup staging before sharing or disposing of this computer.");
        }
    }

    /// <summary>
    /// The copy of the shop database taken before a restore or (later) before a cloud pull
    /// (Spec 23.4). Public because the sync feature will need the same guarantee.
    /// </summary>
    public ServiceResult<string> SafetyCopy(string tag, User? user)
    {
        if (!CanUse(user)) return ServiceResult<string>.Fail("Current Backup permission is required.");
        var name = SafetyPrefix + TimeSource.Now.ToString(StampFormat, CultureInfo.InvariantCulture) + BackupPackage.Extension;
        var result = Create(user, includePhotos: true, destinationFile: Path.Combine(_backupsFolder, name));
        return result.Ok && result.Value is not null ? ServiceResult<string>.Success(result.Value.FullPath)
            : ServiceResult<string>.Fail(result.Error ?? "The encrypted safety copy could not be made.");
    }

    // ------------------------------ Helpers -------------------------------

    private bool CanUse(User? user)
    {
        try
        {
            using var connection = _db.Open();
            SnapshotReadAccess.Require(_db, connection, _permissions, user, Modules.Backup);
            return true;
        }
        catch (Exception) { return false; }
    }

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

            CopyEntry(database, target);

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

    private static void WriteArchiveFile(ZipArchive zip, string path, string entryName)
    {
        using var input = File.OpenRead(path);
        if (input.Length > MaxEntryBytes) throw new InvalidDataException("Backup entry too large.");
        using var output = zip.CreateEntry(entryName, CompressionLevel.Optimal).Open();
        CopyBounded(input, output, input.Length);
    }

    private static void CopyBounded(Stream input, Stream output, long expected)
    {
        var buffer = new byte[81920];
        long copied = 0;
        int read;
        while ((read = input.Read(buffer)) != 0)
        {
            copied = checked(copied + read);
            if (copied > expected) throw new InvalidDataException("Expanded entry exceeds its declared size.");
            output.Write(buffer, 0, read);
        }
        if (copied != expected) throw new InvalidDataException("Truncated archive entry.");
    }

    private static void CopyEntry(ZipArchiveEntry entry, string path)
    {
        using var input = entry.Open();
        using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        CopyBounded(input, output, entry.Length);
    }

    private static void PreflightArchive(string path)
    {
        using var file = File.OpenRead(path);
        if (file.Length < 22 || file.Length > BackupPackage.MaxPayloadBytes) throw new InvalidDataException("Archive size invalid.");
        var tail = new byte[(int)Math.Min(file.Length, 65557)];
        file.Position = file.Length - tail.Length;
        file.ReadExactly(tail);
        var end = -1;
        for (var i = tail.Length - 22; i >= 0; i--)
            if (BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(i, 4)) == 0x06054b50
                && i + 22 + BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(i + 20, 2)) == tail.Length)
            { end = i; break; }
        if (end < 0) throw new InvalidDataException("Archive directory missing.");
        var record = tail.AsSpan(end);
        if (BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(4, 4)) != 0)
            throw new InvalidDataException("Multi-disk archives are not supported.");
        ulong count = BinaryPrimitives.ReadUInt16LittleEndian(record.Slice(10, 2));
        ulong bytes = BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(12, 4));
        ulong offset = BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(16, 4));
        var directoryEnd = file.Length - tail.Length + end;
        if (count == ushort.MaxValue || bytes == uint.MaxValue || offset == uint.MaxValue)
        {
            if (directoryEnd < 76) throw new InvalidDataException("ZIP64 locator missing.");
            file.Position = directoryEnd - 20;
            var locator = new byte[20];
            file.ReadExactly(locator);
            var position = BinaryPrimitives.ReadUInt64LittleEndian(locator.AsSpan(8, 8));
            if (BinaryPrimitives.ReadUInt32LittleEndian(locator) != 0x07064b50
                || BinaryPrimitives.ReadUInt32LittleEndian(locator.AsSpan(4, 4)) != 0
                || BinaryPrimitives.ReadUInt32LittleEndian(locator.AsSpan(16, 4)) != 1
                || position > (ulong)(directoryEnd - 20 - 56)) throw new InvalidDataException("ZIP64 locator invalid.");
            file.Position = (long)position;
            var wide = new byte[56];
            file.ReadExactly(wide);
            if (BinaryPrimitives.ReadUInt32LittleEndian(wide) != 0x06064b50
                || BinaryPrimitives.ReadUInt64LittleEndian(wide.AsSpan(4, 8)) != 44
                || BinaryPrimitives.ReadUInt64LittleEndian(wide.AsSpan(16, 8)) != 0)
                throw new InvalidDataException("ZIP64 directory invalid.");
            count = BinaryPrimitives.ReadUInt64LittleEndian(wide.AsSpan(32, 8));
            if (count != BinaryPrimitives.ReadUInt64LittleEndian(wide.AsSpan(24, 8))) throw new InvalidDataException("ZIP64 count mismatch.");
            bytes = BinaryPrimitives.ReadUInt64LittleEndian(wide.AsSpan(40, 8));
            offset = BinaryPrimitives.ReadUInt64LittleEndian(wide.AsSpan(48, 8));
            directoryEnd = (long)position;
        }
        else if (count != BinaryPrimitives.ReadUInt16LittleEndian(record.Slice(8, 2)))
            throw new InvalidDataException("Archive count mismatch.");
        if (count == 0 || count > MaxArchiveEntries || bytes > 128UL * 1024 * 1024
            || offset > (ulong)directoryEnd || bytes != (ulong)directoryEnd - offset)
            throw new InvalidDataException("Archive directory bounds exceeded.");
    }

    // Central-directory bounds first, then actual bounded expansion BEFORE any SQLite processing.
    private static void ValidateArchive(string path)
    {
        PreflightArchive(path);
        using var zip = ZipFile.OpenRead(path);
        if (zip.Entries.Count > MaxArchiveEntries || zip.Entries.Count == 0) throw new InvalidDataException("Invalid archive count.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long expanded = 0;
        foreach (var entry in zip.Entries)
        {
            var name = entry.FullName;
            if (name.Length == 0 || name.Length > 1024 || name.Contains('\\') || name.StartsWith('/')
                || name.Any(c => char.IsControl(c) || "<>:\"|?*".Contains(c)) || !names.Add(name))
                throw new InvalidDataException("Unsafe archive path.");
            foreach (var part in name.TrimEnd('/').Split('/'))
            {
                var stem = part.Split('.')[0].ToUpperInvariant();
                if (part.Length == 0 || part.Length > 240 || part is "." or ".." || part.EndsWith('.') || part.EndsWith(' ')
                    || stem is "CON" or "PRN" or "AUX" or "NUL"
                    || (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && stem[3] is >= '0' and <= '9'))
                    throw new InvalidDataException("Unsafe archive path.");
            }
            var directory = name.EndsWith('/');
            if (name != DatabaseEntry && name != ManifestEntry && !name.StartsWith(PhotoFolderEntry, StringComparison.Ordinal))
                throw new InvalidDataException("Unexpected archive entry.");
            if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000) throw new InvalidDataException("Archive links are not supported.");
            if (entry.Length < 0 || entry.Length > MaxEntryBytes || (directory && entry.Length != 0)
                || (name == ManifestEntry && entry.Length > 65536)
                || entry.Length / Math.Max(1, entry.CompressedLength) > 10000)
                throw new InvalidDataException("Archive expansion limit exceeded.");
            expanded = checked(expanded + entry.Length);
            if (expanded > BackupPackage.MaxPayloadBytes) throw new InvalidDataException("Archive total too large.");
        }
        if (!names.Contains(DatabaseEntry)) throw new InvalidDataException("Database entry missing.");
        foreach (var entry in zip.Entries)
        {
            using var input = entry.Open();
            CopyBounded(input, Stream.Null, entry.Length);
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

            EnsureNoLinks(full);
            Directory.CreateDirectory(Path.GetDirectoryName(full) ?? root);
            CopyEntry(entry, full);
        }
    }

    private List<string> CollectPhotos()
    {
        if (!Directory.Exists(_photosFolder))
        {
            return new List<string>();
        }

        EnsureNoLinks(_photosFolder);
        var files = Directory.EnumerateFiles(_photosFolder, "*", new EnumerationOptions
            { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false })
            .Where(path => !Path.GetFileName(path).StartsWith('.'))
            .Take(MaxArchiveEntries + 1).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
        if (files.Count > MaxArchiveEntries) throw new InvalidDataException("Too many backup photos.");
        return files;
    }

    private string RelativeToPhotos(string path) =>
        Path.GetRelativePath(_photosFolder, path);

    /// <summary>
    /// Deletes the oldest of the program's own automatic backups, keeping <see cref="KeepCount"/>.
    /// Manual backups - anything without "-auto" - are never touched here.
    /// </summary>
    private static bool IsManagedAutomaticName(string name) => Regex.IsMatch(name,
        "^area11-backup-[0-9]{8}-[0-9]{6}-[0-9]{3}-auto(?:-[0-9]+|-[a-f0-9]{32})?\\.(?:a11bak|zip)$",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    private int TrimAutomatic(string? justPublished)
    {
        var keep = KeepCount;

        // List() is newest first (the name is an ISO stamp), so everything past "keep" is the oldest.
        var automaticFiles = List()
            .Where(file => file.IsAutomatic && file.IsEncrypted)
            .ToList();

        var removed = 0;

        // Clock rollback / same-stamp suffix sorting must not delete the file just reported successful.
        var older = automaticFiles.Where(file => !string.Equals(file.FullPath, justPublished, StringComparison.OrdinalIgnoreCase));
        foreach (var file in older.Skip(Math.Max(0, keep - (justPublished is null ? 0 : 1))))
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
            // Best-effort cleanup only; deletion does not guarantee secure erasure.
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

        if (bare.EndsWith(BackupPackage.Extension, StringComparison.OrdinalIgnoreCase)) bare = bare[..^BackupPackage.Extension.Length];
        else if (bare.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) bare = bare[..^4];

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
