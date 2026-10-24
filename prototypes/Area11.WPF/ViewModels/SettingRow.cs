using Area11.WPF.ViewModels.Base;

namespace Area11.WPF.ViewModels;

/// <summary>One editable line from the settings table.</summary>
public sealed class SettingRow : ObservableObject
{
    private string _value;

    public SettingRow(string key, string value)
    {
        Key = key;
        _value = value;
        OriginalValue = value;
        Group = key.Length > 0 && key.Contains('.') ? key[..key.IndexOf('.')] : "general";
    }

    public string Key { get; }

    public string Group { get; }

    public string OriginalValue { get; }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    public bool IsChanged => !string.Equals(Value, OriginalValue, StringComparison.Ordinal);
}
