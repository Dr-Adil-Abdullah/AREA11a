using System.IO;

namespace Area11.Core.Utils;

/// <summary>How this computer uses the shop's database.</summary>
public enum ShopMode
{
    /// <summary>One computer: the database sits in this PC's own data folder.</summary>
    Single,

    /// <summary>The main counter: this PC owns the database and shares its folder on the LAN.</summary>
    Main,

    /// <summary>The second counter: this PC opens the main counter's file over the network.</summary>
    Second
}

/// <summary>
/// The shop's own little settings file - **outside** the database (F-45, Spec §34.9).
///
/// Why a file at all: the database path cannot live in the database. A second counter must know
/// *where* the shop's file is before it can open a single table, so this one line lives in
/// `%ProgramData%\Area11\shop.ini` on each PC. Nothing else is stored here; every real setting
/// stays in the `settings` table.
///
/// The file is read **once** at start-up. Changing it therefore needs a restart of the program, and
/// every screen says so - silently swapping the database under an open bill would be the worst kind
/// of surprise.
/// </summary>
public sealed class ShopConfig
{
    public const string FileName = "shop.ini";

    private static readonly Lazy<ShopConfig> CurrentLazy =
        new(() => Load(DefaultIniPath), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The configuration this running program started with.</summary>
    public static ShopConfig Current => CurrentLazy.Value;

    /// <summary>Where the file sits on every PC (ProgramData, or LocalAppData when that is closed).</summary>
    public static string DefaultIniPath => Path.Combine(ConfigRoot(), FileName);

    public ShopMode Mode { get; init; } = ShopMode.Single;

    /// <summary>The folder holding `area11.db`; null on a single-computer shop.</summary>
    public string? DataFolder { get; init; }

    /// <summary>A line from the file that could not be understood - shown, never guessed away.</summary>
    public string? Problem { get; init; }

    public bool IsLan => Mode != ShopMode.Single;

    public string ModeLabel => Mode switch
    {
        ShopMode.Main => "Main counter - this PC holds the database",
        ShopMode.Second => "Second counter - uses the main PC's database",
        _ => "One computer"
    };

    /// <summary>Reads the file. A missing file is not an error: most shops have one computer.</summary>
    public static ShopConfig Load(string? path = null)
    {
        var file = string.IsNullOrWhiteSpace(path) ? DefaultIniPath : path;
        var mode = ShopMode.Single;
        string? folder = null;
        string? problem = null;

        if (!File.Exists(file))
        {
            return new ShopConfig();
        }

        try
        {
            foreach (var raw in File.ReadAllLines(file))
            {
                var line = raw.Trim();

                if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
                {
                    continue;
                }

                var split = line.IndexOf('=');
                if (split <= 0)
                {
                    continue;
                }

                var key = line[..split].Trim().ToLowerInvariant();
                var value = line[(split + 1)..].Trim().Trim('"');

                switch (key)
                {
                    case "mode":
                        mode = value.ToLowerInvariant() switch
                        {
                            "main" => ShopMode.Main,
                            "second" => ShopMode.Second,
                            "single" or "" => ShopMode.Single,
                            _ => Unknown(value, ref problem, mode)
                        };
                        break;

                    case "data":
                        folder = value.Length == 0 ? null : NormalizeFolder(value);
                        break;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            problem = "The file " + file + " could not be read: " + exception.Message;
        }

        return new ShopConfig { Mode = mode, DataFolder = folder, Problem = problem };
    }

    /// <summary>Writes the two lines. The old file is replaced in one move, never half written.</summary>
    public static void Save(ShopConfig config, string? path = null)
    {
        var file = string.IsNullOrWhiteSpace(path) ? DefaultIniPath : path;
        var folder = Path.GetDirectoryName(file);

        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var lines = new[]
        {
            "; Area11 - where this counter keeps the shop's database.",
            "; Written by the program (SYSTEM -> Second counter). Read once at start-up.",
            "mode=" + config.Mode.ToString().ToLowerInvariant(),
            "data=" + (config.DataFolder ?? string.Empty)
        };

        var temporary = file + ".new";
        File.WriteAllLines(temporary, lines);
        File.Move(temporary, file, overwrite: true);
    }

    /// <summary>
    /// The rules a folder must obey before it is written into the file. The main counter must keep
    /// the file on its **own disk** (a shared file must have one owner); the second counter must
    /// name the main PC's folder the way Windows writes a share: `\\PC\Area11`.
    /// </summary>
    public static string? Validate(ShopMode mode, string? folder)
    {
        var clean = NormalizeFolder(folder);

        return mode switch
        {
            ShopMode.Main when clean.Length == 0 =>
                "Write the folder that holds the shop's database on this PC - usually C:\\ProgramData\\Area11.",
            ShopMode.Main when IsUnc(clean) =>
                "The main counter must keep the database on its own disk. A main counter cannot open the file from another PC.",
            ShopMode.Second when clean.Length == 0 =>
                "Write the main PC's folder, for example \\\\MAIN-PC\\Area11.",
            ShopMode.Second when !IsUnc(clean) =>
                "A second counter opens the main PC's folder over the network, so it starts with two backslashes, for example \\\\MAIN-PC\\Area11.",
            ShopMode.Second when clean.TrimStart('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries).Length < 2 =>
                "That is not a shared folder. Write the PC name and the share, for example \\\\MAIN-PC\\Area11.",
            _ => null
        };
    }

    /// <summary>True for `\\PC\share\...`; a mapped drive (Z:) looks local and is warned about in the guide.</summary>
    public static bool IsUnc(string? path) =>
        !string.IsNullOrWhiteSpace(path) && path!.TrimStart().StartsWith(@"\\", StringComparison.Ordinal);

    /// <summary>Trims spaces and quotes and drops a trailing separator, so two spellings compare equal.</summary>
    public static string NormalizeFolder(string? folder)
    {
        var clean = (folder ?? string.Empty).Trim().Trim('"').Trim();

        while (clean.Length > 1 && (clean.EndsWith('\\') || clean.EndsWith('/')))
        {
            clean = clean[..^1];
        }

        return clean;
    }

    private static ShopMode Unknown(string value, ref string? problem, ShopMode fallback)
    {
        problem = "The file says mode=" + value + ", which is not single, main or second. Nothing was changed.";
        return fallback;
    }

    private static string ConfigRoot()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Area11"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Area11")
        };

        foreach (var candidate in candidates)
        {
            try
            {
                Directory.CreateDirectory(candidate);
                return candidate;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Try the next one; the fallback below only matters on a locked-down PC.
            }
        }

        return Path.Combine(Path.GetTempPath(), "Area11");
    }
}
