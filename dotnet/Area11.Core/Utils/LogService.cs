using System.Diagnostics;
using System.IO;

namespace Area11.Core.Utils;

/// <summary>
/// One log file per day under data/logs. Failures in the logger itself never break a sale:
/// they fall back to the debug output (no silent empty catch — Spec rule "central error handling").
/// </summary>
public sealed class LogService
{
    private readonly object _gate = new();
    private readonly string _folder;

    public LogService(string? folder = null)
    {
        _folder = folder ?? AppPaths.LogsFolder;
        Directory.CreateDirectory(_folder);
    }

    public void Info(string message) => Write("INFO", message, null);

    public void Warn(string message) => Write("WARN", message, null);

    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
        if (exception is not null)
        {
            line += Environment.NewLine + exception;
        }

        lock (_gate)
        {
            try
            {
                var file = Path.Combine(_folder, $"app-{DateTime.Now:yyyy-MM-dd}.log");
                File.AppendAllText(file, line + Environment.NewLine);
            }
            catch (Exception writeFailure) when (writeFailure is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"[Area11] log write failed: {writeFailure.Message}{Environment.NewLine}{line}");
            }
        }
    }
}
