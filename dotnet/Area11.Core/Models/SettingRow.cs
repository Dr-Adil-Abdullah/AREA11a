namespace Area11.Core.Models;

/// <summary>One row of the settings key/value table (values are JSON text always).</summary>
public sealed class SettingRow
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string UpdatedAt { get; set; } = string.Empty;
}
