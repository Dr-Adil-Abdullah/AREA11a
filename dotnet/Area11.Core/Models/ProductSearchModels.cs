namespace Area11.Core.Models;

/// <summary>
/// The answer to a product search that may have been misspelt (F-50).
///
/// Why it is more than a list: a typo-tolerant search has to tell the truth about itself. If the page
/// silently showed "the closest medicines" as though they were results, a cashier would read the top
/// row as the answer - so <see cref="IsFuzzy"/> travels with the rows and the page says the words in
/// <see cref="NoteLabel"/> above them.
/// </summary>
public sealed class ProductSearchResult
{
    public List<Product> Items { get; } = new();

    /// <summary>The words as typed, trimmed - so the page repeats them exactly.</summary>
    public string Typed { get; init; } = string.Empty;

    /// <summary>True when the rows are suggestions for a misspelling, not an exact search.</summary>
    public bool IsFuzzy { get; set; }

    /// <summary>What the page shows above the rows; empty when the search was exact.</summary>
    public string NoteLabel { get; set; } = string.Empty;

    /// <summary>The name the program says is closest, when it is guessing.</summary>
    public string ClosestName { get; set; } = string.Empty;

    public int Count => Items.Count;

    public bool IsEmpty => Items.Count == 0;

    public string CountLabel => Count == 1 ? "1 product" : Count + " products";

    /// <summary>The one sentence the search box shows: how many, and whether they are a guess.</summary>
    public string SummaryLabel => IsFuzzy
        ? NoteLabel
        : (IsEmpty ? "No product matches \"" + Typed + "\"." : CountLabel + ".");
}
