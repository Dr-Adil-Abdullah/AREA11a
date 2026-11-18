namespace Area11.Core.Models;

/// <summary>
/// Exact persisted-definition comparison for stale manager UI detection, not authorization,
/// a database version token, or an atomic compare-and-swap. Unsaved editor text is not compared.
/// </summary>
public static class CustomFieldDefinitionState
{
    public static bool Matches(CustomField? expected, CustomField? current) =>
        expected is not null && current is not null
        && expected.Id == current.Id
        && string.Equals(expected.Entity, current.Entity, StringComparison.Ordinal)
        && string.Equals(expected.FieldKey, current.FieldKey, StringComparison.Ordinal)
        && string.Equals(expected.Label, current.Label, StringComparison.Ordinal)
        && string.Equals(expected.Type, current.Type, StringComparison.Ordinal)
        && string.Equals(expected.OptionsJson, current.OptionsJson, StringComparison.Ordinal)
        && string.Equals(expected.HelpText, current.HelpText, StringComparison.Ordinal)
        && expected.Required == current.Required
        && expected.Active == current.Active
        && expected.Sort == current.Sort
        && string.Equals(expected.CreatedAt, current.CreatedAt, StringComparison.Ordinal);
}
