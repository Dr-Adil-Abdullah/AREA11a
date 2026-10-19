using System.Text;

namespace Area11.WPF.Services;

/// <summary>Single writable log used by every service; one file per day.</summary>
public sealed class LoggerService
{
    private readonly string _folder;
    private readonly object _gate = new();

    public LoggerService(string folder)
    {
        _folder = folder;
        Directory.CreateDirectory(folder);
    }

    public void Info(string message) => Write("INFO", message, null);

    public void Warn(string message) => Write("WARN", message, null);

    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        try
        {
            var builder = new StringBuilder();
            builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append(" [").Append(level).Append("] ").Append(message);
            if (exception is not null)
            {
                builder.AppendLine().Append(exception);
            }

            lock (_gate)
            {
                File.AppendAllText(
                    Path.Combine(_folder, $"app-{DateTime.Now:yyyy-MM-dd}.log"),
                    builder + Environment.NewLine);
            }
        }
        catch (IOException)
        {
            // Logging must never take the till down; the UI still receives the friendly error.
        }
        catch (UnauthorizedAccessException)
        {
            // Same rule: a locked or read-only log folder is not a business failure.
        }
    }
}
