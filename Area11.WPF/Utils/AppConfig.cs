using System.Text.Json;

namespace Area11.WPF.Utils;

/// <summary>
/// Configuration comes from appsettings.json only — no URL, path or secret is hard-coded in C#.
/// </summary>
public sealed class AppConfig
{
    public DatabaseConfig Database { get; set; } = new();

    public LoggingConfig Logging { get; set; } = new();

    public SecurityConfig Security { get; set; } = new();

    public ApiConfig Api { get; set; } = new();

    public SyncConfig Sync { get; set; } = new();

    public static AppConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"appsettings.json not found at {path}");
        }

        var config = JsonSerializer.Deserialize<AppConfig>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return config ?? throw new InvalidOperationException("appsettings.json could not be read.");
    }

    public string ResolvePath(string relativeOrAbsolute)
    {
        return Path.IsPathRooted(relativeOrAbsolute)
            ? relativeOrAbsolute
            : Path.Combine(AppContext.BaseDirectory, relativeOrAbsolute);
    }

    public string ResolveDatabasePath() => ResolvePath(Database.Path);

    public string ResolveLogPath() => ResolvePath(Logging.Path);
}

public sealed class DatabaseConfig
{
    public string Path { get; set; } = "data/area11.db";
}

public sealed class LoggingConfig
{
    public string Path { get; set; } = "data/logs";
}

public sealed class SecurityConfig
{
    public string DefaultOwnerPassword { get; set; } = "area11";

    public int MaxLoginAttempts { get; set; } = 5;

    public int LockoutSeconds { get; set; } = 30;
}

public sealed class ApiConfig
{
    public string BaseUrl { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 30;
}

public sealed class SyncConfig
{
    public string Url { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public string Table { get; set; } = "area11_sync";

    public string ShopId { get; set; } = "area11-main";
}
