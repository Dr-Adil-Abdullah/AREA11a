using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Text;
using Area11.Core.Data;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>
/// Photos of medicines, customers and suppliers (F-41, Spec 17.1 &amp; D-21).
///
/// **The file lives in `data/photos`; the database keeps only its name** (rule U-19). That is why a
/// backup can be restored on another computer and still work: a path from the old machine would
/// point at nothing, while a name is found in the folder beside the restored database.
///
/// Three promises, and each is a test:
///   1. **a photo is copied, not moved** - the picture the owner picked stays where he had it;
///   2. **nothing is deleted** (D-00b) - "put away" sets `active = 0`, hides it from the gallery and
///      keeps the file on the disk and the name in the black box;
///   3. **the folder is never filled by accident** - a file bigger than <see cref="MaxBytes"/>, or of
///      a kind the program cannot show, is refused **before** anything is copied.
///
/// The camera: the window offers the Windows Camera app and then adds the picture it saved. A live
/// preview inside this program would need the Windows runtime component, which cannot be verified
/// on the build machine - and a camera that works in the shop beats one that only looks finished
/// (D-58).
/// </summary>
public sealed class PhotoService
{
    /// <summary>A phone photo is a few megabytes; anything past this is probably the wrong file.</summary>
    public const long MaxBytes = 12L * 1024 * 1024;

    /// <summary>The owner asked for two or three; six is room to spare and still a short list.</summary>
    public const int MaxPerRecord = 6;

