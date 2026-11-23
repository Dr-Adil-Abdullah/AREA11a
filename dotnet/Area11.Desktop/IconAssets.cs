using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;

namespace Area11.Desktop;

/// <summary>
/// The owner-selected WINDOW icon. Files live in the existing photos backup folder, while the
/// setting stores only a generated name. The executable and pre-existing Windows shortcuts retain
/// their compiled icon; neither is rewritten. Missing/bad files fall back to the shipped icon.
/// </summary>
internal static class IconAssets
{
    private const string Key = IconAssetService.Key;
    private const int MaximumBytes = IconAssetService.MaximumBytes;

    public static ServiceResult Choose(AppServices services, User? owner, string? source, SettingsEditForm expected)
    {
        if (owner?.IsOwner != true) return Refuse("Only the owner can change the application icon.");
        if (string.IsNullOrWhiteSpace(source)
            || !string.Equals(Path.GetExtension(source), ".ico", StringComparison.OrdinalIgnoreCase))
            return Refuse("Choose a Windows .ico file no larger than 2 MB.");
        byte[] bytes;
        try
        {
            bytes = ReadBounded(source);
            Decode(bytes); // WPF codec validation BEFORE any destination or Settings write.
        }
        catch (Exception exception)
        {
            services.Log.Error("The application icon could not be read.", exception);
            return Refuse("Choose a readable Windows .ico file no larger than 2 MB.");
        }
        var result = new IconAssetService(services.Settings, services.Log, services.Photos.PhotosFolder)
            .Choose(owner, bytes, expected);
        return ApplySaved(services, result);
    }

    public static ServiceResult Reset(AppServices services, User? owner, SettingsEditForm expected)
    {
        var result = new IconAssetService(services.Settings, services.Log, services.Photos.PhotosFolder)
            .Reset(owner, expected);
        return ApplySaved(services, result);
    }

    private static ServiceResult ApplySaved(AppServices services, ServiceResult result)
    {
        if (!result.Ok) return result;
        try { Apply(services); }
        catch (Exception exception)
        {
            services.Log.Error("SAVED icon could not be displayed.", exception);
            // Commit already acknowledged;keep the exact token,never advise another write.
            return new ServiceResult { Ok = true, SettingsEdit = result.SettingsEdit,
                Warnings = new[] { "SAVED. The icon could not be displayed. Reopen Area11 and verify the current icon." } };
        }
        return result;
    }

    // Limit the read itself,not merely a FileInfo length checked before a growing file is read.
    private static byte[] ReadBounded(string path)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var buffer = new MemoryStream();
        var chunk = new byte[64 * 1024];
        int read;
        while ((read = input.Read(chunk, 0, (int)Math.Min(chunk.Length, MaximumBytes + 1L - buffer.Length))) != 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaximumBytes) throw new InvalidDataException("Icon exceeds 2 MB.");
        }
        if (buffer.Length < 6) throw new InvalidDataException("Icon is empty or incomplete.");
        return buffer.ToArray();
    }

    private static ServiceResult Refuse(string error) => new() { Error = error, NoCommitConfirmed = true };

    public static void Apply(AppServices services)
    {
        if (Application.Current is not { } app) return;
        ImageSource icon;
        var name = services.Settings.GetString(Key);
        try
        {
            // Treat database content as untrusted. Only a generated basename can be loaded.
            if (name.Length != 48 || !name.StartsWith("area11-icon-", StringComparison.Ordinal)
                || !name.EndsWith(".ico", StringComparison.Ordinal)
                || !Guid.TryParseExact(name.Substring(12, 32), "N", out _))
                throw new InvalidDataException("No valid custom icon is selected.");
            var path = Path.Combine(services.Photos.PhotosFolder, name);
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaximumBytes || info.Length < 6)
                throw new InvalidDataException("The custom icon file is unavailable.");
            icon = Decode(ReadBounded(path));
        }
        catch (Exception)
        {
            icon = new BitmapImage(new Uri("pack://application:,,,/Resources/area11.ico", UriKind.Absolute));
        }
        app.Resources["Area11WindowIcon"] = icon;
    }

    private static BitmapFrame Decode(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        if (decoder is not IconBitmapDecoder || decoder.Frames.Count == 0)
            throw new InvalidDataException("The file is not an icon.");
        var frame = decoder.Frames.OrderByDescending(item => item.PixelWidth * item.PixelHeight).First();
        frame.Freeze();
        return frame;
    }
}
