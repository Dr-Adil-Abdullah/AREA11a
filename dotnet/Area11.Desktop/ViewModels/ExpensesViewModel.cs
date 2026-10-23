using System.Collections.ObjectModel;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// Expenses and owner drawings (F-32). The page that closes the last hole in the drawer: rent, tea,
/// a broken shutter, or money the owner took for himself.
///
/// Two ideas decide the whole screen:
///   • **an expense is not a purchase.** No stock arrives and no supplier's account moves; the money
///     simply leaves the till. That is why this page is separate from Purchases.
///   • **cash and goods are different.** Cash lowers the drawer count; goods lower the shelf at
///     *cost* and leave the drawer alone. The screen shows which one is about to happen.
///
/// No rule lives here: ExpenseService refuses a zero amount, a missing title or a cashier trying to
/// take the owner's money, and warns when the entry cannot reach a drawer count.
/// </summary>
public sealed class ExpensesViewModel : ObservableObject, IEmptyPage
{
    /// <summary>
    /// Why this page has nothing on it, in the shop's words (v0.33.0, D-89). **Null when it
    /// has rows**, so nothing in this page has to keep a second flag in step with its list.
    /// </summary>
    public EmptyHint? EmptyHint => EmptyPage.When(Rows.Count == 0, "Expenses");

    private readonly AppServices _services;
    private readonly User _user;

    private string _title = string.Empty;
    private string _amountText = string.Empty;
    private string _expenseNote = string.Empty;
    private CategoryOption _category = new("other", "Other");

    private string _drawingKind = "Cash";
    private string _drawingAmountText = string.Empty;
    private string _drawingNote = string.Empty;

    private string _searchText = string.Empty;
    private string _qtyText = string.Empty;
    private string _unit = "unit";
    private string _valuePreview = "Pick a medicine first.";
    private string _stockPreview = string.Empty;
    private Product? _selectedProduct;
    private BatchRow? _selectedBatch;

    private string _todayText = "Rs 0";
    private string _statusText = "Type what was spent, or what the owner took, then press Save.";
    private bool _isStatusError;

    public ExpensesViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        SaveExpenseCommand = new RelayCommand(SaveExpense);
        SaveDrawingCommand = new RelayCommand(SaveDrawing);
        SearchCommand = new RelayCommand(Search);
        RefreshCommand = new RelayCommand(Load);

