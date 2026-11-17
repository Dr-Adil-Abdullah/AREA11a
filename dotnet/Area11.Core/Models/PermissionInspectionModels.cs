namespace Area11.Core.Models;

/// <summary>Public permission documentation, not authorization or a claim of complete route coverage.</summary>
public sealed record PermissionDefinition(string Module, string Title, string Summary, string Scope,
    string Prerequisites, string RolloutNote);

/// <summary>Owner-only, secret-free assignment projection. Assigned is not proof of page access.</summary>
public sealed class PermissionAssignment
{
    public int UserId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public bool Active { get; init; }
    public bool Assigned { get; init; }
    public string Source { get; init; } = string.Empty;
    public string Access { get; init; } = string.Empty;
    public string RoleLabel => UserRoles.Label(UserRoles.Parse(Role));
    public string AccountLabel => Active ? "Active" : "Inactive";
    public string AssignedLabel => Assigned ? "Enabled" : "Disabled";
}

public sealed class PermissionInspection
{
    public PermissionDefinition Definition { get; init; } = new("", "", "", "", "", "");
    public bool MainSwitchOn { get; init; }
    public List<PermissionAssignment> People { get; init; } = new();
    public string MainSwitchLabel => "Saved main profit / cost switch: " + (MainSwitchOn ? "ON" : "OFF")
        + ". This gates Profit and Purchase Cost, not the entire ledger module.";
}
