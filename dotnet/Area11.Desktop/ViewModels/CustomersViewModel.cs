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
public sealed class CustomersViewModel : ObservableObject, IEmptyPage
{
    /// <summary>
    /// Why this page has nothing on it, in the shop's words (v0.33.0, D-89). **Null when it
    /// has rows**, so nothing in this page has to keep a second flag in step with its list.
    /// </summary>
    public EmptyHint? EmptyHint => EmptyPage.When(
        Customers.Count == 0,
        "Customers",
        searched: SearchText.Trim().Length > 0);

    private readonly AppServices _services;
    private readonly User _user;
    private readonly Action? _onChanged;

    private string _searchText = string.Empty;
    private string _amountText = string.Empty;
    private string _method = "cash";
    private string _note = string.Empty;

    // The month box starts at the month we are in: the commonest statement is "this month".
    private string _statementMonth = Clock.MonthKey();
    private int _lastPaymentId;
    private bool _canPrintReceipt;
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

    private bool _isAdding;
    private string _newName = string.Empty;
    private string _newPhone = string.Empty;
    private string _newCategory = "Normal";

    private bool _isEditing;
    private string _editName = string.Empty;
    private string _editPhone = string.Empty;
    private string _editCategory = "Normal";
    private string _editLimitText = string.Empty;

    public CustomersViewModel(AppServices services, User user, Action? onChanged = null)
    {
        _services = services;
        _user = user;
        _onChanged = onChanged;

        RefreshCommand = new RelayCommand(Load);
        PhotosCommand = new RelayCommand(OpenPhotos);

        Editor = new CustomFieldsEditorViewModel(services, user, CustomFieldEntity.Customer);
        CollectCommand = new RelayCommand(Collect);
        StatementCommand = new RelayCommand(PrintStatement);
        MonthStatementCommand = new RelayCommand(PrintMonthStatement);
        ReceiptCommand = new RelayCommand(PrintReceipt);
        EditCommand = new RelayCommand(BeginEdit);
        SaveEditCommand = new RelayCommand(SaveEdit);
        CancelEditCommand = new RelayCommand(CancelEdit);
        AddNewCommand = new RelayCommand(BeginAdd);
        SaveNewCommand = new RelayCommand(SaveNew);
        CancelNewCommand = new RelayCommand(CancelAdd);

        Load();
    }

    public ObservableCollection<CustomerSummary> Customers { get; } = new();

    public ObservableCollection<LedgerLine> Lines { get; } = new();

    public IReadOnlyList<string> Methods { get; } = CustomerLedgerService.CollectionMethods;

    public RelayCommand RefreshCommand { get; }

    /// <summary>The photo gallery of the customer on the page (F-41, D-21).</summary>
    public RelayCommand PhotosCommand { get; }

    /// <summary>The owner's own questions for a customer (F-42) - drawn only if he made any.</summary>
    public CustomFieldsEditorViewModel Editor { get; }

    public RelayCommand CollectCommand { get; }

    public RelayCommand StatementCommand { get; }

    public RelayCommand MonthStatementCommand { get; }

    /// <summary>Prints the slip of the money received most recently on this page (F-49).</summary>
    public RelayCommand ReceiptCommand { get; }

    public RelayCommand EditCommand { get; }

    public RelayCommand SaveEditCommand { get; }

    public RelayCommand CancelEditCommand { get; }

    /// <summary>Opens the "new customer" card - the form the owner asked for (D-50).</summary>
    public RelayCommand AddNewCommand { get; }

    public RelayCommand SaveNewCommand { get; }

    public RelayCommand CancelNewCommand { get; }

    /// <summary>The three words the products table uses for rates - shown as typed, stored in lower case.</summary>
    public IReadOnlyList<string> Categories { get; } = new[] { "Normal", "VIP", "Doctor" };

    /// <summary>The limit is money: only the owner or a manager may change it (F-11).</summary>
    public bool CanEditLimit => _user.CanManage;

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

    /// <summary>The month for the monthly statement, `2026-10`. Empty means every movement.</summary>
    public string StatementMonth
    {
        get => _statementMonth;
        set => SetProperty(ref _statementMonth, value);
    }

    /// <summary>The last collection on this page - what the Receipt button prints.</summary>
    public int LastPaymentId
    {
        get => _lastPaymentId;
        private set => SetProperty(ref _lastPaymentId, value);
    }

    public bool CanPrintReceipt
    {
        get => _canPrintReceipt;
        private set => SetProperty(ref _canPrintReceipt, value);
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
        private set
        {
            if (SetProperty(ref _hasCustomer, value))
            {
                OnPropertyChanged(nameof(IsViewing));
            }
        }
    }

