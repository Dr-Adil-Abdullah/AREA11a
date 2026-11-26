using System.IO;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Area11.Core.Utils;

/// <summary>Packaged, non-secret documentation only. Never reads a shop database or executes HTML.</summary>
public sealed class GuideLibrary
{
    private readonly GuideBundle _bundle;
    private readonly Dictionary<string, GuideTopic> _byId;

    private GuideLibrary(GuideBundle bundle)
    {
        _bundle = bundle;
        if (string.IsNullOrWhiteSpace(bundle.Version)
            || !DateOnly.TryParseExact(bundle.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            || bundle.Topics is null || bundle.Topics.Count > 1000)
            throw new InvalidDataException("The documentation metadata is invalid.");
        _byId = new Dictionary<string, GuideTopic>(StringComparer.Ordinal);
        foreach (var topic in bundle.Topics)
        {
            if (topic is null || string.IsNullOrWhiteSpace(topic.Id) || string.IsNullOrWhiteSpace(topic.Title)
                || topic.Parent is null || topic.Path is null || topic.Blocks is null || topic.Links is null
                || !_byId.TryAdd(topic.Id, topic))
                throw new InvalidDataException("The documentation topic is invalid or duplicated.");
        }
        if (Roots.Count != 6) throw new InvalidDataException("The documentation must have six sections.");
        foreach (var topic in bundle.Topics)
        {
            if ((topic.Parent.Length > 0 && !_byId.ContainsKey(topic.Parent))
                || topic.Links.Any(link => link is null || !_byId.ContainsKey(link)))
                throw new InvalidDataException("The documentation link is missing.");
            var visited = new HashSet<string>(StringComparer.Ordinal) { topic.Id };
            var parent = topic.Parent;
            while (parent.Length > 0)
            {
                if (!visited.Add(parent)) throw new InvalidDataException("The documentation hierarchy contains a cycle.");
                if (!_byId.TryGetValue(parent, out var ancestor))
                    throw new InvalidDataException("The documentation parent is missing.");
                parent = ancestor.Parent;
            }
            foreach (var block in topic.Blocks)
                if (block is null || block.Kind is not ("heading" or "paragraph" or "item" or "table")
                    || block.Text is null || block.Rows is null || block.Rows.Any(row => row is null || row.Any(cell => cell is null)))
                    throw new InvalidDataException("The documentation block is invalid.");
        }
    }

    public string Version => _bundle.Version;
    public string Date => _bundle.Date;
    public IReadOnlyList<GuideTopic> Roots => _bundle.Topics.Where(topic => topic.Parent.Length == 0).ToArray();
    public IReadOnlyList<GuideTopic> Children(string id) => _bundle.Topics.Where(topic => topic.Parent == id).ToArray();
    public GuideTopic? Find(string id) => _byId.GetValueOrDefault(id);

    public static GuideLibrary Load(string file)
    {
        // No global singleton or startup preloading. One bounded read per explicitly opened view.
        var info = new FileInfo(file);
        if (!info.Exists || info.Length > 2 * 1024 * 1024) throw new InvalidDataException("The documentation file is missing or too large.");
        using var stream = info.OpenRead();
        var bundle = JsonSerializer.Deserialize<GuideBundle>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true, MaxDepth = 32
        }) ?? throw new InvalidDataException("The documentation file is empty.");
        return new GuideLibrary(bundle);
    }

    public IReadOnlyList<GuideTopic> Search(string query)
    {
        var words = Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return Roots;
        return _bundle.Topics.Where(topic =>
        {
            var text = Normalize(topic.Id + " " + topic.Title + " " + topic.Path + " " + string.Join(" ", topic.Blocks.Select(block =>
                block.Text + " " + string.Join(" ", block.Rows.SelectMany(row => row))))
                + (topic.Id == "basics.login" ? " لاگ ان login password" : "")
                + (topic.Id == "sales.no-bill" ? " نو بل no bill PR" : ""));
            return words.All(text.Contains);
        }).ToArray();
    }

    private static string Normalize(string value)
    {
        var result = new StringBuilder();
        foreach (var c in value.ToLowerInvariant())
        {
            if ((c >= '\u064b' && c <= '\u065f') || c is '\u200c' or '\u200d') continue;
            result.Append(c switch { 'ي' or 'ى' => 'ی', 'ك' => 'ک', 'أ' or 'إ' or 'آ' => 'ا', _ => c });
        }
        return result.ToString();
    }
}

public sealed class GuideBundle
{
    public string Version { get; set; } = "";
    public string Date { get; set; } = "";
    public List<GuideTopic> Topics { get; set; } = new();
}

public sealed class GuideTopic
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Parent { get; set; } = "";
    public string Path { get; set; } = "";
    public List<GuideBlock> Blocks { get; set; } = new();
    public List<string> Links { get; set; } = new();
}

public sealed class GuideBlock
{
    public string Kind { get; set; } = "";
    public string Text { get; set; } = "";
    public List<List<string>> Rows { get; set; } = new();
}
