using System.Collections.ObjectModel;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// Stock-take (F-26, Spec 19.2). The shop has closed and the owner walks the shelves.
///
/// Left: the sheet of the current session - one row per live batch, with **book / counted /
/// difference / value** side by side and an "only differences" switch for the second pass.
/// Right: the box where one number is typed, the "found on the shelf" search for medicine the book
/// does not know, and the two decisions - Apply (owner only, once) or Cancel (with a reason).
///
/// Nothing is corrected while counting: <see cref="StockTakeService.Count"/> only writes the sheet.
/// Stock moves at Apply, and only there.
/// </summary>
public sealed class StockTakeViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;

    private string _room = string.Empty;
    private string _openNote = string.Empty;
    private string _qtyText = string.Empty;
    private string _unit = "unit";
    private string _lineNote = string.Empty;
    private string _cancelReason = string.Empty;
    private string _foundText = string.Empty;
    private string _statusText = "Open a stock-take to freeze the book, then count the shelves.";
    private bool _isStatusError;
    private bool _differencesOnly;
    private StockTakeSession? _session;
    private StockTakeLine? _selectedLine;
    private Product? _foundProduct;

    public StockTakeViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        OpenCommand = new RelayCommand(OpenSession);
        CountCommand = new RelayCommand(Count);
        ClearCommand = new RelayCommand(ClearCount);
        ApplyCommand = new RelayCommand(Apply);
        CancelCommand = new RelayCommand(Cancel);
        RefreshCommand = new RelayCommand(Load);
        FoundSearchCommand = new RelayCommand(SearchFound);
        AddFoundCommand = new RelayCommand(AddFound);

        Load();
    }

    public ObservableCollection<StockTakeLine> Lines { get; } = new();

    public ObservableCollection<StockTakeSession> History { get; } = new();

    public ObservableCollection<Product> Found { get; } = new();

    public IReadOnlyList<string> Units { get; } = new[] { "unit", "strip", "box" };

    public RelayCommand OpenCommand { get; }

    public RelayCommand CountCommand { get; }

    public RelayCommand ClearCommand { get; }

    public RelayCommand ApplyCommand { get; }

    public RelayCommand CancelCommand { get; }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand FoundSearchCommand { get; }

    public RelayCommand AddFoundCommand { get; }

    public bool IsOwner => _user.IsOwner;

    public bool IsNotOwner => !_user.IsOwner;

    public bool CanManage => _user.CanManage;

    public string Room
    {
        get => _room;
        set => SetProperty(ref _room, value);
    }

    public string OpenNote
    {
        get => _openNote;
        set => SetProperty(ref _openNote, value);
    }

    public string QtyText
    {
        get => _qtyText;
        set => SetProperty(ref _qtyText, value);
    }

    public string Unit
    {
        get => _unit;
        set => SetProperty(ref _unit, value);
    }

    public string LineNote
    {
        get => _lineNote;
        set => SetProperty(ref _lineNote, value);
    }

    public string CancelReason
    {
        get => _cancelReason;
        set => SetProperty(ref _cancelReason, value);
    }

    public string FoundText
    {
        get => _foundText;
        set => SetProperty(ref _foundText, value);
    }

    public Product? FoundProduct
    {
        get => _foundProduct;
        set => SetProperty(ref _foundProduct, value);
    }

    public bool DifferencesOnly
    {
        get => _differencesOnly;
        set
        {
            if (SetProperty(ref _differencesOnly, value))
            {
                ReloadSheet();
            }
        }
    }

    public StockTakeLine? SelectedLine
    {
        get => _selectedLine;
        set
        {
            if (SetProperty(ref _selectedLine, value))
            {
                OnPropertyChanged(nameof(SelectedCaption));
            }
        }
    }

    public string SelectedCaption => _selectedLine is null
        ? "Pick a row on the sheet first."
        : _selectedLine.ProductName + " · " + _selectedLine.WhereLabel
          + " · book " + _selectedLine.BookLabel;

    public string SessionCaption => _session is null
        ? "No stock-take yet."
        : _session.Code + " · " + _session.StatusLabel + " · " + _session.ProgressLabel;

    public string SummaryCaption => _session is null
        ? string.Empty
        : "Shortage at cost: " + _session.ShortLabel + "   ·   Extra at cost: " + _session.ExtraLabel;

    public bool IsOpen => _session?.IsOpen == true;

    public string CountedAtLabel => _selectedLine is null ? string.Empty : _selectedLine.CountedByLabel;

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
        var current = _services.StockTake.Current();

        // Keep the same session on screen when it is still the open one.
        _session = current;

        History.Clear();
        foreach (var row in _services.StockTake.History())
        {
            History.Add(row);
        }

        ReloadSheet();
        RaiseSession();
    }

    private void ReloadSheet()
    {
        Lines.Clear();

        if (_session is null)
        {
            SelectedLine = null;
            return;
        }

        foreach (var line in _services.StockTake.Sheet(_session.Id, DifferencesOnly))
        {
            Lines.Add(line);
        }

        // The service has the freshest totals; re-read the session so the labels move with the count.
        _session = _services.StockTake.History().FirstOrDefault(row => row.Id == _session.Id) ?? _session;

        SelectedLine = Lines.FirstOrDefault(line => !line.IsCounted)
            ?? Lines.FirstOrDefault(line => line.Id == SelectedLine?.Id)
            ?? Lines.FirstOrDefault();

        RaiseSession();
    }

    private void RaiseSession()
    {
        OnPropertyChanged(nameof(SessionCaption));
        OnPropertyChanged(nameof(SummaryCaption));
        OnPropertyChanged(nameof(IsOpen));
        OnPropertyChanged(nameof(SelectedCaption));
        OnPropertyChanged(nameof(CountedAtLabel));
    }

    private void OpenSession()
    {
        var result = _services.StockTake.Open(_user, Room, null, OpenNote);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The stock-take could not be opened.", true);
            return;
        }

        var opened = result.Value!;
        Room = string.Empty;
        OpenNote = string.Empty;
        Load();
        SetStatus(opened.Code + " opened - " + opened.TotalLines + " batch(es) frozen. Count the shelves.", false);
    }

    private void Count()
    {
        if (_selectedLine is null)
        {
            SetStatus("Pick a row on the sheet first.", true);
            return;
        }

        if (!Qty.TryParse(QtyText, out var qty) || qty < 0)
        {
            SetStatus("Type the number you counted (0 is allowed, letters are not).", true);
            return;
        }

        var result = _services.StockTake.Count(_user, _selectedLine.Id, qty, Unit, LineNote);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The count could not be saved.", true);
            return;
        }

        var caption = _selectedLine.ProductName;

        QtyText = string.Empty;
        LineNote = string.Empty;
        ReloadSheet();
        SetStatus(caption + " counted.", false);
    }

    private void ClearCount()
    {
        if (_selectedLine is null)
        {
            SetStatus("Pick a row first.", true);
            return;
        }

        var result = _services.StockTake.ClearCount(_user, _selectedLine.Id);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The count could not be cleared.", true);
            return;
        }

        QtyText = string.Empty;
        ReloadSheet();
        SetStatus("Count cleared - the row is back to 'not counted'.", false);
    }

    private void Apply()
    {
        if (_session is null)
        {
            SetStatus("There is no stock-take to apply.", true);
            return;
        }

        var result = _services.StockTake.Apply(_user, _session.Id, OpenNote);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The stock-take could not be applied.", true);
            return;
        }

        var applied = result.Value!;
        var message = applied.Code + " applied: " + applied.DiffCount + " difference(s), short "
                      + Money.Format(applied.ShortPaisa) + ", extra " + Money.Format(applied.ExtraPaisa) + ".";

        if (applied.Warnings.Count > 0)
        {
            SetStatus(message + " " + string.Join(" ", applied.Warnings), true);
        }
        else
        {
            SetStatus(message, false);
        }

        Load();
    }

    private void Cancel()
    {
        if (_session is null)
        {
            SetStatus("There is no stock-take to cancel.", true);
            return;
        }

        var result = _services.StockTake.Cancel(_user, _session.Id, CancelReason);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The stock-take could not be cancelled.", true);
            return;
        }

        CancelReason = string.Empty;
        Load();
        SetStatus("The stock-take was cancelled. Nothing in stock changed.", false);
    }

    private void SearchFound()
    {
        Found.Clear();

        if (_session is null)
        {
            SetStatus("Open a stock-take first.", true);
            return;
        }

        foreach (var product in _services.StockTake.NotOnSheet(_session.Id, FoundText))
        {
            Found.Add(product);
        }

        SetStatus(Found.Count == 0
            ? "No other medicine matches that name."
            : Found.Count + " medicine(s) not on the sheet - pick one and press Add.", false);
    }

    private void AddFound()
    {
        if (_session is null || FoundProduct is null)
        {
            SetStatus("Pick a medicine from the list first.", true);
            return;
        }

        var result = _services.StockTake.AddFound(_user, _session.Id, FoundProduct.Id);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The medicine could not be added to the sheet.", true);
            return;
        }

        var name = FoundProduct.Name;
        FoundProduct = null;
        Found.Clear();
        FoundText = string.Empty;
        ReloadSheet();

        // The new line is the last one added; select it so the owner can type the number at once.
        SelectedLine = Lines.LastOrDefault(line => line.ProductName == name && line.BatchId is null) ?? SelectedLine;
        SetStatus(name + " added to the sheet - it will get a GINTI batch at Apply.", false);
    }

    private void SetStatus(string message, bool isError)
    {
        StatusText = message;
        IsStatusError = isError;
    }
}
