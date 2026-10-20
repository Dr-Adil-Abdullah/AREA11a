namespace Area11.WPF.Models;

public enum UserRole
{
    Owner,
    Manager,
    Cashier
}

public sealed class User
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    public bool IsOwner => Role == UserRole.Owner;

    public bool CanManage => Role is UserRole.Owner or UserRole.Manager;
}
