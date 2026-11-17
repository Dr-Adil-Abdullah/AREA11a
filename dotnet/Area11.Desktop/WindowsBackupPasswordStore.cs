using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Area11.Core.Services;

namespace Area11.Desktop;

/// <summary>
/// Current-Windows-user DPAPI, no machine-wide/shared key. Per-shop credential stays in the user
/// profile, not ProgramData DB/settings or any backup payload. Same OS account/admin is trusted.
/// Another Windows account/new PC needs explicit setup and the original package password.
/// </summary>
public sealed class WindowsBackupPasswordStore : IBackupPasswordStore
{
    private readonly string _path;

    public WindowsBackupPasswordStore(string databasePath)
    {
        var scope = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(databasePath).ToUpperInvariant())));
        _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Area11", "backup-credentials", scope + ".dpapi");
    }

    public string? Load()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows backup credential storage is required.");
        if (!File.Exists(_path)) return null;
        if (new FileInfo(_path).Length > 16384) throw new InvalidDataException("Invalid protected backup credential.");
        var plain = Protect(File.ReadAllBytes(_path), encrypt: false);
        try { return Encoding.UTF8.GetString(plain); }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    public void Save(string password)
    {
        BackupPackage.ValidatePassword(password);
        var plain = Encoding.UTF8.GetBytes(password);
        byte[] encrypted;
        try { encrypted = Protect(plain, encrypt: true); }
        finally { CryptographicOperations.ZeroMemory(plain); }
        var folder = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(folder); // inherits current user's profile ACL
        var temporary = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                file.Write(encrypted);
                file.Flush(flushToDisk: true);
            }
            File.Move(temporary, _path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Blob { public int Length; public IntPtr Data; }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref Blob input, IntPtr description, IntPtr entropy,
        IntPtr reserved, IntPtr prompt, int flags, out Blob output);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy,
        IntPtr reserved, IntPtr prompt, int flags, out Blob output);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr pointer);

    private static byte[] Protect(byte[] data, bool encrypt)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows backup credential storage is required.");
        var input = new Blob { Length = data.Length, Data = Marshal.AllocHGlobal(data.Length) };
        var output = new Blob();
        try
        {
            Marshal.Copy(data, 0, input.Data, data.Length);
            var ok = encrypt
                ? CryptProtectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output)
                : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!ok || output.Length < 0 || output.Length > 16384)
                throw new CryptographicException("Backup credential storage is unavailable for this Windows account.");
            var result = new byte[output.Length];
            Marshal.Copy(output.Data, result, 0, result.Length);
            return result;
        }
        finally
        {
            Marshal.Copy(new byte[data.Length], 0, input.Data, data.Length);
            Marshal.FreeHGlobal(input.Data);
            if (output.Data != IntPtr.Zero)
            {
                if (output.Length > 0 && output.Length <= 16384)
                    Marshal.Copy(new byte[output.Length], 0, output.Data, output.Length);
                LocalFree(output.Data);
            }
        }
    }
}
