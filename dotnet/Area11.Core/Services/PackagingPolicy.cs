using Area11.Core.Models;

namespace Area11.Core.Services;

/// <summary>
/// Descriptive pack/variant metadata and the shared in-memory product search used by Desktop pages.
/// A pack label never creates a variant row and never changes the base unit or box/strip conversion.
/// </summary>
public static class PackagingPolicy
{
    public const int MaxPackSizeLength = 100;

    /// <summary>Trim and collapse spacing in the optional, human-readable pack description.</summary>
    public static string CleanPackSize(string? value) => string.Join(" ",
        (value ?? string.Empty).Trim()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static string? PackSizeError(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Any(char.IsControl))
        {
            return "Pack size / variant label must be one line.";
        }

        return CleanPackSize(text).Length > MaxPackSizeLength
            ? $"Pack size / variant label must be {MaxPackSizeLength} characters or fewer."
            : null;
    }

    /// <summary>
    /// Search packaging the way a person types it: 50 mL, 50ml and 50 ML are the same key.
    /// This is only a search key; the saved label and managed stock unit stay untouched.
    /// </summary>
    public static string SearchKey(string? value)
    {
        var key = new string((value ?? string.Empty)
            .Where(character => !char.IsWhiteSpace(character))
            .Select(character => character == '×' ? 'x' : character)
            .ToArray());
        return key.ToLowerInvariant();
    }

    /// <summary>Portion 10: a query is that many words at most; the rest is ignored, never an error.</summary>
    public const int MaxSearchTokens = 10;

    /// <summary>
    /// Portion 10: one shared word-split for the Counter, Purchases and Products searches.
    /// Whitespace separates words ("50 ml" is two words, "50ml" is one); extra spacing collapses.
    /// </summary>
    public static string[] Tokens(string? term)
    {
        var words = (term ?? string.Empty).Trim()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return words.Length <= MaxSearchTokens ? words : words[..MaxSearchTokens];
    }

    /// <summary>
    /// Exact catalog discovery for pages which already hold the active catalog in memory.
    /// Every word must land somewhere (Portion 10): "panadol 100ml" needs Panadol in a text
    /// field and 100ml in the pack/strength key, in any order. A single word behaves exactly
    /// as before. Fuzzy fallback remains a separate, clearly labelled name/generic step.
    /// </summary>
    public static bool Matches(
        Product product,
        string? term,
        IEnumerable<string>? additionalBarcodes = null)
    {
        var tokens = Tokens(term);
        if (tokens.Length == 0)
        {
            return true;
        }

        return tokens.All(token => MatchesToken(product, token, additionalBarcodes));
    }

    private static bool MatchesToken(
        Product product,
        string token,
        IEnumerable<string>? additionalBarcodes) =>
        Contains(product.Name, token)
        || Contains(product.Generic, token)
        || Contains(product.Brand, token)
        || Contains(product.Barcode, token)
        || Contains(product.RackNo, token)
        || Contains(product.Room, token)
        || Contains(product.CategoryName, token)
        || Contains(product.CategoryTags, token)
        || Contains(product.CompanyName, token)
        || Contains(product.BaseUnit, token)
        || ContainsPackaging(product.PackSizeLabel, token)
        || ContainsPackaging(product.StrengthLabel, token)
        || (additionalBarcodes?.Any(code => Contains(code, token)) ?? false);

    private static bool Contains(string? source, string term) =>
        !string.IsNullOrWhiteSpace(source)
        && source.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsPackaging(string? source, string term)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        var key = SearchKey(term);
        return key.Length > 0 && SearchKey(source).Contains(key, StringComparison.Ordinal);
    }
}
