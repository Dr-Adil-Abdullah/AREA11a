namespace Area11.WPF.ViewModels;

/// <summary>Display-only row for the products grid; formatting stays outside the model.</summary>
public sealed class ProductRow
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Barcode { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string Pack { get; init; } = string.Empty;

    public string Retail { get; init; } = string.Empty;

    public string Stock { get; init; } = string.Empty;
}
