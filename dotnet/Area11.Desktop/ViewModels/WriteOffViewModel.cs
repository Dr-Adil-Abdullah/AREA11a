using System.Collections.ObjectModel;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// Write-off / count correction (F-26, Spec 19.1, U-30).
///
/// The owner stands in front of the shelf. He searches the medicine, picks the batch (or lets the
/// oldest expiry be chosen), says how many and why, and sees the loss **at cost** before saving.
/// If the quantity is bigger than the book says, the screen says so in red - and still lets him
/// press Save, because the shelf is the truth and the alerting is done by the alert board.
///
/// No rule lives here: StockService refuses what must be refused (permission, zero, negative stock
/// switched off in Settings) and warns what must be warned.
/// </summary>
public sealed class WriteOffViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;

    private string _searchText = string.Empty;
    private string _qtyText = string.Empty;
    private string _unit = "unit";
    private string _reason = StockService.ReasonExpired;
    private string _note = string.Empty;
    private string _statusText = "Search a medicine, pick its batch, type the quantity and the reason, then Save.";
    private bool _isStatusError;
    private string _lossPreview = "Rs 0";
    private string _stockPreview = "Pick a medicine first.";
    private string _todayTotal = "Rs 0";
    private bool _willGoMinus;
    private Product? _selectedProduct;
    private BatchRow? _selectedBatch;

    public WriteOffViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        SearchCommand = new RelayCommand(Search);
        SaveCommand = new RelayCommand(Save);
        RefreshCommand = new RelayCommand(Load);

        Load();
    }

    public ObservableCollection<Product> Results { get; } = new();

    public ObservableCollection<BatchRow> Batches { get; } = new();

    public ObservableCollection<WriteOffRow> History { get; } = new();

    public IReadOnlyList<string> Units { get; } = new[] { "unit", "strip", "box" };

    public IReadOnlyList<string> Reasons { get; } = StockService.Reasons;

    public RelayCommand SearchCommand { get; }

    public RelayCommand SaveCommand { get; }

    public RelayCommand RefreshCommand { get; }

    public bool IsNotAllowed =>
        !_services.Permissions.Can(_user, Modules.Stock);

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public string QtyText
    {
        get => _qtyText;
        set
        {
            if (SetProperty(ref _qtyText, value))
            {
                RefreshPreview();
            }
        }
    }

    public string Unit
    {
        get => _unit;
        set
        {
            if (SetProperty(ref _unit, value))
            {
                RefreshPreview();
            }
        }
    }

    public string Reason
    {
        get => _reason;
        set
        {
            if (SetProperty(ref _reason, value))
            {
                RefreshPreview();
            }
        }
    }

    public string Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    public Product? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (SetProperty(ref _selectedProduct, value))
            {
                LoadBatches();
            }
        }
    }

    public BatchRow? SelectedBatch
    {
        get => _selectedBatch;
        set
        {
            if (SetProperty(ref _selectedBatch, value))
            {
                RefreshPreview();
            }
        }
    }

    /// <summary>The loss at purchase cost - the real money, before anything is written.</summary>
    public string LossPreview
    {
        get => _lossPreview;
        private set => SetProperty(ref _lossPreview, value);
    }

    public string StockPreview
    {
        get => _stockPreview;
        private set => SetProperty(ref _stockPreview, value);
    }

    public string TodayTotal
    {
        get => _todayTotal;
        private set => SetProperty(ref _todayTotal, value);
    }

    public bool WillGoMinus
    {
        get => _willGoMinus;
        private set => SetProperty(ref _willGoMinus, value);
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
        History.Clear();
        foreach (var row in _services.Stock.History())
        {
            History.Add(row);
        }

        TodayTotal = "Written off today: " + Money.Format(_services.Stock.LossTodayPaisa());
        OnPropertyChanged(nameof(TodayTotal));
        OnPropertyChanged(nameof(IsNotAllowed));
    }

    private void Search()
    {
        var result = _services.Products.Search(SearchText);

        Results.Clear();

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The medicines could not be searched.", true);
            return;
        }

        foreach (var product in result.Value!.Take(40))
        {
            Results.Add(product);
        }

        SetStatus(Results.Count == 0
            ? "No medicine found with that name."
            : Results.Count + " medicine(s) found - pick one.", false);
    }

    private void LoadBatches()
    {
        Batches.Clear();
        SelectedBatch = null;

        if (SelectedProduct is null)
        {
            RefreshPreview();
            return;
        }

        var result = _services.Stock.Batches(SelectedProduct.Id);

        if (result.Ok && result.Value is not null)
        {
            foreach (var batch in result.Value)
            {
                Batches.Add(batch);
            }
        }

        SelectedBatch = Batches.FirstOrDefault(batch => batch.IsExpired) ?? Batches.FirstOrDefault();
        RefreshPreview();
    }

    /// <summary>Recomputes the two numbers on screen. Pure arithmetic - it writes nothing.</summary>
    private void RefreshPreview()
    {
        if (SelectedProduct is null)
        {
            LossPreview = "Rs 0";
            StockPreview = "Pick a medicine first.";
            WillGoMinus = false;
            return;
        }

        var perUnit = SelectedProduct.UnitsPerBox > 0 && Unit == "box"
            ? SelectedProduct.UnitsPerBox
            : Unit == "strip" ? SelectedProduct.UnitsPerStrip : 1;

        Qty.TryParse(QtyText, out var qty);
        var qtyBase = qty * perUnit;

        var cost = SelectedBatch is not null && SelectedBatch.CostPaisa > 0
            ? SelectedBatch.CostPaisa
            : SelectedProduct.CostPaisa;

        var isExtra = Reason == StockService.ReasonCountExtra;
        var stockBefore = SelectedBatch?.QtyBase ?? SelectedProduct.StockBase;
        var stockAfter = isExtra ? stockBefore + qtyBase : stockBefore - qtyBase;

        LossPreview = Money.Format(Calc.Round(qtyBase * cost)) + " at cost";

        StockPreview = SelectedBatch is null
            ? "No batch on this medicine - a count correction will still record the truth."
            : "This batch: " + Qty.Format(stockBefore) + " now -> " + Qty.Format(stockAfter) + " after";

        WillGoMinus = !isExtra && stockAfter < -0.0005;
    }

    private void Save()
    {
        if (SelectedProduct is null)
        {
            SetStatus("Pick a medicine first.", true);
            return;
        }

        if (!Qty.TryParse(QtyText, out var qty) || qty <= 0)
        {
            SetStatus("The quantity must be more than zero.", true);
            return;
        }

        var input = new WriteOffInput
        {
            ProductId = SelectedProduct.Id,
            BatchId = SelectedBatch?.Id,
            QtyEntered = qty,
            Unit = Unit,
            Reason = Reason,
            Note = Note
        };

        var result = _services.Stock.WriteOff(input, _user);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The write-off could not be saved.", true);
            return;
        }

        var saved = result.Value!;
        var name = SelectedProduct.Name;

        QtyText = string.Empty;
        Note = string.Empty;
        Reason = StockService.ReasonExpired;

        Load();
        LoadBatches();

        var message = name + ": " + Qty.Format(Math.Abs(saved.QtyBase)) + " written off, loss "
                      + Money.Format(saved.LossPaisa) + " at cost.";

        if (saved.Warnings.Count > 0)
        {
            SetStatus(message + " " + string.Join(" ", saved.Warnings), true);
        }
        else
        {
            SetStatus(message, false);
        }
    }

    private void SetStatus(string message, bool isError)
    {
        StatusText = message;
        IsStatusError = isError;
    }
}
