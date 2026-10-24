using System.Collections.ObjectModel;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;

// empty-page: none - this is a dialog opened from a record card, not a page of the window
// frame, so no card of the frame is drawn for it; its own list says "no field yet".

namespace Area11.Desktop.ViewModels;

/// <summary>
/// The owner's own questions inside a form (F-42, Spec 8.4.3): the panel that appears on the
/// medicine, customer and supplier pages, built from whatever fields the owner made.
///
/// **Why a panel and not three copies of the form.** The three pages do not know what a custom field
/// is; they place this panel and hand it the selected record's id. The panel decides which boxes to
/// draw (text, number, amount, date, list, tick, photo) and asks
/// <see cref="CustomFieldService"/> to save them in one go.
///
/// **Why its own Save button.** A medicine is saved by the products page, a customer by the khata
/// page, a supplier by the supplier page - three transactions with their own rules. Adding a second
/// write inside each of them would mean three chances to leave half a record saved. The panel writes
/// its own answers in one transaction of its own (D-59), and says so on the screen.
/// </summary>
public sealed class CustomFieldsEditorViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;
    private readonly string _entity;

    private int _entityId;
    private string _statusText = string.Empty;
    private bool _isStatusError;
    private bool _canEdit;

    public CustomFieldsEditorViewModel(AppServices services, User user, string entity)
    {
        _services = services;
        _user = user;

        _entity = CustomFieldEntity.Clean(entity);
        _canEdit = services.Permissions.Can(user, CustomFieldEntity.ModuleFor(_entity));

        SaveCommand = new RelayCommand(Save);
    }

    public ObservableCollection<CustomInputRow> Rows { get; } = new();

    public RelayCommand SaveCommand { get; }

    public string Title => "Own fields";

    public string Subtitle => "Questions the owner added for the " + EntityWord + " form.";

    /// <summary>True when the owner may fill the panel; a reader only sees what is already there.</summary>
    public bool CanEdit
    {
        get => _canEdit;
        private set
        {
            if (SetProperty(ref _canEdit, value))
            {
                OnPropertyChanged(nameof(IsReadOnly));
            }
        }
    }

    public bool IsReadOnly => !CanEdit;

    /// <summary>The panel draws itself only when the form really has fields (Spec 8.4.3).</summary>
    public bool HasFields => Rows.Count > 0;

    public bool HasStatus => StatusText.Length > 0;

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (SetProperty(ref _statusText, value))
            {
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    }

    public bool IsStatusError
    {
        get => _isStatusError;
        private set => SetProperty(ref _isStatusError, value);
    }

    private string EntityWord => CustomFieldEntity.LabelFor(_entity).ToLowerInvariant();

    /// <summary>
    /// Loads the answers of one record. Called when the page's selection changes - and with 0 when
    /// there is no record, so a stale answer can never be saved onto the wrong medicine.
    /// </summary>
    public void Load(int entityId)
    {
        _entityId = entityId;
        Rows.Clear();
        StatusText = string.Empty;
        IsStatusError = false;

        if (entityId <= 0)
        {
            RaiseListState();
            return;
        }

        var fields = _services.CustomFields.List(_user, _entity);

        if (!fields.Ok || fields.Value is null)
        {
            CanEdit = false;
            RaiseListState();
            SetStatus(fields.Error ?? "The extra fields could not be loaded.", true);
            return;
        }

        var values = _services.CustomFields.Values(_user, _entity, entityId);

        if (!values.Ok || values.Value is null)
        {
            CanEdit = false;
            RaiseListState();
            SetStatus(values.Error ?? "The answers could not be loaded.", true);
            return;
        }

        foreach (var field in fields.Value)
        {
            values.Value.TryGetValue(field.Id, out var stored);
            Rows.Add(new CustomInputRow(field, stored));
        }

        CanEdit = _services.Permissions.Can(_user, CustomFieldEntity.ModuleFor(_entity));
        RaiseListState();
    }

    /// <summary>
    /// Saves every answer in one transaction. A required field left empty stops the whole save, and
    /// the message names the field - "something is missing" is not a message a shopkeeper can act on.
    /// </summary>
    private void Save()
    {
        if (_entityId <= 0)
        {
            SetStatus("Save the record first, then fill its own fields.", true);
            return;
        }

        if (Rows.Count == 0)
        {
            return;
        }

        var values = new Dictionary<int, string?>();

        foreach (var row in Rows)
        {
            if (row.IsCheck)
            {
                values[row.FieldId] = row.BoolValue ? "1" : "0";
            }
            else if (row.IsPhoto)
            {
                values[row.FieldId] = null;
            }
            else if (row.IsSelect && string.IsNullOrWhiteSpace(row.Text))
            {
                // An untouched list is "no answer", not the word that happens to be first.
                values[row.FieldId] = null;
            }
            else
            {
                values[row.FieldId] = row.Text;
            }
        }

        var result = _services.CustomFields.SaveValues(_user, _entity, _entityId, values);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The answers could not be saved.", true);
            return;
        }

        // Reload so the boxes show exactly what the database holds (1200.50 becomes 1200.5,
        // 22/10/2026 becomes 2026-10-22).
        Load(_entityId);
        SetStatus("Saved.", false);
    }

    private void RaiseListState() => OnPropertyChanged(nameof(HasFields));

    private void SetStatus(string text, bool isError)
    {
        StatusText = text;
        IsStatusError = isError;
    }
}

