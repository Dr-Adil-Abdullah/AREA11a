using System.Collections.ObjectModel;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// The screen where the owner makes his own fields (F-42, Spec 2.2 &amp; 8.4).
///
/// Six forms: three masters (Medicines, Customers, Suppliers) and three documents (Bills,
/// Purchases, Expenses). A field is a name, a kind
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
    /// Only a successful authorized empty list gets an empty hint. A failed read is unknown,
    /// not proof that this form has no definitions.
    /// </summary>
    public EmptyHint? EmptyHint => EmptyPage.When(HasNoFields, "Own fields");

    private readonly AppServices _services;
    private readonly User _user;

    private CustomFieldTab _selectedTab;
    private readonly IReadOnlyList<CustomFieldTypeOption> _typesAll;
    private readonly IReadOnlyList<CustomFieldTypeOption> _typesDocuments;
    private readonly IReadOnlyList<CustomFieldSuggestion> _suggestionsDocuments;
    private CustomFieldAdminRow? _selectedField;
    private CustomFieldTypeOption _selectedType;
    private string _labelText = string.Empty;
    private string _optionsText = string.Empty;
    private string _helpText = string.Empty;
    private bool _required;
    private bool _isEditing;
    private int _editingId;
    private bool _canManage;
    private bool _lastLoadSucceeded;
    private bool _updatingList;
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
            new(CustomFieldEntity.Supplier, "Suppliers"),
            new(CustomFieldEntity.Sale, "Bills"),
            new(CustomFieldEntity.Purchase, "Purchases"),
            new(CustomFieldEntity.Expense, "Expenses")
        };

        // Documents have no photo gallery, so their kind list and quick-add leave photo out
        // (Portion 18). The service refuses a photo definition on a document form as well, so an
        // older window or a direct call cannot sneak one in either.
        _typesAll = CustomFieldType.All
            .Select(type => new CustomFieldTypeOption(type, CustomFieldType.LabelFor(type)))
            .ToList();
        _typesDocuments = _typesAll
            .Where(option => option.Key != CustomFieldType.Photo)
            .ToList();
        _suggestionsDocuments = CustomFieldSuggestion.All
            .Where(suggestion => suggestion.Type != CustomFieldType.Photo)
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

    /// <summary>The kind list and quick-add follow the tab: no photo on a document form.</summary>
    public IReadOnlyList<CustomFieldTypeOption> Types =>
        CustomFieldEntity.IsDocument(SelectedTab.Entity) ? _typesDocuments : _typesAll;

    public IReadOnlyList<CustomFieldSuggestion> Suggestions =>
        CustomFieldEntity.IsDocument(SelectedTab.Entity) ? _suggestionsDocuments : CustomFieldSuggestion.All;

    public ObservableCollection<CustomFieldAdminRow> Fields { get; } = new();

    public RelayCommand NewCommand { get; }

    public RelayCommand SaveCommand { get; }

    public RelayCommand CancelCommand { get; }

    public RelayCommand MoveUpCommand { get; }

    public RelayCommand MoveDownCommand { get; }

    public RelayCommand ToggleCommand { get; }

    public RelayCommand<CustomFieldSuggestion> UseSuggestionCommand { get; }

    public bool CanManage
    {
        get => _canManage;
        private set
        {
            if (SetProperty(ref _canManage, value)) OnPropertyChanged(nameof(IsNotAllowed));
        }
    }

    public bool HasNoFields => _lastLoadSucceeded && CanManage && Fields.Count == 0;

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

            OnPropertyChanged(nameof(Types));
            OnPropertyChanged(nameof(Suggestions));
            OnPropertyChanged(nameof(DocumentTabNote));
            OnPropertyChanged(nameof(HasDocumentTabNote));
            ResetEditor();
            Load();
        }
    }

    public CustomFieldAdminRow? SelectedField
    {
        get => _selectedField;
        set
        {
            if (_updatingList) return;
            if (value is not null)
            {
                if (!CanManage || !Fields.Contains(value) || value.Field.Entity != SelectedTab.Entity) return;
                if (!RefreshAccess() || !Fields.Contains(value)) return;
            }
            if (!SetProperty(ref _selectedField, value)) return;
            if (value is null) ResetEditor();

            OnPropertyChanged(nameof(ToggleLabel));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(SelectedFieldKey));

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

    /// <summary>Why the kind list on a document tab has no photo: documents have no gallery.</summary>
    public bool HasDocumentTabNote => CustomFieldEntity.IsDocument(SelectedTab.Entity);

    public string DocumentTabNote =>
        "Bills, purchases and expenses carry text, number, amount, date, list and yes/no answers. " +
        "Photos stay on medicines, customers and suppliers, which have galleries.";

    public string TypeHint => SelectedType.Key switch
    {
        CustomFieldType.Amount => "The answer is money - kept in paisa, printed in rupees.",
        CustomFieldType.Number => "A number, for example 500 or 2.5.",
        CustomFieldType.Date => "A date, for example 2026-10-22 or 22/10/2026.",
        CustomFieldType.Select => "Write the choices separated by commas: None, A, B, C.",
        CustomFieldType.Check => "A tick box. Required means it must be ticked.",
        CustomFieldType.Photo => "The picture lives in the record's gallery - add it with the Photos… button, or choose one on the new-record form. Required means the record needs a picture.",
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

    /// <summary>Optional one-line guidance; it is metadata, not a custom validation script.</summary>
    public string HelpText
    {
        get => _helpText;
        set => SetProperty(ref _helpText, value);
    }

    public string SelectedFieldKey => _selectedField?.Field.Key ?? "Assigned when the field is saved";

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

    /// <summary>The shell Refresh action uses a deliberate fresh read, not a cached selection.</summary>
    public void Refresh() => Load();

    private void Load()
    {
        var keep = _editingId;
        var result = _services.CustomFields.List(_user, SelectedTab.Entity, includeInactive: true, manage: true);
        if (!result.Ok || result.Value is null)
        {
            ClearManager(result.Error ?? "The fields could not be loaded.");
            return;
        }
        CanManage = true;
        _lastLoadSucceeded = true;
        ReplaceRows(result.Value, keep);
        if (_selectedField is not null) Edit(_selectedField.Field);
        else ResetEditor();
        SetStatus("Fields loaded. Pick a field or add a new one.", false);
    }

    /// <summary>
    /// Current management read, with persisted selected-definition comparison before commands.
    /// An unchanged tick never replaces rows or overwrites unsaved editor text. Not an atomic CAS.
    /// </summary>
    public bool RefreshAccess()
    {
        var result = _services.CustomFields.List(_user, SelectedTab.Entity, includeInactive: true, manage: true);
        if (!result.Ok || result.Value is null)
        {
            ClearManager(result.Error ?? "Field access needs checking. Refresh before editing.");
            return false;
        }
        if (!_lastLoadSucceeded)
        {
            CanManage = true;
            _lastLoadSucceeded = true;
            ReplaceRows(result.Value, 0);
            ResetEditor();
            SetStatus("Access restored. Pick a field again; discarded edits were not saved.", false);
            return true;
        }
        var current = result.Value.FirstOrDefault(field => field.Id == _editingId);
        if (_editingId > 0 && !CustomFieldDefinitionState.Matches(_selectedField?.Field, current))
        {
            ReplaceRows(result.Value, 0);
            ResetEditor();
            SetStatus("The selected field changed or disappeared. Unsaved edits were discarded; pick it again and review before repeating the action.", true);
            return false;
        }
        if (Fields.Count != result.Value.Count || Fields.Where((row, index) =>
            !CustomFieldDefinitionState.Matches(row.Field, result.Value[index])).Any())
            ReplaceRows(result.Value, _editingId);
        return true;
    }

    private void ClearManager(string error)
    {
        CanManage = false;
        _lastLoadSucceeded = false;
        ReplaceRows(Array.Empty<CustomField>(), 0);
        ResetEditor();
        CountText = "Fields not loaded";
        RaiseListState();
        SetStatus(error, true);
    }

    private void ReplaceRows(IReadOnlyList<CustomField> fields, int keep)
    {
        _updatingList = true;
        try
        {
            Fields.Clear();
            foreach (var field in fields) Fields.Add(new CustomFieldAdminRow(field));
            _selectedField = keep <= 0 ? null : Fields.FirstOrDefault(row => row.Field.Id == keep);
            var active = fields.Count(field => field.Active);
            var hidden = fields.Count - active;
            CountText = active + (active == 1 ? " field" : " fields")
                + (hidden > 0 ? " · " + hidden + " put away" : string.Empty);
            RaiseSelection();
            RaiseListState();
        }
        finally { _updatingList = false; }
    }

    private void RaiseListState()
    {
        OnPropertyChanged(nameof(HasNoFields));
        OnPropertyChanged(nameof(EmptyHint));
    }

    private void RaiseSelection()
    {
        OnPropertyChanged(nameof(SelectedField));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedFieldKey));
        OnPropertyChanged(nameof(ToggleLabel));
    }

    // ------------------------------------------------------------------ the form

    private void BeginNew()
    {
        if (!RefreshAccess()) return;
        ResetEditor();
        SetStatus("Type a name, choose the kind of answer, then save.", false);
    }

    private void ResetEditor()
    {
        _editingId = 0;
        IsEditing = false;
        _selectedField = null;
        LabelText = string.Empty;
        OptionsText = string.Empty;
        HelpText = string.Empty;
        Required = false;
        SelectedType = Types[0];
        RaiseSelection();
    }

    private void Edit(CustomField field)
    {
        _editingId = field.Id;
        IsEditing = true;

        LabelText = field.Label;
        Required = field.Required;
        OptionsText = field.OptionsText;
        HelpText = field.HelpText;
        SelectedType = Types.FirstOrDefault(option => option.Key == field.Type) ?? Types[0];

        SetStatus("Change the name, the kind or the required tick - or put the field away.", false);
    }

    private void Save()
    {
        if (!RefreshAccess()) return;
        var type = SelectedType.Key;
        ServiceResult result;

        if (_editingId > 0)
        {
            result = _services.CustomFields.Update(
                _user, _editingId, LabelText, type, OptionsText, Required, HelpText);
        }
        else
        {
            var added = _services.CustomFields.Add(
                _user, SelectedTab.Entity, LabelText, type, OptionsText, Required, HelpText);

            result = added.Ok
                ? ServiceResult.Success(added.Warnings.ToArray())
                : ServiceResult.Fail(added.Error ?? "The field could not be saved.");

            _editingId = added.Value?.Id ?? 0;
        }

        if (!result.Ok)
        {
            RefreshAccess();
            SetStatus(result.Error ?? "The field could not be saved.", true);
            return;
        }

        var warnings = result.Warnings.Count > 0 ? " " + string.Join(" ", result.Warnings) : string.Empty;

        FinishWrite(_editingId, "Saved. The form shows it now." + warnings);
    }

    private void MoveUp() => Move(-1);

    private void MoveDown() => Move(1);

    private void Move(int direction)
    {
        if (!RefreshAccess()) return;
        if (_selectedField is null)
        {
            SetStatus("Pick a field first.", true);
            return;
        }

        _editingId = _selectedField.Field.Id;

        var result = _services.CustomFields.Move(_user, _editingId, direction);

        if (!result.Ok)
        {
            RefreshAccess();
            SetStatus(result.Error ?? "The field could not be moved.", true);
            return;
        }

        FinishWrite(_editingId, "The order is saved.");
    }

    private void ToggleActive()
    {
        if (!RefreshAccess()) return;
        if (_selectedField is null)
        {
            SetStatus("Pick a field first.", true);
            return;
        }

        var field = _selectedField.Field;
        var result = _services.CustomFields.SetActive(_user, field.Id, !field.Active);

        if (!result.Ok)
        {
            RefreshAccess();
            SetStatus(result.Error ?? "The field could not be changed.", true);
            return;
        }

        _editingId = field.Id;
        FinishWrite(field.Id, field.Active
            ? "\"" + field.Label + "\" is put away. Its answers are still saved."
            : "\"" + field.Label + "\" is on the form again.");
    }

    private void UseSuggestion(CustomFieldSuggestion? suggestion)
    {
        if (!RefreshAccess()) return;
        if (suggestion is null || !Types.Any(option => option.Key == suggestion.Type))
        {
            return;
        }

        LabelText = suggestion.Label;
        OptionsText = suggestion.Options;
        SelectedType = Types.FirstOrDefault(option => option.Key == suggestion.Type) ?? Types[0];

        SetStatus("Filled in from \"" + suggestion.Label + "\". Change anything you like, then save.", false);
    }

    private void FinishWrite(int savedId, string success)
    {
        Load();
        if (!_lastLoadSucceeded || _selectedField?.Field.Id != savedId)
        {
            SetStatus("SAVED. The field change was stored, but could not be reloaded. Do not repeat the action; refresh to check.", true);
            return;
        }
        SetStatus(success, false);
    }

    private void SetStatus(string text, bool isError)
    {
        StatusText = text;
        IsStatusError = isError;
    }
}

/// <summary>One of the six forms (three master, three document), as the tab row shows it.</summary>
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