    /// <summary>
    /// True while the "new customer" card is open. Adding and correcting never show together, so a
    /// half-typed correction can never be saved as a new person by mistake.
    /// </summary>
    public bool IsAdding
    {
        get => _isAdding;
        private set
        {
            if (SetProperty(ref _isAdding, value))
            {
                OnPropertyChanged(nameof(IsViewing));
                OnPropertyChanged(nameof(CanOpenAddForm));
            }
        }
    }

    /// <summary>The "+ New" button is dead while a card is already open - one card at a time.</summary>
    public bool CanOpenAddForm => !_isAdding && !_isEditing;

    /// <summary>True while the correction form is open; the khata book steps aside for it.</summary>
    public bool IsEditing
    {
        get => _isEditing;
        private set
        {
            if (SetProperty(ref _isEditing, value))
            {
                OnPropertyChanged(nameof(IsViewing));
                OnPropertyChanged(nameof(CanOpenAddForm));
            }
        }
    }

    public bool IsViewing => _hasCustomer && !_isEditing && !_isAdding;

    /// <summary>No card is open: the page shows the khata book and the history.</summary>
    public bool IsBrowsing => !_isAdding && !_isEditing;

    public string EditName
    {
        get => _editName;
        set => SetProperty(ref _editName, value);
    }

    public string EditPhone
    {
        get => _editPhone;
        set => SetProperty(ref _editPhone, value);
    }

    public string EditCategory
    {
        get => _editCategory;
        set => SetProperty(ref _editCategory, value);
    }

