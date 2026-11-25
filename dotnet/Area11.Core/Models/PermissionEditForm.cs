using System.Collections.ObjectModel;

namespace Area11.Core.Models;

/// <summary>Detached authorized staff permission editor. Presence/defaults and current target
/// role/active are covered by its local token,not a durable revision or authorization grant.</summary>
public sealed class PermissionEditForm
{
    public int UserId { get; }
    public UserRole Role { get; }
    public IReadOnlyDictionary<string, bool> Values { get; }
    [System.Text.Json.Serialization.JsonIgnore]
    public EditVersion EditVersion { get; }

    internal PermissionEditForm(int userId, UserRole role, IDictionary<string, bool> values, EditVersion version)
    {
        UserId = userId;
        Role = role;
        Values = new ReadOnlyDictionary<string, bool>(new Dictionary<string, bool>(values, StringComparer.Ordinal));
        EditVersion = version;
    }
}
