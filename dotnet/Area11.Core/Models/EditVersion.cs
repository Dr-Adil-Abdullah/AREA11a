using Area11.Core.Data;

namespace Area11.Core.Models;

/// <summary>Immutable local edit baseline,not a login or durable request ID. Never serialized in
/// audit/export/draft payloads. Only an authorized service snapshot creates one.</summary>
public sealed class EditVersion
{
    internal EditVersion(Database database, string scope, int id, string fingerprint)
    { Database = database; Scope = scope; Id = id; Fingerprint = fingerprint; }
    internal Database Database { get; }
    internal string Scope { get; }
    internal int Id { get; }
    internal string Fingerprint { get; }
}

/// <summary>Fields and answers read together;the parent edit loader nests this in its own snapshot.</summary>
public sealed class ExtraFieldsSnapshot
{
    public List<CustomField> Fields { get; init; } = new();
    public Dictionary<int, string> Answers { get; init; } = new();
    [System.Text.Json.Serialization.JsonIgnore]
    public EditVersion? Version { get; init; }
}

public sealed class RecordEditForm<T>
{
    public required T Record { get; init; }
    public required ExtraFieldsSnapshot Extra { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public EditVersion? Version { get; init; }
}
