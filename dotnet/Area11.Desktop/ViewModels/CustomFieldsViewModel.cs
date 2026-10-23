using System.Collections.ObjectModel;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// The screen where the owner makes his own fields (F-42, Spec 2.2 &amp; 8.4).
///
/// Three forms, one list each: **Medicines**, **Customers**, **Suppliers**. A field is a name, a kind
/// of answer, an optional list of choices and a tick that says whether the answer is required. The
/// moment it is saved it appears on that form - there is no rebuild and no restart (Spec 8.4.3).
///
/// The arrows and the put-away button are here because a form is read top to bottom: the owner
/// decides what the shop is asked first. Nothing is ever deleted (D-00b); a field that is put away
/// keeps every answer and comes back unchanged.
///
/// No rule lives here: the label, the choices and every answer are checked by
/// <see cref="CustomFieldService"/>.
/// </summary>
public sealed class CustomFieldsViewModel : ObservableObject, IEmptyPage
{
    /// <summary>
    /// Why this page has nothing on it, in the shop's words (v0.33.0, D-89). **Null when it
    /// has rows**, so nothing in this page has to keep a second flag in step with its list.
    /// </summary>
    public EmptyHint? EmptyHint => EmptyPage.When(Fields.Count == 0, "Own fields");

    private readonly AppServices _services;
    private readonly User _user;

    private CustomFieldTab _selectedTab;
    private CustomFieldAdminRow? _selectedField;
    private CustomFieldTypeOption _selectedType;
    private string _labelText = string.Empty;
    private string _optionsText = string.Empty;
    private bool _required;
    private bool _isEditing;
    private int _editingId;
    private string _statusText = "Pick a form, then add a field. It appears there the moment you save it.";
    private bool _isStatusError;
    private string _countText = "no fields";

    public CustomFieldsViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        Tabs = new List<CustomFieldTab>
        {
            new(CustomFieldEntity.Product, "Medicines"),
            new(CustomFieldEntity.Customer, "Customers"),
            new(CustomFieldEntity.Supplier, "Suppliers")
        };

        Types = CustomFieldType.All
            .Select(type => new CustomFieldTypeOption(type, CustomFieldType.LabelFor(type)))
            .ToList();

        _selectedTab = Tabs[0];
        _selectedType = Types[0];

        NewCommand = new RelayCommand(BeginNew);
        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(BeginNew);
        MoveUpCommand = new RelayCommand(MoveUp);
        MoveDownCommand = new RelayCommand(MoveDown);
        ToggleCommand = new RelayCommand(ToggleActive);
        UseSuggestionCommand = new RelayCommand<CustomFieldSuggestion>(UseSuggestion);

