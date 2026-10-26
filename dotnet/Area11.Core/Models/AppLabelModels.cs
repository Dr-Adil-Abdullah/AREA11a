namespace Area11.Core.Models;

/// <summary>One stable application-section key and the words Area11 ships with.</summary>
public sealed class AppLabelDefinition
{
    public AppLabelDefinition(string key, string defaultText)
    {
        Key = key;
        DefaultText = defaultText;
    }

    public string Key { get; }

    public string DefaultText { get; }

    /// <summary>The ordinary settings row; the visible text is never a navigation or permission key.</summary>
    public string SettingKey => "labels." + Key;
}

/// <summary>One editable row: the original words beside the shop's current words.</summary>
public sealed class AppLabelValue
{
    public string Key { get; init; } = string.Empty;

    public string DefaultText { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;
}
