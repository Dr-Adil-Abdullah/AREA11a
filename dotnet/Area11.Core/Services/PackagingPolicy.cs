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

    /// <summary>
    /// Exact catalog discovery for pages which already hold the active catalog in memory.
    /// Fuzzy fallback remains a separate, clearly labelled name/generic suggestion step.
    /// </summary>
    public static bool Matches(
        Product product,
        string? term,
        IEnumerable<string>? additionalBarcodes = null)
    {
        var text = (term ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return true;
        }

        return Contains(product.Name, text)
               || Contains(product.Generic, text)
               || Contains(product.Brand, text)
               || Contains(product.Barcode, text)
               || Contains(product.RackNo, text)
               || Contains(product.Room, text)
               || Contains(product.CategoryName, text)
               || Contains(product.CategoryTags, text)
               || Contains(product.CompanyName, text)
               || Contains(product.BaseUnit, text)
               || ContainsPackaging(product.PackSizeLabel, text)
               || ContainsPackaging(product.StrengthLabel, text)
               || (additionalBarcodes?.Any(code => Contains(code, text)) ?? false);
    }

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