    // No .webp: Windows' own picture reader (WPF) cannot decode it without an extra codec pack, and
    // a photo the shop can add but not look at is worse than one it cannot add at all.
    private static readonly string[] Allowed =
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif"
    };

    private readonly Database _db;
    private readonly AuditService _audit;
    private readonly PermissionService _permissions;
    private readonly LogService _log;

    /// <param name="photosFolder">Tests pass their own folder; the shop uses %ProgramData%\Area11\photos.</param>
    public PhotoService(
        Database db,
        AuditService audit,
        PermissionService permissions,
        LogService log,
        string? photosFolder = null)
    {
        _db = db;
        _audit = audit;
        _permissions = permissions;
        _log = log;

        PhotosFolder = string.IsNullOrWhiteSpace(photosFolder) ? AppPaths.PhotosFolder : photosFolder;
        Directory.CreateDirectory(PhotosFolder);
    }

    public string PhotosFolder { get; }

    /// <summary>
    /// Portion 17: a stored file name becomes the full path the screen binds - or null when there
    /// is no name or the file is gone. The one formula every thumbnail list delegates to (the
    /// Counter and the Products list used to carry private copies of these three lines).
    /// </summary>
    public string? CoverPath(string? fileName)
    {
        // Stored names can come from imported/restored data. Use the same boundary for
        // thumbnails, bulk covers, galleries, cover selection and full-size reads.
        if (string.IsNullOrWhiteSpace(fileName) || fileName != fileName.Trim()
            || Path.IsPathRooted(fileName) || fileName.Contains("..", StringComparison.Ordinal)
            || fileName.IndexOfAny(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|', '\0' }) >= 0)
        {
            return null;
        }

        var path = Path.Combine(PhotosFolder, fileName);

        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Portion 17: the cover of many records in one query - the same cover rule the product list
    /// uses (active, first by sort order), resolved to full paths, missing files skipped. Plain
    /// dictionary, never a failure: thumbnails are decoration, and a list must never fail because
    /// of them. Unknown entity, no permission, no ids - all answer empty, and the reason is logged.
    ///
    /// The reviewed privacy rule (P17): product covers are open to whoever sees the list (the
    /// Counter has always shown them); customer, supplier and staff covers keep their module's
    /// gate, exactly like their galleries.
    /// </summary>
    public Dictionary<int, string> CoversFor(User? user, string entity, IEnumerable<int> ids)
    {
        var covers = new Dictionary<int, string>();
        var key = PhotoEntity.Clean(entity);

        if (!PhotoEntity.IsKnown(key))
        {
            return covers;
        }

        if (key != PhotoEntity.Product && !_permissions.Can(user, PhotoEntity.ModuleFor(key)))
        {
            return covers;
        }

        var wanted = ids.Where(id => id > 0).Distinct().ToList();

        if (wanted.Count == 0)
        {
            return covers;
        }

        try
        {
            using var connection = _db.Open();

            // One row per record: the cover subquery is the same rule as the product list's.
            // Expando carries a number of ids no anonymous object can spell (the P10 precedent).
            var names = new List<string>();
            IDictionary<string, object?> parameters = new ExpandoObject();
            for (var index = 0; index < wanted.Count; index++)
            {
                var name = "id" + index.ToString(CultureInfo.InvariantCulture);
                names.Add("@" + name);
                parameters[name] = wanted[index];
            }

            parameters["entity"] = key;

            // StringBuilder, not one literal: the id list is as long as the page (P10 precedent).
            var sql = new StringBuilder(@"
SELECT DISTINCT entity_id AS entityId, (SELECT ph.file_name FROM photos ph
WHERE ph.entity = @entity AND ph.entity_id = photos.entity_id AND IFNULL(ph.active, 1) = 1
ORDER BY ph.sort_order, ph.id LIMIT 1) AS fileName
FROM photos WHERE entity = @entity AND entity_id IN (");
            sql.Append(string.Join(", ", names));
            sql.Append(");");

            var rows = _db.Query<CoverRow>(connection, sql.ToString(), parameters);

            foreach (var row in rows)
            {
                var path = CoverPath(row.FileName);

                if (path is not null)
                {
                    covers[row.EntityId] = path;
                }
            }
        }
        catch (Exception exception)
        {
            _log.Error("The covers could not be listed.", exception);
        }

        return covers;
    }

    /// <summary>The photos of one record, in the order they were added. Hidden ones only if asked.</summary>
    public ServiceResult<List<PhotoItem>> List(
        User? user,
        string entity,
        int entityId,
        bool includeHidden = false)
    {
        if (!PhotoEntity.IsKnown(entity))
        {
            return ServiceResult<List<PhotoItem>>.Fail("That kind of record has no photos.");
        }

        var key = PhotoEntity.Clean(entity);

        if (!_permissions.Can(user, PhotoEntity.ModuleFor(key)))
        {
            return ServiceResult<List<PhotoItem>>.Fail("You do not have permission to see these photos.");
        }

        try
        {
            using var connection = _db.Open();

            var rows = _db.Query<PhotoItem>(connection, @"
SELECT id AS id, entity AS entity, entity_id AS entityId, file_name AS fileName,
       sort_order AS sortOrder, created_at AS createdAt, IFNULL(active, 1) AS isActive
FROM photos
WHERE entity = @entity AND entity_id = @entityId
  AND (@includeHidden = 1 OR IFNULL(active, 1) = 1)
ORDER BY sort_order, id;",
                new { entity = key, entityId, includeHidden = includeHidden ? 1 : 0 });

            foreach (var row in rows)
            {
                row.FullPath = CoverPath(row.FileName) ?? string.Empty;
                row.Exists = File.Exists(row.FullPath);
            }

            return ServiceResult<List<PhotoItem>>.Success(rows);
        }
        catch (Exception exception)
        {
            _log.Error($"The photos of {key} {entityId} could not be listed.", exception);
            return ServiceResult<List<PhotoItem>>.Fail(
                "The photos could not be loaded. " + exception.Message);
        }
    }

    /// <summary>
    /// Copies a picture into the shop's photo folder and records its name. The original stays where
    /// the owner had it; a failure after the copy removes the copy, so no orphan file is left behind.
    /// </summary>
    public ServiceResult<PhotoItem> Add(User? user, string entity, int entityId, string? sourcePath)
    {
        if (!PhotoEntity.IsKnown(entity))
        {
            return ServiceResult<PhotoItem>.Fail("That kind of record has no photos.");
        }

        var key = PhotoEntity.Clean(entity);

        if (!_permissions.Can(user, PhotoEntity.ModuleFor(key)))
        {
            return ServiceResult<PhotoItem>.Fail("You do not have permission to add a photo.");
        }

        var source = (sourcePath ?? string.Empty).Trim();

        var sourceError = ValidateSource(sourcePath);

        if (sourceError is not null)
        {
            return ServiceResult<PhotoItem>.Fail(sourceError);
        }

        string? copiedPath = null;

        try
        {
            return _db.Tx(connection =>
                AddInTransaction(connection, user, key, entityId, source, out copiedPath));
        }
        catch (Exception exception)
        {
            // The copy is already on the disk; a row that never happened must not leave it there.
            DiscardCopy(copiedPath);

            _log.Error($"A photo of {key} {entityId} could not be added.", exception);
            return ServiceResult<PhotoItem>.Fail("The photo could not be added. " + exception.Message);
        }
    }

    /// <summary>File checks only: the picture exists, is a picture, is not empty, fits the limit.</summary>
    internal string? ValidateSource(string? sourcePath)
    {
        var source = (sourcePath ?? string.Empty).Trim();

        if (source.Length == 0 || !File.Exists(source))
        {
            return "That file is not there any more. Choose it again.";
        }

        var extension = Path.GetExtension(source).ToLowerInvariant();

        if (!Allowed.Contains(extension))
        {
            return "Only a picture can be added (" + string.Join(", ", Allowed) + ").";
        }

        long size;

        try
        {
            size = new FileInfo(source).Length;
        }
        catch (Exception exception)
        {
            return "The file could not be read. " + exception.Message;
        }

        if (size <= 0)
        {
            return "That file is empty.";
        }

        if (size > MaxBytes)
        {
            return "That picture is " + (size / (1024 * 1024)) + " MB - the limit is "
                + (MaxBytes / (1024 * 1024)) + " MB. Take it again at a smaller size.";
        }

        return null;
    }

    // Caller owns the transaction: the staged photo of a new record shares the creator's commit,
    // so a failed creation rolls the photo row back with the parent. The copied file is reported
    // through copiedPath; the caller must pass it to DiscardCopy when the transaction fails,
    // because a file is not rolled back by SQLite.
    internal ServiceResult<PhotoItem> AddInTransaction(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        User? user, string key, int entityId, string source, out string? copiedPath)
    {
        copiedPath = null;

        if (!_permissions.Can(user, PhotoEntity.ModuleFor(key)))
        {
            return ServiceResult<PhotoItem>.Fail("You do not have permission to add a photo.");
        }

        var recordExists = RecordExists(connection, key, entityId);

        if (!recordExists)
        {
            return ServiceResult<PhotoItem>.Fail(
                PhotoEntity.LabelFor(key) + " is not in the list any more, so a photo cannot be attached.");
        }

        var activeCount = Convert.ToInt32(_db.Scalar(connection, @"
SELECT COUNT(1) FROM photos
WHERE entity = @entity AND entity_id = @entityId AND IFNULL(active, 1) = 1;",
            new { entity = key, entityId }) ?? 0);

        if (activeCount >= MaxPerRecord)
        {
            return ServiceResult<PhotoItem>.Fail(
                "This " + PhotoEntity.LabelFor(key).ToLowerInvariant() + " already has "
                + MaxPerRecord + " photos. Put one away first.");
        }

        var nextOrder = Convert.ToInt32(_db.Scalar(connection, @"
SELECT IFNULL(MAX(sort_order), 0) + 1 FROM photos WHERE entity = @entity AND entity_id = @entityId;",
            new { entity = key, entityId }) ?? 1);

        // A stamped name can never collide with an older file, even when two people add a
        // photo in the same minute on a second counter.
        var extension = Path.GetExtension(source).ToLowerInvariant();
        var name = key + "-" + entityId + "-"
                   + TimeSource.Now.ToString("yyyyMMdd-HHmmss-fff", System.Globalization.CultureInfo.InvariantCulture)
                   + extension;

        var destination = Path.Combine(PhotosFolder, name);

        if (File.Exists(destination))
        {
            // Same millisecond twice: one more suffix, still readable.
            name = Path.GetFileNameWithoutExtension(name) + "-b" + extension;
            destination = Path.Combine(PhotosFolder, name);
        }

        File.Copy(source, destination, overwrite: false);
        copiedPath = destination;

        _db.Execute(connection, @"
INSERT INTO photos(entity, entity_id, file_name, sort_order, active, created_at)
VALUES(@entity, @entityId, @fileName, @sortOrder, 1, @at);",
            new
            {
                entity = key,
                entityId,
                fileName = name,
                sortOrder = nextOrder,
                at = Clock.Now()
            });

        var photoId = (int)_db.NextId(connection);

        _audit.Record(connection, user, PhotoEntity.ModuleFor(key), "photo_add",
            "photos", photoId.ToString(), newValue: name,
            details: PhotoEntity.LabelFor(key) + " " + entityId);

        return ServiceResult<PhotoItem>.Success(new PhotoItem
        {
            Id = photoId,
            Entity = key,
            EntityId = entityId,
            FileName = name,
            SortOrder = nextOrder,
            IsActive = true,
            CreatedAt = Clock.Now(),
            FullPath = destination,
            Exists = true
        });
    }

    /// <summary>Removes a staged copy after a rolled-back transaction. Never touches the gallery.</summary>
    internal void DiscardCopy(string? path)
    {
        if (!string.IsNullOrEmpty(path))
        {
            TryDelete(path);
        }
    }

    /// <summary>
    /// Takes a photo out of the gallery **without deleting it** (D-00b): the row stays, the file
    /// stays, `active` becomes 0 and the black box says who did it.
    /// </summary>
    public ServiceResult Hide(User? user, int photoId)
    {
        return ChangeActive(user, photoId, false);
    }

    /// <summary>
    /// Makes an active product photo the first thumbnail without deleting or copying any file.
    /// All order changes and the audit row share the same transaction. Hidden photos remain hidden.
    /// </summary>
    public ServiceResult SetProductCover(User? user, int photoId)
    {
        if (!_permissions.Can(user, Modules.Products))
        {
            return ServiceResult.Fail("You do not have permission to change product photos.");
        }

        try
        {
            return _db.Tx(connection =>
            {
                var selected = _db.QuerySingle<PhotoItem>(connection, @"
SELECT id, entity, entity_id AS entityId, file_name AS fileName,
       sort_order AS sortOrder, IFNULL(active, 1) AS isActive
FROM photos WHERE id = @id;", new { id = photoId });
                if (selected is null || selected.Entity != PhotoEntity.Product || !selected.IsActive)
                {
                    return ServiceResult.Fail("Choose an active product photo.");
                }
                if (CoverPath(selected.FileName) is null)
                {
                    return ServiceResult.Fail("That photo file is missing; choose an available photo.");
                }

                var rows = _db.Query<PhotoItem>(connection, @"
SELECT id, sort_order AS sortOrder, IFNULL(active, 1) AS isActive FROM photos
WHERE entity = @entity AND entity_id = @entityId ORDER BY sort_order, id;",
                    new { entity = PhotoEntity.Product, entityId = selected.EntityId });
                var firstActive = rows.FirstOrDefault(row => row.IsActive);
                if (firstActive?.Id == selected.Id)
                {
                    return ServiceResult.Success();
                }

                // Put the selected photo before every row, including hidden photos: restoring an
                // older hidden item must not silently steal the cover. Keep the rest in their order.
                var ordered = rows.OrderBy(row => row.Id == selected.Id ? 0 : 1).ToList();
                for (var index = 0; index < ordered.Count; index++)
                {
                    _db.Execute(connection, "UPDATE photos SET sort_order = @sort WHERE id = @id;",
                        new { sort = index + 1, id = ordered[index].Id });
                }
                _audit.Record(connection, user, Modules.Products, "photo_cover_changed", "photos",
                    selected.Id.ToString(), oldValue: firstActive?.Id.ToString(),
                    newValue: selected.Id.ToString(), details: "Product " + selected.EntityId);
                return ServiceResult.Success();
            });
        }
        catch (Exception exception)
        {
            _log.Error($"The product photo {photoId} could not be made the cover.", exception);
            return ServiceResult.Fail("The cover photo could not be changed.");
        }
    }

    /// <summary>Puts a hidden photo back in the gallery - the same row, the same file.</summary>
    public ServiceResult Restore(User? user, int photoId)
    {
        return ChangeActive(user, photoId, true);
    }

    /// <summary>
    /// Only the signed-in person's own avatar, not an arbitrary user's gallery. Staff administrators
    /// manage user photos through List/Add/Hide; ordinary staff cannot enumerate those galleries.
    /// An inactive account and a missing or unsafe file both fall back to initials in the shell.
    /// </summary>
    public ServiceResult<string> OwnAvatar(User? user)
    {
        if (user is null || user.Id <= 0)
        {
            return ServiceResult<string>.Fail("Sign in to see your photo.");
        }
        try
        {
            using var connection = _db.Open();
            var file = _db.QuerySingle<string>(connection, @"
SELECT p.file_name FROM photos p JOIN users u ON u.id = p.entity_id AND u.active = 1
WHERE p.entity = 'user' AND p.entity_id = @id AND IFNULL(p.active, 1) = 1
ORDER BY p.sort_order, p.id LIMIT 1;", new { id = user.Id });
            var path = CoverPath(file);
            if (path is null)
                return ServiceResult<string>.Fail("No available profile photo.");
            return File.Exists(path) ? ServiceResult<string>.Success(path)
                : ServiceResult<string>.Fail("No available profile photo.");
        }
        catch (Exception exception)
        {
            _log.Error("The signed-in person's photo could not be loaded.", exception);
            return ServiceResult<string>.Fail("The profile photo could not be loaded.");
        }
    }

    /// <summary>The file to show or print. Refuses a path that escaped the photo folder.</summary>
    public ServiceResult<string> PathOf(User? user, int photoId)
    {
        var found = Read(user, photoId);

        if (!found.Ok || found.Value is null)
        {
            return ServiceResult<string>.Fail(found.Error ?? "That photo is not there any more.");
        }

        return ServiceResult<string>.Success(found.Value.FullPath);
    }

    /// <summary>The folder the camera app and the file picker should start in.</summary>
    public static string CameraHint =>
        "The Windows Camera app saves the picture in Pictures\\Camera Roll. Take it, then press "
        + "\"Add a photo\" and pick the newest file. The picture is copied into the shop's folder, so "
        + "deleting it from Pictures later does not lose it.";

    // ------------------------------------------------------------------ helpers

    private ServiceResult ChangeActive(User? user, int photoId, bool active)
    {
        try
        {
            return _db.Tx(connection =>
            {
                var row = _db.QuerySingle<PhotoItem>(connection, @"
SELECT id AS id, entity AS entity, entity_id AS entityId, file_name AS fileName,
       sort_order AS sortOrder, created_at AS createdAt, IFNULL(active, 1) AS isActive
FROM photos WHERE id = @id;", new { id = photoId });

                if (row is null)
                {
                    return ServiceResult.Fail("That photo is not in the list any more.");
                }

                if (!_permissions.Can(user, PhotoEntity.ModuleFor(row.Entity)))
                {
                    return ServiceResult.Fail("You do not have permission to change these photos.");
                }

                if (row.IsActive == active)
                {
                    return ServiceResult.Success();
                }

                _db.Execute(connection, "UPDATE photos SET active = @active WHERE id = @id;",
                    new { active = active ? 1 : 0, id = photoId });

                _audit.Record(connection, user, PhotoEntity.ModuleFor(row.Entity),
                    active ? "photo_restore" : "photo_hide", "photos", photoId.ToString(),
                    oldValue: row.IsActive ? "visible" : "put away",
                    newValue: active ? "visible" : "put away",
                    details: row.FileName);

                return ServiceResult.Success();
            });
        }
        catch (Exception exception)
        {
            _log.Error($"The photo {photoId} could not be changed.", exception);
            return ServiceResult.Fail("The photo could not be changed. " + exception.Message);
        }
    }

    private ServiceResult<PhotoItem> Read(User? user, int photoId)
    {
        try
        {
            using var connection = _db.Open();

            var row = _db.QuerySingle<PhotoItem>(connection, @"
SELECT id AS id, entity AS entity, entity_id AS entityId, file_name AS fileName,
       sort_order AS sortOrder, created_at AS createdAt, IFNULL(active, 1) AS isActive
FROM photos WHERE id = @id;", new { id = photoId });

            if (row is null)
            {
                return ServiceResult<PhotoItem>.Fail("That photo is not in the list any more.");
            }

            if (!_permissions.Can(user, PhotoEntity.ModuleFor(row.Entity)))
            {
                return ServiceResult<PhotoItem>.Fail("You do not have permission to see this photo.");
            }

            var path = CoverPath(row.FileName);
            if (path is null)
                return ServiceResult<PhotoItem>.Fail("The picture is missing or outside the photos folder.");
            row.FullPath = path;
            row.Exists = true;

            return ServiceResult<PhotoItem>.Success(row);
        }
        catch (Exception exception)
        {
            _log.Error($"The photo {photoId} could not be read.", exception);
            return ServiceResult<PhotoItem>.Fail("The photo could not be read. " + exception.Message);
        }
    }

    /// <summary>True when the record the photo belongs to still exists and is active.</summary>
    private bool RecordExists(Microsoft.Data.Sqlite.SqliteConnection connection, string entity, int entityId)
    {
        var table = entity switch
        {
            PhotoEntity.Product => "products",
            PhotoEntity.Customer => "customers",
            PhotoEntity.Supplier => "suppliers",
            PhotoEntity.User => "users",
            _ => string.Empty
        };

        if (table.Length == 0)
        {
            return false;
        }

        // The table name is chosen from this file's own list, never from the caller's text, so the
        // string is safe to place in the SQL; the id stays a parameter.
        return Convert.ToInt32(_db.Scalar(connection,
            "SELECT COUNT(1) FROM " + table + " WHERE id = @id AND active = 1;",
            new { id = entityId }) ?? 0) > 0;
    }

    private void TryDelete(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Nothing else can be done; the log line below names the file so it can be removed by hand.
            _log.Info("A copied photo could not be removed after a failed save: " + path);
        }
    }
}

/// <summary>One cover answer: which record, and the stored file name (null when it has no active photo).</summary>
internal sealed class CoverRow
{
    public int EntityId { get; set; }

    public string? FileName { get; set; }
}
