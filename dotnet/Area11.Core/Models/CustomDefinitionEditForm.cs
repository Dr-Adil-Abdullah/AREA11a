namespace Area11.Core.Models;

/// <summary>Detached exact post-write entity state. Public rows are copies;not a durable identity
/// or permission grant. Every row carries its own token for the same complete entity baseline.</summary>
public sealed class CustomDefinitionEditForm
{
    private readonly List<CustomField> _fields;
    public string Entity { get; }
    public int FieldId { get; }
    [System.Text.Json.Serialization.JsonIgnore]
    public EditVersion EditVersion { get; }
    public IReadOnlyList<CustomField> Fields => _fields.Select(Copy).ToList().AsReadOnly();

    internal CustomDefinitionEditForm(string entity, int fieldId, IEnumerable<CustomField> fields)
    {
        Entity = entity;
        FieldId = fieldId;
        _fields = fields.Select(Copy).ToList();
        EditVersion = _fields.Single(field => field.Id == fieldId).EditVersion
            ?? throw new InvalidOperationException("The saved definition needs an edit baseline.");
    }

    private static CustomField Copy(CustomField field) => new()
    {
        Id = field.Id, Entity = field.Entity, FieldKey = field.FieldKey, Label = field.Label,
        Type = field.Type, OptionsJson = field.OptionsJson, HelpText = field.HelpText,
        Required = field.Required, Active = field.Active, Sort = field.Sort,
        CreatedAt = field.CreatedAt, EditVersion = field.EditVersion
    };
}
