namespace Area11.Desktop.ViewModels;

/// <summary>One Settings section shown in the shell's contextual header row.</summary>
public sealed class SettingsTabItem : ObservableObject
{
    private string _title;
    private bool _isCurrent;

    public SettingsTabItem(int index, string title, string labelKey = "")
    {
        Index = index;
        _title = title;
        LabelKey = labelKey;
    }

    /// <summary>The existing SettingsViewModel tab index; no second section-numbering scheme.</summary>
    public int Index { get; }

    /// <summary>Optional application-label key for sections such as Cloud sync.</summary>
    public string LabelKey { get; }

    public string Title => _title;

    public bool IsCurrent
    {
        get => _isCurrent;
        set => SetProperty(ref _isCurrent, value);
    }

    public void SetTitle(string title) => SetProperty(ref _title, title, nameof(Title));
}

/// <summary>One category choice in the Products header, including the "all" choice with a null id.</summary>
public sealed class HeaderCategoryItem
{
    public HeaderCategoryItem(int? id, string displayName)
    {
        Id = id;
        DisplayName = displayName;
    }

    public int? Id { get; }

    public string DisplayName { get; }

    public override string ToString() => DisplayName;
}