/// <summary>
/// One question in the panel. The row knows its kind of answer and holds the text the box shows; the
/// rules (what a number is, what an amount becomes, what a required field means) are in the service.
/// </summary>
public sealed class CustomInputRow
{
    public CustomInputRow(CustomField field, string? stored)
    {
        Field = field;

        var type = CustomFieldType.Clean(field.Type);

        IsText = type == CustomFieldType.Text;
        IsNumber = type == CustomFieldType.Number;
        IsAmount = type == CustomFieldType.Amount;
        IsDate = type == CustomFieldType.Date;
        IsSelect = type == CustomFieldType.Select;
        IsCheck = type == CustomFieldType.Check;
        IsPhoto = type == CustomFieldType.Photo;

        BoolValue = CustomFieldType.AsBool(stored);

        // An amount is shown in rupees - but a value that does not parse (an old text answer, left
        // behind when the field changed from text to amount) is shown as it is, so pressing Save says
        // what is wrong instead of quietly wiping it.
        var pretty = IsAmount ? CustomFieldType.PrettyAmount(stored) : string.Empty;

        Text = IsAmount && pretty.Length > 0 ? pretty : (stored ?? string.Empty);
    }

    public CustomField Field { get; }

    public int FieldId => Field.Id;

    /// <summary>The name the form shows; a required field carries a star so the person knows.</summary>
    public string Label => Field.Required ? Field.Label + " *" : Field.Label;

    public string Hint => Field.IsPhoto
        ? "Use the Photos… button on this page - the picture is the answer."
        : Field.TypeLabel + (Field.Required ? " · required" : string.Empty);

    public bool IsText { get; }

    public bool IsNumber { get; }

    public bool IsAmount { get; }

    public bool IsDate { get; }

    public bool IsSelect { get; }

    public bool IsCheck { get; }

    public bool IsPhoto { get; }

    /// <summary>True for the four kinds that share one text box.</summary>
    public bool IsPlain => IsText || IsNumber || IsAmount || IsDate;

    public bool IsRequired => Field.Required;

    public IReadOnlyList<string> Options => Field.Options;

    /// <summary>What the person typed (or picked). The service turns it into the stored form.</summary>
    public string Text { get; set; }

    /// <summary>The tick box of a yes/no field.</summary>
    public bool BoolValue { get; set; }
}
