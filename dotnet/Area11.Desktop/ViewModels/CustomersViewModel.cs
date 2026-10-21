using System.Collections.ObjectModel;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// Customers (F-27, Spec 17.3, 13.5): the khata book.
///
/// Left: every customer with their udhaar, searchable by name or phone. Right: one customer's page -
/// the balance, the limit, the stars, every bill, collection and return, and the box where money is
/// received.
///
/// The line at the bottom of the page is the one this screen exists for: **"Books match"** when the
/// stored khata equals its own history. When it does not, the page says so in red and prints the
/// difference - a balance the app cannot explain is worse than no balance at all.
///
/// No rule lives here. CustomerLedgerService refuses an over-payment, writes the collection into
/// `payments`, and rebuilds the running balance with the same clamps the rest of the app uses.
/// </summary>
public sealed class CustomersViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;
    private readonly Action? _onChanged;

    private string _searchText = string.Empty;
    private string _amountText = string.Empty;
    private string _method = "cash";
    private string _note = string.Empty;
    private string _statusText = "Search a customer, or tick 'Only with udhaar' to see who owes money.";
    private bool _isStatusError;
    private bool _onlyCredit;
    private bool _hasCustomer;
    private bool _booksMatch = true;
    private string _booksLabel = "Pick a customer to see the khata.";
    private string _nameText = string.Empty;
    private string _phoneText = string.Empty;
    private string _categoryText = string.Empty;
    private string _balanceText = "Rs 0";
    private string _limitText = "-";
    private string _starsText = "-";
    private string _pointsText = "-";
    private string _billsText = "-";
    private string _lifetimeText = "Rs 0";
    private string _profitText = "Rs 0";
    private string _returnsText = "Rs 0";
    private string _lastBillText = "-";
    private CustomerSummary? _selected;

    public CustomersViewModel(AppServices services, User user, Action? onChanged = null)
    {
        _services = services;
        _user = user;
        _onChanged = onChanged;

        RefreshCommand = new RelayCommand(Load);
        CollectCommand = new RelayCommand(Collect);
        StatementCommand = new RelayCommand(PrintStatement);

        Load();
    }

    public ObservableCollection<CustomerSummary> Customers { get; } = new();

    public ObservableCollection<LedgerLine> Lines { get; } = new();

    public IReadOnlyList<string> Methods { get; } = CustomerLedgerService.CollectionMethods;

    public RelayCommand RefreshCommand { get; }

    public RelayCommand CollectCommand { get; }

    public RelayCommand StatementCommand { get; }

    public bool CanCollect => _services.Permissions.Can(_user, Modules.Customers);

    public bool IsNotAllowed => !CanCollect;

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public bool OnlyCredit
    {
        get => _onlyCredit;
        set
        {
            if (SetProperty(ref _onlyCredit, value))
            {
                Load();
            }
        }
    }

    public string AmountText
    {
        get => _amountText;
        set => SetProperty(ref _amountText, value);
    }

    public string Method
    {
        get => _method;
        set => SetProperty(ref _method, value);
    }

    public string Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    public CustomerSummary? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                ShowCustomer();
            }
        }
    }

    public bool HasCustomer
    {
        get => _hasCustomer;
        private set => SetProperty(ref _hasCustomer, value);
    }

    public bool BooksMatch
    {
        get => _booksMatch;
        private set
        {
            if (SetProperty(ref _booksMatch, value))
            {
                OnPropertyChanged(nameof(BooksMismatch));
            }
        }
    }

    public bool BooksMismatch => !_booksMatch;

    public string BooksLabel
    {
        get => _booksLabel;
        private set => SetProperty(ref _booksLabel, value);
    }

    public string NameText
    {
        get => _nameText;
        private set => SetProperty(ref _nameText, value);
    }

    public string PhoneText
    {
        get => _phoneText;
        private set => SetProperty(ref _phoneText, value);
    }

    public string CategoryText
    {
        get => _categoryText;
        private set => SetProperty(ref _categoryText, value);
    }

    public string BalanceText
    {
        get => _balanceText;
        private set => SetProperty(ref _balanceText, value);
    }

    public string LimitText
    {
        get => _limitText;
        private set => SetProperty(ref _limitText, value);
    }

    public string StarsText
    {
        get => _starsText;
        private set => SetProperty(ref _starsText, value);
    }

    public string PointsText
    {
        get => _pointsText;
        private set => SetProperty(ref _pointsText, value);
    }

    public string BillsText
    {
        get => _billsText;
        private set => SetProperty(ref _billsText, value);
    }

    public string LifetimeText
    {
        get => _lifetimeText;
        private set => SetProperty(ref _lifetimeText, value);
    }

    public string ProfitText
    {
        get => _profitText;
        private set => SetProperty(ref _profitText, value);
    }

    public string ReturnsText
    {
        get => _returnsText;
        private set => SetProperty(ref _returnsText, value);
    }

    public string LastBillText
    {
        get => _lastBillText;
        private set => SetProperty(ref _lastBillText, value);
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
        var keep = Selected?.Id;

        var result = _services.Ledger.List(SearchText, OnlyCredit);

        Customers.Clear();

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The customers could not be loaded.", true);
            return;
        }

        foreach (var row in result.Value!)
        {
            Customers.Add(row);
        }

        Selected = Customers.FirstOrDefault(row => row.Id == keep) ?? Customers.FirstOrDefault();

        if (Customers.Count == 0)
        {
            SetStatus(OnlyCredit
                ? "Nobody owes money right now."
                : "No customer matches that search.", false);
        }
    }

    /// <summary>Fills the page from the service - the numbers and the whole history.</summary>
    private void ShowCustomer()
    {
        Lines.Clear();

        if (Selected is null)
        {
            HasCustomer = false;
            BooksLabel = "Pick a customer to see the khata.";
            BooksMatch = true;
            return;
        }

        HasCustomer = true;
        NameText = Selected.Name;
        PhoneText = Selected.PhoneLabel;
        CategoryText = Selected.CategoryLabel;
        LimitText = Selected.LimitText;
        StarsText = Selected.StarsLabel;
        PointsText = Selected.LoyaltyPoints.ToString();
        BillsText = Selected.BillsCount.ToString();
        LifetimeText = Selected.LifetimeText;
        ProfitText = Selected.ProfitText;
        ReturnsText = Selected.ReturnsText;
        LastBillText = Selected.LastBillLabel;
        BalanceText = Selected.BalanceText;

        var page = _services.Ledger.Page(Selected.Id);

        if (!page.Ok || page.Value is null)
        {
            BooksMatch = false;
            BooksLabel = page.Error ?? "The account could not be loaded.";
            return;
        }

        var data = page.Value;

        foreach (var line in data.Lines)
        {
            Lines.Add(line);
        }

        BalanceText = Money.Format(data.StoredPaisa);
        BooksMatch = data.BooksMatch;
        BooksLabel = data.BooksLabel;
    }

    private void Collect()
    {
        if (Selected is null)
        {
            SetStatus("Pick a customer first.", true);
            return;
        }

        if (!Money.TryParse(AmountText, out var rupees) || rupees <= 0)
        {
            SetStatus("Type the money received, for example 500.", true);
            return;
        }

        var customerId = Selected.Id;
        var name = Selected.Name;

        var result = _services.Ledger.Collect(_user, customerId, Money.ToPaisa(rupees), Method, Note);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The money could not be recorded.", true);
            return;
        }

        var collection = result.Value!;
        AmountText = string.Empty;
        Note = string.Empty;

        Load();

        // Load() re-selects the same customer; make sure the page shows the new balance.
        ShowCustomer();
        _onChanged?.Invoke();

        SetStatus(name + ": " + Money.Format(collection.AmountPaisa) + " received (" + collection.Code
                  + "). Khata now " + Money.Format(collection.NewBalancePaisa) + ".", false);
    }

    private void PrintStatement()
    {
        if (Selected is null)
        {
            SetStatus("Pick a customer first.", true);
            return;
        }

        var statement = _services.Ledger.Statement(Selected.Id);

        if (!statement.Ok || statement.Value is null)
        {
            SetStatus(statement.Error ?? "The statement could not be prepared.", true);
            return;
        }

        var document = statement.Value;
        var printed = _services.Printer.Print(document);

        SetStatus(printed.Ok
            ? "Statement for " + Selected.Name + " sent to the printer."
            : "The printer did not answer: " + printed.Error, !printed.Ok);
    }

    private void SetStatus(string message, bool isError)
    {
        StatusText = message;
        IsStatusError = isError;
    }
}
