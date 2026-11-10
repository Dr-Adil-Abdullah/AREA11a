using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Desktop;

/// <summary>
/// The owner-selected WINDOW icon. Files live in the existing photos backup folder, while the
/// setting stores only a generated name. The executable and pre-existing Windows shortcuts retain
/// their compiled icon; neither is rewritten. Missing/bad files fall back to the shipped icon.
/// </summary>
internal static class IconAssets
{
    private const string Key = "appearance.iconFileName";
    private const int MaximumBytes = 2 * 1024 * 1024;

    public static ServiceResult Choose(AppServices services, User? owner, string? source)
    {
        if (owner?.IsOwner != true) return ServiceResult.Fail("Only the owner can change the application icon.");
        if (string.IsNullOrWhiteSpace(source) || !File.Exists(source)) return ServiceResult.Fail("Choose an icon file.");
        string? destination = null;
        var saved = false;
        var created = false;
        try
        {
            var info = new FileInfo(source);
            if (!string.Equals(info.Extension, ".ico", StringComparison.OrdinalIgnoreCase)
                || info.Length < 6 || info.Length > MaximumBytes)
                return ServiceResult.Fail("Choose a Windows .ico file no larger than 2 MB.");
            var bytes = File.ReadAllBytes(source);
            if (bytes.Length < 6 || bytes.Length > MaximumBytes || bytes[0] != 0 || bytes[1] != 0
                || bytes[2] != 1 || bytes[3] != 0)
                return ServiceResult.Fail("That is not a valid Windows icon.");
            Decode(bytes); // Refuse undecodable images before changing the setting or copying a file.
            var name = "area11-icon-" + Guid.NewGuid().ToString("N") + ".ico";
            destination = Path.Combine(services.Photos.PhotosFolder, name);
            using (var stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                stream.Write(bytes, 0, bytes.Length);
            }
            var result = services.Settings.Set(Key, name, owner);
            if (!result.Ok)
            {
                File.Delete(destination);
                return result;
            }
            saved = true;
            Apply(services);
            return ServiceResult.Success();
        }
        catch (Exception exception)
        {
            // Only a newly-created file is ours to clean up. Never delete the previous icon.
            if (created && !saved && destination is not null)
            {
                try { File.Delete(destination); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            services.Log.Error("The application icon could not be changed.", exception);
            return ServiceResult.Fail(saved
                ? "The icon was saved but could not be displayed. Restart Area11 or restore the default icon."
                : "The icon could not be read or saved. Choose another .ico file.");
        }
    }

    public static ServiceResult Reset(AppServices services, User? owner)
    {
        if (owner?.IsOwner != true) return ServiceResult.Fail("Only the owner can change the application icon.");
        var result = services.Settings.Set(Key, string.Empty, owner);
        if (result.Ok) Apply(services);
        return result;
    }

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
            icon = Decode(File.ReadAllBytes(path));
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
