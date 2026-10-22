using System.Collections.ObjectModel;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// The barcodes of one medicine (F-43, Spec 8.1.5, 35.6) - the window behind the "Barcodes…" button.
///
/// The owner scans a box, the digit lands in the box on the left, he says **which pack** it belongs
/// to and presses Add. Nothing is ever deleted (D-00b): a code that is no longer printed is put away
/// and stays in the list, because the shop must still be able to explain a sale from the old box.
///
/// No rule lives here: the length, the spaces, the duplicate and the pack are checked by
/// <see cref="BarcodeService"/>.
/// </summary>
public sealed class BarcodeListViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;
    private readonly int _productId;

    private string _codeText = string.Empty;
    private string _noteText = string.Empty;
    private BarcodePackOption _selectedPack;
    private BarcodeRow? _selected;
    private bool _includeHidden;
    private bool _canEdit;
    private string _statusText = "Scan the box, say which pack it is, then press Add.";
    private bool _isStatusError;
    private string _countText = "no barcodes";

    public BarcodeListViewModel(AppServices services, User user, int productId, string productName)
    {
        _services = services;
        _user = user;
        _productId = productId;
        ProductName = productName;

        Packs = new List<BarcodePackOption>
        {
            new(BarcodePack.Base, "Unit (tablet / ml)"),
            new(BarcodePack.Strip, "Strip"),
            new(BarcodePack.Box, "Box")
        };

        _selectedPack = Packs[0];
        _canEdit = services.Permissions.Can(user, Modules.Products);

        AddCommand = new RelayCommand(Add);
        RefreshCommand = new RelayCommand(Load);
        PutAwayCommand = new RelayCommand(PutAway);
        RestoreCommand = new RelayCommand(Restore);
        ShowHiddenCommand = new RelayCommand(ToggleHidden);

        Load();
    }

    public string ProductName { get; }

    public string Title => "Barcodes - " + ProductName;

    public string Subtitle => "One code per pack. Scanning a box code adds a box at the counter.";

    public IReadOnlyList<BarcodePackOption> Packs { get; }

    public ObservableCollection<BarcodeRow> Rows { get; } = new();

    public RelayCommand AddCommand { get; }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand PutAwayCommand { get; }

    public RelayCommand RestoreCommand { get; }

    public RelayCommand ShowHiddenCommand { get; }

    public bool CanEdit
    {
        get => _canEdit;
        private set
        {
            if (SetProperty(ref _canEdit, value))
            {
                OnPropertyChanged(nameof(IsReadOnly));
                OnPropertyChanged(nameof(CanPutAway));
                OnPropertyChanged(nameof(CanRestore));
            }
        }
    }

    public bool IsReadOnly => !CanEdit;

    public string CodeText
    {
        get => _codeText;
        set => SetProperty(ref _codeText, value);
    }

    public string NoteText
    {
        get => _noteText;
        set => SetProperty(ref _noteText, value);
    }

    public BarcodePackOption SelectedPack
    {
        get => _selectedPack;
        set
        {
            if (value is not null)
            {
                SetProperty(ref _selectedPack, value);
            }
        }
    }

    public BarcodeRow? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                OnPropertyChanged(nameof(CanPutAway));
                OnPropertyChanged(nameof(CanRestore));
            }
        }
    }

    public bool CanPutAway => CanEdit && _selected is { Item.Active: true };

    public bool CanRestore => CanEdit && _selected is { Item.Active: false };

    public bool IncludeHidden
    {
        get => _includeHidden;
        private set
        {
            if (SetProperty(ref _includeHidden, value))
            {
                OnPropertyChanged(nameof(HiddenToggleLabel));
            }
        }
    }

    public string HiddenToggleLabel => IncludeHidden ? "Hide the put-away codes" : "Show put-away codes";

    /// <summary>True when the medicine has no codes yet - the list is replaced by one hint.</summary>
    public bool HasNoCodes => Rows.Count == 0;

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

    public void Load()
    {
        var result = _services.Barcodes.List(_user, _productId, IncludeHidden);

        if (!result.Ok || result.Value is null)
        {
            Rows.Clear();
            CanEdit = false;
            CountText = "no barcodes";
            OnPropertyChanged(nameof(HasNoCodes));
            SetStatus(result.Error ?? "The barcodes could not be loaded.", true);
            return;
        }

        Rows.Clear();

        foreach (var item in result.Value)
        {
            Rows.Add(new BarcodeRow(item));
        }

        var active = result.Value.Count(item => item.Active);
        var hidden = result.Value.Count - active;

        CountText = active + (active == 1 ? " code" : " codes")
                    + (hidden > 0 ? " · " + hidden + " put away" : string.Empty);

        if (Rows.Count > 0)
        {
            Selected = Rows.FirstOrDefault(row => row.Item.Active) ?? Rows[0];
        }

        CanEdit = _services.Permissions.Can(_user, Modules.Products);
        OnPropertyChanged(nameof(HasNoCodes));
        OnPropertyChanged(nameof(CanPutAway));
        OnPropertyChanged(nameof(CanRestore));
    }

    private void Add()
    {
        var added = _services.Barcodes.Add(_user, _productId, CodeText, SelectedPack.Key, NoteText);

        if (!added.Ok || added.Value is null)
        {
            SetStatus(added.Error ?? "The barcode could not be added.", true);
            return;
        }

        var caption = added.Value.Barcode + " · " + added.Value.PackLabel;

        CodeText = string.Empty;
        NoteText = string.Empty;
        Load();

        if (added.Value.Id > 0)
        {
            Selected = Rows.FirstOrDefault(row => row.Item.Id == added.Value.Id) ?? Selected;
        }

        SetStatus("Added: " + caption + ". Scan it at the counter to check it.", false);
    }

    private void PutAway()
    {
        if (_selected is null)
        {
            SetStatus("Pick a code first.", true);
            return;
        }

        var code = _selected.Item.Barcode;
        var result = _services.Barcodes.SetActive(_user, _selected.Item.Id, false);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The code could not be put away.", true);
            return;
        }

        Load();
        SetStatus(code + " is put away. The row stays, and an old box can still be explained.", false);
    }

    private void Restore()
    {
        if (_selected is null)
        {
            SetStatus("Pick a code first.", true);
            return;
        }

        var result = _services.Barcodes.SetActive(_user, _selected.Item.Id, true);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The code could not be switched on.", true);
            return;
        }

        Load();
        SetStatus("The code works at the counter again.", false);
    }

    private void ToggleHidden()
    {
        IncludeHidden = !IncludeHidden;
        Load();
    }

    private void SetStatus(string text, bool isError)
    {
        StatusText = text;
        IsStatusError = isError;
    }
}

/// <summary>One pack in the drop-down: the word the database keeps and the word the owner reads.</summary>
public sealed class BarcodePackOption
{
    public BarcodePackOption(string key, string label)
    {
        Key = key;
        Label = label;
    }

    public string Key { get; }

    public string Label { get; }
}

/// <summary>One code in the list.</summary>
public sealed class BarcodeRow
{
    public BarcodeRow(ProductBarcode item)
    {
        Item = item;
    }

    public ProductBarcode Item { get; }

    public string Code => Item.Barcode;

    public string PackLabel => Item.PackLabel;

    public string NoteLabel => Item.NoteLabel;

    public string StateLabel => Item.StateLabel;
}
