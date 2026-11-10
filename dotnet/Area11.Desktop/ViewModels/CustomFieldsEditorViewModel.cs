using System.Collections.ObjectModel;
using System.Globalization;
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
/// Product, customer and supplier answers are saved with their record in one transaction on the
/// new/edit panels; the separate save button remains for the read panel of existing records.
/// </summary>
public sealed class CustomFieldsEditorViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;
    private readonly string _entity;
    private readonly bool _saveWithParent;

    private int _entityId;
    private string _statusText = string.Empty;
    private bool _isStatusError;
    private bool _canEdit;

    public CustomFieldsEditorViewModel(AppServices services, User user, string entity, bool saveWithParent = false)
    {
        _services = services;
        _user = user;

        _entity = CustomFieldEntity.Clean(entity);
        _saveWithParent = saveWithParent;
        _canEdit = services.Permissions.Can(user, CustomFieldEntity.ModuleFor(_entity));

        SaveCommand = new RelayCommand(Save);
    }

    public ObservableCollection<CustomInputRow> Rows { get; } = new();

    public RelayCommand SaveCommand { get; }

    public string Subtitle => "Questions the owner added for the " + EntityWord + " form."
        + (_saveWithParent ? " Saved together with the record." : " Saved separately.");

    public bool ShowSeparateSave => !_saveWithParent;
    public bool LoadedSuccessfully { get; private set; }

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
    public void Load(int entityId) => LoadCore(entityId, false);

    public void LoadNew() => LoadCore(0, true);

    private void LoadCore(int entityId, bool forNewRecord)
    {
        LoadedSuccessfully = false;
        _entityId = entityId;
        Rows.Clear();
        StatusText = string.Empty;
        IsStatusError = false;

        if (entityId <= 0 && !forNewRecord)
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

        var answers = new Dictionary<int, string>();
        if (entityId > 0)
        {
            var values = _services.CustomFields.Values(_user, _entity, entityId);
            if (!values.Ok || values.Value is null)
            {
                CanEdit = false;
                RaiseListState();
                SetStatus(values.Error ?? "The answers could not be loaded.", true);
                return;
            }
            answers = values.Value;
        }

        foreach (var field in fields.Value)
        {
            answers.TryGetValue(field.Id, out var stored);
            Rows.Add(new CustomInputRow(field, stored));
        }

        LoadedSuccessfully = true;
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

        var values = AnswerValues();

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

    public IReadOnlyDictionary<int, string?> AnswerValues()
    {
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

        return values;
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
public sealed class CustomInputRow : ObservableObject
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

    /// <summary>The name the form shows; a required field carries a star so the person knows.
    /// A photo never carries one - it is never required, even on a legacy row.</summary>
    public string Label => Field.Required && !IsPhoto ? Field.Label + " *" : Field.Label;

    public string Hint => !string.IsNullOrWhiteSpace(Field.HelpText)
        ? Field.HelpText
        : Field.IsPhoto
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

    private string _text = string.Empty;

    /// <summary>What the person typed (or picked). The service turns it into the stored form.</summary>
    public string Text
    {
        get => _text;
        set
        {
            if (SetProperty(ref _text, value ?? string.Empty)) OnPropertyChanged(nameof(PickedDate));
        }
    }

    /// <summary>
    /// Calendar is an optional input aid. Never erase an unparseable legacy value: it remains in
    /// the text box for the existing service validation to explain on Save. Clear via the text box.
    /// </summary>
    public DateTime? PickedDate
    {
        get
        {
            var formats = new[] { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy" };
            return DateTime.TryParseExact(Text.Trim(), formats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date) ? date : null;
        }
        set
        {
            if (value is not null) Text = value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>The tick box of a yes/no field.</summary>
    public bool BoolValue { get; set; }
}
