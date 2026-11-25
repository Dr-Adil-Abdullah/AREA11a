namespace Area11.Core.Models;

/// <summary>Immutable authorized roster baseline. No credential hashes or authentication proof.
/// Local content comparison only,not a durable revision/retry identity.</summary>
public sealed class StaffEditForm
{
    public int UserId { get; }
    public string Name { get; }
    public UserRole Role { get; }
    public bool Active { get; }
    [System.Text.Json.Serialization.JsonIgnore]
    public EditVersion EditVersion { get; }

    internal StaffEditForm(User person, EditVersion version)
    { UserId = person.Id; Name = person.Name; Role = person.Kind; Active = person.Active; EditVersion = version; }
}
