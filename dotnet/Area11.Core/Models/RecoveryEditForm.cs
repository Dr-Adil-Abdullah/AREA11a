namespace Area11.Core.Models;

/// <summary>Owner-authorized recovery editor. No word/hash/raw JSON escapes Core.
/// Local baseline only,not authentication or durable retry identity.</summary>
public sealed class RecoveryEditForm
{
    public bool UsesFactoryWord { get; }
    [System.Text.Json.Serialization.JsonIgnore]
    public EditVersion EditVersion { get; }

    internal RecoveryEditForm(bool usesFactoryWord, EditVersion version)
    { UsesFactoryWord = usesFactoryWord; EditVersion = version; }
}
