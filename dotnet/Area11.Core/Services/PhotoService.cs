using System.IO;
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
                row.FullPath = Path.Combine(PhotosFolder, row.FileName);
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

        if (source.Length == 0 || !File.Exists(source))
        {
            return ServiceResult<PhotoItem>.Fail("That file is not there any more. Choose it again.");
        }

        var extension = Path.GetExtension(source).ToLowerInvariant();

        if (!Allowed.Contains(extension))
        {
            return ServiceResult<PhotoItem>.Fail(
                "Only a picture can be added (" + string.Join(", ", Allowed) + ").");
        }

        long size;

        try
        {
            size = new FileInfo(source).Length;
        }
        catch (Exception exception)
        {
            return ServiceResult<PhotoItem>.Fail("The file could not be read. " + exception.Message);
        }

        if (size <= 0)
        {
            return ServiceResult<PhotoItem>.Fail("That file is empty.");
        }

        if (size > MaxBytes)
        {
            return ServiceResult<PhotoItem>.Fail(
                "That picture is " + (size / (1024 * 1024)) + " MB - the limit is "
                + (MaxBytes / (1024 * 1024)) + " MB. Take it again at a smaller size.");
        }

        string destination = string.Empty;

        try
        {
            return _db.Tx(connection =>
            {
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
                var name = key + "-" + entityId + "-"
                           + TimeSource.Now.ToString("yyyyMMdd-HHmmss-fff", System.Globalization.CultureInfo.InvariantCulture)
                           + extension;

                destination = Path.Combine(PhotosFolder, name);

                if (File.Exists(destination))
                {
                    // Same millisecond twice: one more suffix, still readable.
                    name = Path.GetFileNameWithoutExtension(name) + "-b" + extension;
                    destination = Path.Combine(PhotosFolder, name);
                }

                File.Copy(source, destination, overwrite: false);

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
            });
        }
        catch (Exception exception)
        {
            // The copy is already on the disk; a row that never happened must not leave it there.
            TryDelete(destination);

            _log.Error($"A photo of {key} {entityId} could not be added.", exception);
            return ServiceResult<PhotoItem>.Fail("The photo could not be added. " + exception.Message);
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

    /// <summary>Puts a hidden photo back in the gallery - the same row, the same file.</summary>
    public ServiceResult Restore(User? user, int photoId)
    {
        return ChangeActive(user, photoId, true);
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

            // The database is not a trusted input at this point: a name with a separator or ".." in it
            // would point outside the shop's photo folder. Such a row is treated as a missing picture.
            if (row.FileName.IndexOfAny(new[] { '/', '\\' }) >= 0
                || row.FileName.Contains("..", StringComparison.Ordinal))
            {
                return ServiceResult<PhotoItem>.Fail(
                    "The picture is missing from the photos folder (" + row.FileName + ").");
            }

            row.FullPath = Path.Combine(PhotosFolder, row.FileName);
            row.Exists = File.Exists(row.FullPath);

            if (!row.Exists)
            {
                return ServiceResult<PhotoItem>.Fail(
                    "The picture is missing from the photos folder (" + row.FileName + ").");
            }

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
