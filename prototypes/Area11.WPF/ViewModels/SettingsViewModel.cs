using System.Collections.ObjectModel;
using Area11.WPF.Services;
using Area11.WPF.ViewModels.Base;

namespace Area11.WPF.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly string _userName;
    private readonly List<SettingRow> _allRows = new();

    private string _searchText = string.Empty;
    private string _statusMessage = "Changes are written to the database immediately.";
    private bool _isStatusError;

    public SettingsViewModel(AppServices services, string userName)
    {
        _settings = services.Settings;
        _userName = userName;

        SaveCommand = new RelayCommand(SaveChanged);
        ReloadCommand = new RelayCommand(Load);

        Load();
    }

    public ObservableCollection<SettingRow> Rows { get; } = new();

    public RelayCommand SaveCommand { get; }

    public RelayCommand ReloadCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsStatusError
    {
        get => _isStatusError;
        private set => SetProperty(ref _isStatusError, value);
    }

    private void Load()
    {
        _allRows.Clear();

        foreach (var pair in _settings.All().OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            _allRows.Add(new SettingRow(pair.Key, pair.Value));
        }

        ApplyFilter();
        StatusMessage = $"{_allRows.Count} editable settings loaded.";
        IsStatusError = false;
    }

    private void ApplyFilter()
    {
        var term = SearchText.Trim();
        Rows.Clear();

        foreach (var row in _allRows)
        {
            if (term.Length == 0
                || row.Key.Contains(term, StringComparison.OrdinalIgnoreCase)
                || row.Value.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                Rows.Add(row);
            }
        }
    }

    private void SaveChanged()
    {
        var changed = _allRows.Where(r => r.IsChanged).ToList();
        if (changed.Count == 0)
        {
            StatusMessage = "Nothing changed.";
            IsStatusError = false;
            return;
        }

        var saved = 0;
        foreach (var row in changed)
        {
            var result = _settings.Save(row.Key, row.Value, _userName);
            if (!result.Ok)
            {
                StatusMessage = result.Error ?? $"'{row.Key}' could not be saved.";
                IsStatusError = true;
                return;
            }

            saved++;
        }

        Load();
        StatusMessage = $"{saved} setting(s) saved.";
        IsStatusError = false;
    }
}
