using System.Collections.ObjectModel;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// Owner-only list of the shop's own expense words (owner's word, Nov 2026): add under either
/// main type, rename the list word, or put one away. Old expenses always keep the spelling they
/// were saved with, and 'other' can be neither renamed nor put away.
/// </summary>
public sealed class ExpenseCategoryManagerViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;
    private ExpenseCategory? _selected;
    private string _nameText = string.Empty;
    private string _renameText = string.Empty;
    private CategoryOption _selectedType;
    private string _statusText = "Running costs first, then stock goods.";
    private bool _isStatusError;

    public ExpenseCategoryManagerViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        TypeOptions = new List<CategoryOption>
        {
            new(ExpenseCategoryType.Running, ExpenseCategoryType.Label(ExpenseCategoryType.Running)),
            new(ExpenseCategoryType.Stock, ExpenseCategoryType.Label(ExpenseCategoryType.Stock))
        };
        _selectedType = TypeOptions[0];

        AddCommand = new RelayCommand(Add);
        RenameCommand = new RelayCommand(Rename);
        ToggleCommand = new RelayCommand(Toggle);

        Reload();
    }

    public ObservableCollection<ExpenseCategory> Rows { get; } = new();

    public IReadOnlyList<CategoryOption> TypeOptions { get; }

    public string WindowTitle => AppDisplayName + " — Manage expense categories";

    private string AppDisplayName
    {
        get
        {
            var name = _services.Settings.GetString("brand.appName").Trim();
            return name.Length == 0 ? "Area11" : name;
        }
    }

    public ExpenseCategory? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                RenameText = value?.Name ?? string.Empty;
                OnPropertyChanged(nameof(ToggleLabel));
            }
        }
    }

    public string NameText
    {
        get => _nameText;
        set => SetProperty(ref _nameText, value);
    }

    public string RenameText
    {
        get => _renameText;
        set => SetProperty(ref _renameText, value);
    }

    public CategoryOption SelectedType
    {
        get => _selectedType;
        set => SetProperty(ref _selectedType, value);
    }

    public string ToggleLabel => Selected?.Active == false ? "Bring back" : "Put away";

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsStatusError
    {
        get => _isStatusError;
        private set => SetProperty(ref _isStatusError, value);
    }

    public RelayCommand AddCommand { get; }

    public RelayCommand RenameCommand { get; }

    public RelayCommand ToggleCommand { get; }

    private void Add()
    {
        var added = _services.ExpenseCategories.Add(_user, NameText, SelectedType.Key);

        if (!added.Ok || added.Value is null)
        {
            SetStatus(added.Error ?? "The category could not be added.", true);
            return;
        }

        NameText = string.Empty;
        Reload();
        Selected = Rows.FirstOrDefault(row => row.Id == added.Value.Id);
        SetStatus("'" + added.Value.Name + "' was added under " + added.Value.TypeLabel + ".", false);
    }

    private void Rename()
    {
        if (Selected is null)
        {
            SetStatus("Pick a category from the list first.", true);
            return;
        }

        var renamed = _services.ExpenseCategories.Rename(_user, Selected.Id, RenameText);

        if (!renamed.Ok)
        {
            SetStatus(renamed.Error ?? "The category could not be renamed.", true);
            return;
        }

        var id = Selected.Id;
        Reload();
        Selected = Rows.FirstOrDefault(row => row.Id == id);
        SetStatus("Renamed. Old expenses keep the spelling they were saved with.", false);
    }

    private void Toggle()
    {
        if (Selected is null)
        {
            SetStatus("Pick a category from the list first.", true);
            return;
        }

        var changed = _services.ExpenseCategories.SetActive(_user, Selected.Id, !Selected.Active);

        if (!changed.Ok)
        {
            SetStatus(changed.Error ?? "The category could not be changed.", true);
            return;
        }

        var id = Selected.Id;
        var back = !Selected.Active;
        Reload();
        Selected = Rows.FirstOrDefault(row => row.Id == id);
        SetStatus(back ? "Back on the picker." : "Put away. Old expenses are untouched.", false);
    }

    private void Reload()
    {
        Rows.Clear();

        foreach (var row in _services.ExpenseCategories.List(includeInactive: true))
        {
            Rows.Add(row);
        }
    }

    private void SetStatus(string message, bool error)
    {
        StatusText = message;
        IsStatusError = error;
    }
}
