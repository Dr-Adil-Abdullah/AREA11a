using System.IO;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Core.Services;

/// <summary>Exclusive icon file + checked Settings pointer. Not a cross-resource atomic commit.
/// WPF validates the codec before calling Choose;Core bounds and checks the ICO header too.</summary>
public sealed class IconAssetService
{
    public const string Key = "appearance.iconFileName";
    public const int MaximumBytes = 2 * 1024 * 1024;
    private readonly SettingsService _settings;
    private readonly LogService _log;
    private readonly string _folder;
    private readonly IconFileStore _files;

    public IconAssetService(SettingsService settings, LogService log, string photosFolder, IconFileStore? files = null)
    { _settings = settings; _log = log; _folder = photosFolder; _files = files ?? new IconFileStore(); }

    public ServiceResult Choose(User? owner, byte[]? bytes, SettingsEditForm? expected)
    {
        var access = _settings.CheckIconEdit(owner, expected);
        if (!access.Ok) return access;
        if (bytes is null || bytes.Length < 6 || bytes.Length > MaximumBytes)
            return Refuse("Choose a Windows .ico file no larger than 2 MB.");
        var content = (byte[])bytes.Clone(); // Caller mutations cannot change the validated/written bytes.
        if (content[0] != 0 || content[1] != 0 || content[2] != 1 || content[3] != 0
            || (content[4] == 0 && content[5] == 0))
            return Refuse("That is not a valid Windows icon.");
        string? destination = null;
        var created = false;
        var writerStarted = false;
        try
        {
            Directory.CreateDirectory(_folder);
            var name = "area11-icon-" + Guid.NewGuid().ToString("N") + ".ico";
            destination = Path.Combine(_folder, name);
            using (var output = _files.CreateNew(destination))
            {
                created = true; // Ownership BEFORE the first Write/Flush/Dispose can fail.
                output.Write(content, 0, content.Length);
                _files.Flush(output);
            }
            writerStarted = true; // A throwing/lost response is not proof of rollback.
            var result = _settings.Set(Key, name, owner, expected);
            if (!result.Ok)
            {
                if (result.NoCommitConfirmed) Discard(destination);
                else _log.Info("Unconfirmed icon save;retain copied file for verification: " + destination);
            }
            return result; // Preserve exact success handoff and observed no-commit verdict.
        }
        catch (Exception exception)
        {
            if (created && !writerStarted && destination is not null) Discard(destination);
            else if (created) _log.Info("Unconfirmed icon save;retain copied file for verification: " + destination);
            _log.Error("Icon copy/save did not finish.", exception);
            return new ServiceResult { Error = writerStarted
                ? "Icon saving is unconfirmed. Refresh and verify before retrying;the copied file was retained."
                : "The icon file could not be copied. No icon setting was changed.", NoCommitConfirmed = !writerStarted };
        }
    }

    public ServiceResult Reset(User? owner, SettingsEditForm? expected)
    {
        var access = _settings.CheckIconEdit(owner, expected);
        if (!access.Ok) return access;
        // Historical/replaced files remain in photos/backups. Never delete an old selected icon.
        return _settings.Set(Key, string.Empty, owner, expected);
    }

    private void Discard(string ownedPath)
    {
        try { _files.Delete(ownedPath); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { _log.Info("Uncommitted owned icon could not be removed;verify before cleanup: " + ownedPath); }
    }

    private static ServiceResult Refuse(string error) => new() { Error = error, NoCommitConfirmed = true };
}

/// <summary>Narrow file boundary for isolated fault tests. CreateNew transfers ownership only on return;
/// production never opens an existing file for writing. Unknown/old assets must not be deleted.</summary>
public class IconFileStore
{
    public virtual Stream CreateNew(string path) => new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
    public virtual void Flush(Stream output)
    {
        if (output is FileStream file) file.Flush(flushToDisk: true);
        else output.Flush();
    }
    public virtual void Delete(string path) => File.Delete(path);
}
