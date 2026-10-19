namespace Area11.WPF.Models;

public sealed class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? ParentId { get; set; }

    public override string ToString() => Name;
}

public sealed class Company
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public override string ToString() => Name;
}