        Load();
    }

    public IReadOnlyList<CustomFieldTab> Tabs { get; }

    public IReadOnlyList<CustomFieldTypeOption> Types { get; }

    public IReadOnlyList<CustomFieldSuggestion> Suggestions { get; } = CustomFieldSuggestion.All;

    public ObservableCollection<CustomFieldAdminRow> Fields { get; } = new();

    public RelayCommand NewCommand { get; }

    public RelayCommand SaveCommand { get; }

    public RelayCommand CancelCommand { get; }

    public RelayCommand MoveUpCommand { get; }

    public RelayCommand MoveDownCommand { get; }

    public RelayCommand ToggleCommand { get; }

    public RelayCommand<CustomFieldSuggestion> UseSuggestionCommand { get; }

    public bool CanManage => _services.Permissions.Can(_user, Modules.Settings);

    public bool IsNotAllowed => !CanManage;

    public CustomFieldTab SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (value is null || !SetProperty(ref _selectedTab, value))
            {
                return;
            }

            BeginNew();
            Load();
        }
    }

    public CustomFieldAdminRow? SelectedField
    {
        get => _selectedField;
        set
        {
            if (!SetProperty(ref _selectedField, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ToggleLabel));
            OnPropertyChanged(nameof(HasSelection));

            if (value is not null)
            {
                Edit(value.Field);
            }
        }
    }

    public bool HasSelection => _selectedField is not null;

    /// <summary>"Put this field away" / "Show it on the form again" - the button reads the state.</summary>
    public string ToggleLabel => _selectedField is { Field.Active: false }
        ? "Show it on the form again"
        : "Put this field away";

    public CustomFieldTypeOption SelectedType
    {
        get => _selectedType;
        set
        {
            if (value is null || !SetProperty(ref _selectedType, value))
            {
                return;
            }

            OnPropertyChanged(nameof(NeedsOptions));
            OnPropertyChanged(nameof(TypeHint));
        }
    }

    /// <summary>A list needs choices; every other kind of answer does not.</summary>
    public bool NeedsOptions => CustomFieldType.NeedsOptions(SelectedType.Key);

    public string TypeHint => SelectedType.Key switch
    {
        CustomFieldType.Amount => "The answer is money - kept in paisa, printed in rupees.",
        CustomFieldType.Number => "A number, for example 500 or 2.5.",
        CustomFieldType.Date => "A date, for example 2026-10-22 or 22/10/2026.",
        CustomFieldType.Select => "Write the choices separated by commas: None, A, B, C.",
        CustomFieldType.Check => "A tick box. Required means it must be ticked.",
        CustomFieldType.Photo => "The picture lives in the record's photo gallery - use the Photos… button on the form.",
        _ => "Any words, up to " + CustomFieldService.MaxAnswerLength + " letters."
    };

    public string LabelText
    {
        get => _labelText;
        set => SetProperty(ref _labelText, value);
    }

    public string OptionsText
    {
        get => _optionsText;
        set => SetProperty(ref _optionsText, value);
    }

    public bool Required
    {
        get => _required;
        set => SetProperty(ref _required, value);
    }

    /// <summary>True while an existing field is open; false while a new one is being written.</summary>
    public bool IsEditing
    {
        get => _isEditing;
        private set
        {
            if (SetProperty(ref _isEditing, value))
            {
                OnPropertyChanged(nameof(FormTitle));
                OnPropertyChanged(nameof(SaveLabel));
            }
        }
    }

    public string FormTitle => IsEditing ? "Change this field" : "Add a field";

    public string SaveLabel => IsEditing ? "Save the change" : "Add it to the form";

    public string CountText
    {
        get => _countText;
        private set => SetProperty(ref _countText, value);
    }

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

    // ------------------------------------------------------------------ loading

    private void Load()
    {
        var keep = _editingId;

        var result = _services.CustomFields.List(_user, SelectedTab.Entity, includeInactive: true, manage: true);

        if (!result.Ok || result.Value is null)
        {
            Fields.Clear();
            CountText = "no fields";
            SetStatus(result.Error ?? "The fields could not be loaded.", true);
            return;
        }

        Fields.Clear();

        foreach (var field in result.Value)
        {
            Fields.Add(new CustomFieldAdminRow(field));
        }

        var active = result.Value.Count(field => field.Active);
        var hidden = result.Value.Count - active;

        CountText = active + (active == 1 ? " field" : " fields")
                    + (hidden > 0 ? " · " + hidden + " put away" : string.Empty);

        _selectedField = keep <= 0
            ? null
            : Fields.FirstOrDefault(row => row.Field.Id == keep);

        OnPropertyChanged(nameof(SelectedField));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(ToggleLabel));
    }

    // ------------------------------------------------------------------ the form

    private void BeginNew()
    {
        _editingId = 0;
        IsEditing = false;
        _selectedField = null;

        OnPropertyChanged(nameof(SelectedField));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(ToggleLabel));

        LabelText = string.Empty;
        OptionsText = string.Empty;
        Required = false;
        SelectedType = Types[0];

        SetStatus("Type a name, choose the kind of answer, then save.", false);
    }

    private void Edit(CustomField field)
    {
        _editingId = field.Id;
        IsEditing = true;

        LabelText = field.Label;
        Required = field.Required;
        OptionsText = field.OptionsText;
        SelectedType = Types.FirstOrDefault(option => option.Key == field.Type) ?? Types[0];

        SetStatus("Change the name, the kind or the required tick - or put the field away.", false);
    }

    private void Save()
    {
        var type = SelectedType.Key;
        ServiceResult result;

        if (_editingId > 0)
        {
            result = _services.CustomFields.Update(_user, _editingId, LabelText, type, OptionsText, Required);
        }
        else
        {
            var added = _services.CustomFields.Add(
                _user, SelectedTab.Entity, LabelText, type, OptionsText, Required);

            result = added.Ok
                ? ServiceResult.Success(added.Warnings.ToArray())
                : ServiceResult.Fail(added.Error ?? "The field could not be saved.");

            _editingId = added.Value?.Id ?? 0;
        }

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The field could not be saved.", true);
            return;
        }

        var warnings = result.Warnings.Count > 0 ? " " + string.Join(" ", result.Warnings) : string.Empty;

        Load();
        IsEditing = _editingId > 0;
        SetStatus("Saved. The form shows it now." + warnings, false);
    }

    private void MoveUp() => Move(-1);

    private void MoveDown() => Move(1);

    private void Move(int direction)
    {
        if (_selectedField is null)
        {
            SetStatus("Pick a field first.", true);
            return;
        }

        _editingId = _selectedField.Field.Id;

        var result = _services.CustomFields.Move(_user, _editingId, direction);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The field could not be moved.", true);
            return;
        }

        Load();
        SetStatus("The order is saved.", false);
    }

    private void ToggleActive()
    {
        if (_selectedField is null)
        {
            SetStatus("Pick a field first.", true);
            return;
        }

        var field = _selectedField.Field;
        var result = _services.CustomFields.SetActive(_user, field.Id, !field.Active);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The field could not be changed.", true);
            return;
        }

        _editingId = field.Id;
        Load();

        SetStatus(field.Active
            ? "\"" + field.Label + "\" is put away. Its answers are still saved."
            : "\"" + field.Label + "\" is on the form again.", false);
    }

    private void UseSuggestion(CustomFieldSuggestion? suggestion)
    {
        if (suggestion is null)
        {
            return;
        }

        LabelText = suggestion.Label;
        OptionsText = suggestion.Options;
        SelectedType = Types.FirstOrDefault(option => option.Key == suggestion.Type) ?? Types[0];

        SetStatus("Filled in from \"" + suggestion.Label + "\". Change anything you like, then save.", false);
    }

    private void SetStatus(string text, bool isError)
    {
        StatusText = text;
        IsStatusError = isError;
    }
}

/// <summary>One of the three forms, as the tab row shows it.</summary>
public sealed class CustomFieldTab
{
    public CustomFieldTab(string entity, string label)
    {
        Entity = entity;
        Label = label;
    }

    public string Entity { get; }

    public string Label { get; }
}

/// <summary>One kind of answer with its English word - the type drop-down.</summary>
public sealed class CustomFieldTypeOption
{
    public CustomFieldTypeOption(string key, string label)
    {
        Key = key;
        Label = label;
    }

    public string Key { get; }

    public string Label { get; }
}

/// <summary>One field in the manager list. The row owns nothing; the field is the database row.</summary>
public sealed class CustomFieldAdminRow
{
    public CustomFieldAdminRow(CustomField field)
    {
        Field = field;
    }

    public CustomField Field { get; }

    public string Label => Field.Label;

    public string Summary => Field.Summary;

    public string StateLabel => Field.Active ? "on the form" : "put away";

    public string RequiredLabel => Field.RequiredLabel;
}