        Load();
    }

    public ObservableCollection<CashOutRow> Rows { get; } = new();

    public ObservableCollection<Product> Results { get; } = new();

    public ObservableCollection<BatchRow> Batches { get; } = new();

    /// <summary>The eight categories the shop actually uses; "Other" is last so nothing is ever blocked.</summary>
    public IReadOnlyList<CategoryOption> Categories { get; } = ExpenseService.Categories
        .Select(key => new CategoryOption(key, ExpenseService.CategoryLabel(key)))
        .ToList();

    public IReadOnlyList<string> DrawingKinds { get; } = new[] { "Cash", "Goods" };

    public IReadOnlyList<string> Units { get; } = new[] { "unit", "strip", "box" };

    public RelayCommand SaveExpenseCommand { get; }

    public RelayCommand SaveDrawingCommand { get; }

    public RelayCommand SearchCommand { get; }

    public RelayCommand RefreshCommand { get; }

    /// <summary>Recording an expense is money going out of the till, so it needs the Cash permission.</summary>
    public bool CanRecord => _services.Permissions.Can(_user, Modules.Cash);

    public bool IsNotAllowed => !CanRecord;

    /// <summary>Only the owner may take money or goods out of his own shop (D-42).</summary>
    public bool IsOwner => _user.IsOwner;

    public bool IsCashDrawing => _drawingKind == "Goods" ? false : true;

    public bool IsGoodsDrawing => _drawingKind == "Goods";

    public CategoryOption Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string AmountText
    {
        get => _amountText;
        set => SetProperty(ref _amountText, value);
    }

    public string ExpenseNote
    {
        get => _expenseNote;
        set => SetProperty(ref _expenseNote, value);
    }

    public string DrawingKind
    {
        get => _drawingKind;
        set
        {
            if (SetProperty(ref _drawingKind, value))
            {
                OnPropertyChanged(nameof(IsCashDrawing));
                OnPropertyChanged(nameof(IsGoodsDrawing));
            }
        }
    }

    public string DrawingAmountText
    {
        get => _drawingAmountText;
        set => SetProperty(ref _drawingAmountText, value);
    }

    public string DrawingNote
    {
        get => _drawingNote;
        set => SetProperty(ref _drawingNote, value);
    }

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

    /// <summary>What the medicine is worth at purchase cost - the number that will be recorded.</summary>
    public string ValuePreview
    {
        get => _valuePreview;
        private set => SetProperty(ref _valuePreview, value);
    }

    public string StockPreview
    {
        get => _stockPreview;
        private set => SetProperty(ref _stockPreview, value);
    }

    public string TodayText
    {
        get => _todayText;
        private set => SetProperty(ref _todayText, value);
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
        Rows.Clear();

        foreach (var row in _services.CashOut.List())
        {
            Rows.Add(row);
        }

        var today = _services.CashOut.Today();
        TodayText = "Today: " + today.CashOutLabel + " out of the drawer (shop " + today.ExpenseLabel
                    + ", owner cash " + today.DrawingLabel + ") · goods taken at cost " + today.GoodsLabel
                    + " · " + today.Entries + " entr" + (today.Entries == 1 ? "y" : "ies");

        OnPropertyChanged(nameof(CanRecord));
        OnPropertyChanged(nameof(IsNotAllowed));
        OnPropertyChanged(nameof(IsOwner));
    }

    private void SaveExpense()
    {
        if (!Money.TryParse(AmountText, out var rupees) || rupees <= 0)
        {
            SetStatus("Type the amount spent, for example 1500.", true);
            return;
        }

        var input = new ExpenseInput
        {
            Category = Category.Key,
            Title = Title,
            AmountPaisa = Money.ToPaisa(rupees),
            Note = ExpenseNote
        };

        var result = _services.CashOut.AddExpense(_user, input);

        if (!result.Ok || result.Value is null)
        {
            SetStatus(result.Error ?? "The expense could not be saved.", true);
            return;
        }

        var saved = result.Value;

        Title = string.Empty;
        AmountText = string.Empty;
        ExpenseNote = string.Empty;

        Load();

        var message = Category.Label + " " + Money.Format(saved.AmountPaisa) + " recorded.";
        SetStatus(saved.HasWarning ? message + " " + saved.WarningText : message, saved.HasWarning);
    }

    private void SaveDrawing()
    {
        if (IsCashDrawing)
        {
            SaveCashDrawing();
            return;
        }

        SaveGoodsDrawing();
    }

    private void SaveCashDrawing()
    {
        if (!Money.TryParse(DrawingAmountText, out var rupees) || rupees <= 0)
        {
            SetStatus("Type the cash the owner took, for example 2000.", true);
            return;
        }

        var result = _services.CashOut.AddDrawing(_user, new DrawingInput
        {
            Type = "cash",
            AmountPaisa = Money.ToPaisa(rupees),
            Note = DrawingNote
        });

        if (!result.Ok || result.Value is null)
        {
            SetStatus(result.Error ?? "The drawing could not be saved.", true);
            return;
        }

        var saved = result.Value;
        DrawingAmountText = string.Empty;
        DrawingNote = string.Empty;

        Load();

        var message = "Owner took " + Money.Format(saved.AmountPaisa) + " cash.";
        SetStatus(saved.HasWarning ? message + " " + saved.WarningText : message, saved.HasWarning);
    }

    private void SaveGoodsDrawing()
    {
        if (SelectedProduct is null)
        {
            SetStatus("Search the medicine and pick it first.", true);
            return;
        }

        if (!Qty.TryParse(QtyText, out var qty) || qty <= 0)
        {
            SetStatus("The quantity must be more than zero.", true);
            return;
        }

        var name = SelectedProduct.Name;

        var result = _services.CashOut.AddDrawing(_user, new DrawingInput
        {
            Type = "goods",
            ProductId = SelectedProduct.Id,
            BatchId = SelectedBatch?.Id,
            QtyEntered = qty,
            Unit = Unit,
            Note = DrawingNote
        });

        if (!result.Ok || result.Value is null)
        {
            SetStatus(result.Error ?? "The drawing could not be saved.", true);
            return;
        }

        var saved = result.Value;
        QtyText = string.Empty;
        DrawingNote = string.Empty;

        Load();
        LoadBatches();

        var message = "Owner took " + Qty.Format(saved.QtyBase) + " of " + name + ", valued at cost "
                      + Money.Format(saved.AmountPaisa) + ".";

        SetStatus(saved.HasWarning ? message + " " + saved.WarningText : message, saved.HasWarning);
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

        // The nearest expiry first: that is the stock the shop would rather see leave the shelf.
        SelectedBatch = Batches.FirstOrDefault();

        RefreshPreview();
    }

    /// <summary>Shows what will be recorded before the button is pressed. Pure arithmetic.</summary>
    private void RefreshPreview()
    {
        if (SelectedProduct is null)
        {
            ValuePreview = "Pick a medicine first.";
            StockPreview = string.Empty;
            return;
        }

        var perUnit = Packs.PerUnit(SelectedProduct.BoxStrips, SelectedProduct.StripTablets, Unit);

        Qty.TryParse(QtyText, out var qty);
        var qtyBase = Calc.ToBase(qty, perUnit);

        var cost = SelectedBatch is not null && SelectedBatch.CostPaisa > 0
            ? SelectedBatch.CostPaisa
            : SelectedProduct.CostPaisa;

        ValuePreview = Money.Format(Calc.Round(qtyBase * cost)) + " at cost";

        StockPreview = SelectedBatch is null
            ? "No batch on this medicine."
            : "Batch " + SelectedBatch.BatchNo + ": " + Qty.Format(SelectedBatch.QtyBase) + " now -> "
              + Qty.Format(SelectedBatch.QtyBase - qtyBase) + " after";

        var willGoMinus = SelectedBatch is not null && SelectedBatch.QtyBase - qtyBase < -0.0005;

        if (willGoMinus)
        {
            StockPreview += "  (MINUS - allowed with a warning, U-30)";
        }
    }

    private void SetStatus(string message, bool isError)
    {
        StatusText = message;
        IsStatusError = isError;
    }
}

/// <summary>One line of the category drop-down: the key stored in the database and the word on screen.</summary>
public sealed class CategoryOption
{
    public CategoryOption(string key, string label)
    {
        Key = key;
        Label = label;
    }

    public string Key { get; }

    public string Label { get; }

    public override string ToString() => Label;
}