    public string EditLimitText
    {
        get => _editLimitText;
        set => SetProperty(ref _editLimitText, value);
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

    // ----------------------------- the new customer card ------------------------------

    public string NewName
    {
        get => _newName;
        set => SetProperty(ref _newName, value);
    }

    public string NewPhone
    {
        get => _newPhone;
        set => SetProperty(ref _newPhone, value);
    }

    public string NewCategory
    {
        get => _newCategory;
        set => SetProperty(ref _newCategory, value);
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
        IsEditing = false;
        Editor.Load(Selected?.Id ?? 0);

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
        // The stars with the word behind them ("***** VIP"), and the points in money the customer can
        // actually spend - the two things the cashier says out loud (F-40).
        StarsText = Selected.StarsLabel + " " + Selected.TitleLabel;
        PointsText = Selected.PointsLabel;
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

    /// <summary>Opens the photo window for the customer whose khata is on the screen.</summary>
    private void OpenPhotos()
    {
        if (Selected is null)
        {
            SetStatus("Choose a customer first.", true);
            return;
        }

        PhotoWindow.Show(_services, _user, PhotoEntity.Customer, Selected.Id, Selected.Name);
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

        LastPaymentId = collection.PaymentId;
        CanPrintReceipt = true;

        (bool Ok, string Message) printed = _services.Settings.GetBool("printer.autoPrint", true)
            ? PrintPaymentReceipt(collection.PaymentId)
            : (true, "Press Receipt for the paper.");

        SetStatus(name + ": " + Money.Format(collection.AmountPaisa) + " received (" + collection.Code
                  + "). Khata now " + Money.Format(collection.NewBalancePaisa) + ". " + printed.Message, !printed.Ok);
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

    /// <summary>The month's statement (F-49): one month between an opening and a closing balance.</summary>
    private void PrintMonthStatement()
    {
        if (Selected is null)
        {
            SetStatus("Pick a customer first.", true);
            return;
        }

        var statement = _services.Ledger.Statement(Selected.Id, StatementMonth);

        if (!statement.Ok || statement.Value is null)
        {
            SetStatus(statement.Error ?? "The monthly statement could not be prepared.", true);
            return;
        }

        var printed = _services.Printer.Print(statement.Value);

        SetStatus(printed.Ok
            ? "Monthly statement for " + Selected.Name + " (" + StatementMonth.Trim() + ") sent to the printer."
            : "The printer did not answer: " + printed.Error, !printed.Ok);
    }

    /// <summary>Prints the slip of the last collection on this page.</summary>
    private void PrintReceipt()
    {
        if (Selected is null || LastPaymentId <= 0)
        {
            SetStatus("Receive money first - the receipt belongs to a collection.", true);
            return;
        }

        var printed = PrintPaymentReceipt(LastPaymentId);
        SetStatus(printed.Message, !printed.Ok);
    }

    /// <summary>
    /// Builds and prints the PAY slip. A failure is a sentence, never an exception: the money has
    /// already been saved, and the cashier must still see that it was.
    /// </summary>
    private (bool Ok, string Message) PrintPaymentReceipt(int paymentId)
    {
        var receipt = _services.Ledger.PaymentReceipt(_user, paymentId);

        if (!receipt.Ok || receipt.Value is null)
        {
            return (false, "The receipt could not be built: " + (receipt.Error ?? "no reason given"));
        }

        var printed = _services.Printer.Print(receipt.Value);

        return printed.Ok
            ? (true, "Receipt printed.")
            : (false, "The receipt did not print: " + (printed.Error ?? "the printer did not answer"));
    }

    // ---------------------------- adding a customer ----------------------------

    /// <summary>
    /// Opens the blank card. The search box is left alone: the cashier may have typed "Ali" to check
    /// that the person really is new before pressing the button.
    /// </summary>
    private void BeginAdd()
    {
        if (!CanCollect)
        {
            SetStatus("You are not allowed to add customers.", true);
            return;
        }

        NewName = string.Empty;
        NewPhone = string.Empty;
        NewCategory = "Normal";
        IsEditing = false;
        IsAdding = true;

        SetStatus("Type the name (and the phone, if they give it) and press Add customer.", false);
    }

    private void CancelAdd()
    {
        IsAdding = false;
        SetStatus("No customer was added.", false);
    }

    /// <summary>
    /// Creates the customer and puts them on the page with an empty khata. Every rule - the name
    /// length, the category word, the "this phone already belongs to someone" guard - lives in
    /// PartnerService, so the counter page and this page can never refuse differently.
    /// </summary>
    private void SaveNew()
    {
        if (!CanCollect)
        {
            SetStatus("You are not allowed to add customers.", true);
            return;
        }

        var result = _services.Partners.CreateCustomer(_user, NewName, NewPhone, NewCategory);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The customer could not be saved.", true);
            return;
        }

        var newId = result.Value;

        IsAdding = false;

        // Clear the filter, or the new person could be created and then hidden by the search text.
        SearchText = string.Empty;
        OnlyCredit = false;
        Load();

        Selected = Customers.FirstOrDefault(row => row.Id == newId) ?? Selected;
        ShowCustomer();
        _onChanged?.Invoke();

        SetStatus("Added: " + NameText + ". The khata starts at zero.", false);
    }

    // ---------------------------- correcting a customer ----------------------------

    private void BeginEdit()
    {
        if (Selected is null)
        {
            SetStatus("Pick a customer first.", true);
            return;
        }

        IsAdding = false;
        EditName = Selected.Name;
        EditPhone = Selected.Phone ?? string.Empty;
        EditCategory = Selected.CategoryLabel;
        EditLimitText = Money.Edit(Selected.CreditLimitPaisa);
        IsEditing = true;

        SetStatus("Correct the name, phone, category or limit, then press Save.", false);
    }

    private void CancelEdit()
    {
        IsEditing = false;
        SetStatus("Nothing was changed.", false);
    }

    private void SaveEdit()
    {
        if (Selected is null)
        {
            IsEditing = false;
            SetStatus("Pick a customer first.", true);
            return;
        }

        // A cashier's form does not send a limit at all: null means "leave the money alone".
        long? limit = null;

        if (CanEditLimit)
        {
            if (!Money.TryParse(EditLimitText, out var rupees) || rupees < 0)
            {
                SetStatus("The credit limit must be a number, for example 5000. Use 0 for no limit.", true);
                return;
            }

            limit = Money.ToPaisa(rupees);
        }

        var edit = new CustomerEditInput
        {
            Id = Selected.Id,
            Name = EditName,
            Phone = EditPhone,
            Category = EditCategory,
            CreditLimitPaisa = limit
        };

        var result = _services.Partners.UpdateCustomer(_user, edit);

        if (!result.Ok || result.Value is null)
        {
            SetStatus(result.Error ?? "The customer could not be saved.", true);
            return;
        }

        var saved = result.Value;
        IsEditing = false;

        Load();
        ShowCustomer();
        _onChanged?.Invoke();

        var message = "Saved: " + saved.Name
                      + (saved.LimitChanged ? ", limit " + saved.LimitLabel : string.Empty) + ".";

        SetStatus(saved.HasWarning ? message + " " + saved.WarningText : message, false);
    }

    private void SetStatus(string message, bool isError)
    {
        StatusText = message;
        IsStatusError = isError;
    }
}
