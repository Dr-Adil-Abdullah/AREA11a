namespace Area11.Core.Models;

public enum UserRole
{
    Owner,
    Manager,
    Salesman,
    Cashier
}

public static class UserRoles
{
    public static UserRole Parse(string? raw) => (raw ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "owner" => UserRole.Owner,
        "manager" => UserRole.Manager,
        "salesman" => UserRole.Salesman,
        "cashier" => UserRole.Cashier,
        _ => UserRole.Cashier
    };

    public static string ToDb(UserRole role) => role switch
    {
        UserRole.Owner => "owner",
        UserRole.Manager => "manager",
        UserRole.Salesman => "salesman",
        _ => "cashier"
    };

    public static string Label(UserRole role) => role switch
    {
        UserRole.Owner => "Owner",
        UserRole.Manager => "Manager",
        UserRole.Salesman => "Salesman",
        _ => "Cashier"
    };
}

/// <summary>One signed-in person. The hash columns never leave the Core layer.</summary>
public sealed class User
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Role { get; set; } = "cashier";

    public bool Active { get; set; } = true;

    public UserRole Kind => UserRoles.Parse(Role);

    public bool IsOwner => Kind == UserRole.Owner;

    public bool CanManage => Kind is UserRole.Owner or UserRole.Manager;

    public string RoleLabel => UserRoles.Label(Kind);

    public override string ToString() => $"{Name} ({RoleLabel})";
}
