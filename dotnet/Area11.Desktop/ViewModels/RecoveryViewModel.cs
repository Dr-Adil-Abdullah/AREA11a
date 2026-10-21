using System.Collections.ObjectModel;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// Udhaar recovery (F-33): who still owes, how old the money is, and the slip for today's payment.
///
/// The page answers one question - **whose money is oldest?** - in the order the owner works:
/// the longest debt at the top. Selecting a customer fills the amount with the whole khata, so the
/// common case (he paid everything) is one click; a part payment is just a smaller number.
///
/// No rule lives here: RecoveryService ages the debt, CustomerLedgerService.Collect refuses an
/// over-payment and writes the permanent `PAY-nnnn` number, and the slip is rebuilt from history so
/// a reprint months later says the same numbers.
/// </summary>
public sealed class RecoveryViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;

    private string _searchText = string.Empty;
    private string _amountText = string.Empty;
    private string _method = "cash";
    private string _note = string.Empty;
    private bool _onlyOverdue;
    private bool _isBusy;

    private string _totalText = "Rs 0";
    private string _overdueText = "Rs 0";
    private string _bucket0Text = "Rs 0";
    private string _bucket1Text = "Rs 0";
    private string _bucket2Text = "Rs 0";
    private string _bucket3Text = "Rs 0";
    private string _countText = "0 customers owe money";
    private bool _booksMatch = true;
    private string _booksLabel = string.Empty;

    private string _customerText = "Pick a customer from the list.";
    private string _phoneText = string.Empty;
    private string _balanceText = "Rs 0";
    private string _ageText = string.Empty;
    private string _autoPrintHint = string.Empty;

    private string _statusText = "Pick the customer who came to pay, type the amount, then Receive.";
    private bool _isStatusError;

    private RecoveryRow? _selected;
    private int _lastPaymentId;

    public RecoveryViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        RefreshCommand = new RelayCommand(Load);
        ReceiveCommand = new RelayCommand(Receive);
        SlipCommand = new RelayCommand(PrintLastSlip);

        Load();
    }

    /// <summary>The screen's list, already filtered by the two small boxes.</summary>
    public ObservableCollection<RecoveryRow> Rows { get; } = new();

    /// <summary>Everything the service returned, before the filter - the totals come from here.</summary>
    private readonly List<RecoveryRow> _all = new();

    public IReadOnlyList<string> Methods { get; } = CustomerLedgerService.CollectionMethods;

    public RelayCommand RefreshCommand { get; }

    public RelayCommand ReceiveCommand { get; }

    public RelayCommand SlipCommand { get; }

    /// <summary>Reading the whole shop's debts is the customers permission (D-43).</summary>
    public bool CanRead => _services.Permissions.Can(_user, Modules.Customers);

    public bool IsNotAllowed => !CanRead;

    public bool HasLastSlip => _lastPaymentId > 0;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public bool OnlyOverdue
    {
        get => _onlyOverdue;
        set
        {
            if (SetProperty(ref _onlyOverdue, value))
            {
                ApplyFilter();
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

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public RecoveryRow? Selected
    {
        get => _selected;
        set
        {
            if (!SetProperty(ref _selected, value) || value is null)
            {
                return;
            }

            // The common case is a customer who came to clear his account: pre-fill the whole khata.
            CustomerText = value.Name;
            PhoneText = value.PhoneLabel;
            BalanceText = value.BalanceText;
            AgeText = "Oldest money: " + value.OldestLabel + " · over two months: " + value.OverdueText;
            AmountText = Money.Edit(value.BalancePaisa);
        }
    }

    public string TotalText
    {
        get => _totalText;
        private set => SetProperty(ref _totalText, value);
    }

    public string OverdueText
    {
        get => _overdueText;
        private set => SetProperty(ref _overdueText, value);
    }

    public string Bucket0Text
    {
        get => _bucket0Text;
        private set => SetProperty(ref _bucket0Text, value);
    }

    public string Bucket1Text
    {
        get => _bucket1Text;
        private set => SetProperty(ref _bucket1Text, value);
    }

    public string Bucket2Text
    {
        get => _bucket2Text;
        private set => SetProperty(ref _bucket2Text, value);
    }

    public string Bucket3Text
    {
        get => _bucket3Text;
        private set => SetProperty(ref _bucket3Text, value);
    }

    public string CountText
    {
        get => _countText;
        private set => SetProperty(ref _countText, value);
    }

    public bool BooksMatch
    {
        get => _booksMatch;
        private set => SetProperty(ref _booksMatch, value);
    }

    public string BooksLabel
    {
        get => _booksLabel;
        private set => SetProperty(ref _booksLabel, value);
    }

    public string CustomerText
    {
        get => _customerText;
        private set => SetProperty(ref _customerText, value);
    }

    public string PhoneText
    {
        get => _phoneText;
        private set => SetProperty(ref _phoneText, value);
    }

    public string BalanceText
    {
        get => _balanceText;
        private set => SetProperty(ref _balanceText, value);
    }

    public string AgeText
    {
        get => _ageText;
        private set => SetProperty(ref _ageText, value);
    }

    public string AutoPrintHint
    {
        get => _autoPrintHint;
        private set => SetProperty(ref _autoPrintHint, value);
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
        _all.Clear();
        Rows.Clear();

        AutoPrintHint = _services.Settings.GetBool("printer.autoPrint", true)
            ? "The slip prints by itself after every payment."
            : "Printing is manual (Settings): press 'Print slip' after a payment.";

        var report = _services.Recovery.Report(_user);

        if (!report.Ok || report.Value is null)
        {
            SetStatus(report.Error ?? "The udhaar list could not be built.", true);
            OnPropertyChanged(nameof(CanRead));
            OnPropertyChanged(nameof(IsNotAllowed));
            return;
        }

        var data = report.Value;

        _all.AddRange(data.Rows);
        ApplyFilter();

        TotalText = data.TotalText;
        OverdueText = data.OverdueText;
        Bucket0Text = data.Bucket0Text;
        Bucket1Text = data.Bucket1Text;
        Bucket2Text = data.Bucket2Text;
        Bucket3Text = data.Bucket3Text;
        CountText = data.CountText;
        BooksMatch = data.BooksMatch;
        BooksLabel = data.BooksMatch
            ? "Books match: every rupee of udhaar is in one of the four age columns."
            : "CHECK THE BOOKS: " + data.MismatchCount
              + " customer(s) owe a balance that their own history cannot explain.";

        OnPropertyChanged(nameof(CanRead));
        OnPropertyChanged(nameof(IsNotAllowed));
    }

    private void ApplyFilter()
    {
        var term = (SearchText ?? string.Empty).Trim();

        Rows.Clear();

        foreach (var row in _all)
        {
            if (OnlyOverdue && row.OverduePaisa <= 0)
            {
                continue;
            }

            if (term.Length > 0
                && !row.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                && !(row.Phone ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Rows.Add(row);
        }
    }

    private void Receive()
    {
        if (Selected is null)
        {
            SetStatus("Pick the customer who is paying.", true);
            return;
        }

        if (!Money.TryParse(AmountText, out var rupees) || rupees <= 0)
        {
            SetStatus("Type the amount received, for example 1500.", true);
            return;
        }

        IsBusy = true;

        try
        {
            var name = Selected.Name;
            var customerId = Selected.CustomerId;

            var collected = _services.Ledger.Collect(
                _user, customerId, Money.ToPaisa(rupees), Method, string.IsNullOrWhiteSpace(Note) ? null : Note);

            if (!collected.Ok || collected.Value is null)
            {
                SetStatus(collected.Error ?? "The money could not be recorded.", true);
                return;
            }

            var saved = collected.Value;

            _lastPaymentId = saved.PaymentId;
            OnPropertyChanged(nameof(HasLastSlip));

            Note = string.Empty;
            Load();

            // Load() rebuilt the list, so the row object the customer was selected with is gone;
            // find him again by id and re-fill the amount box with what is still owed.
            Selected = Rows.FirstOrDefault(row => row.CustomerId == customerId);

            if (Selected is null)
            {
                // He paid the last rupee: the customer is no longer on a list of people who owe.
                CustomerText = name + " - khata clear";
                BalanceText = "Rs 0";
                AgeText = string.Empty;
                AmountText = string.Empty;
            }

            var message = name + ": " + Money.Format(saved.AmountPaisa) + " received (" + saved.Code
                          + "). Khata now " + Money.Format(saved.NewBalancePaisa) + ".";

            if (_services.Settings.GetBool("printer.autoPrint", true))
            {
                var printed = _services.Printer.Print(SlipDocument(saved.PaymentId));

                message += printed.Ok
                    ? " Slip sent to the printer."
                    : " The printer did not answer: " + printed.Error + " - press 'Print slip' to try again.";
            }

            SetStatus(message, false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void PrintLastSlip()
    {
        if (_lastPaymentId <= 0)
        {
            SetStatus("Receive a payment first - the slip carries its PAY number.", true);
            return;
        }

        var printed = _services.Printer.Print(SlipDocument(_lastPaymentId));

        SetStatus(printed.Ok
            ? "Recovery slip sent to the printer."
            : "The printer did not answer: " + printed.Error, !printed.Ok);
    }

    private ReceiptDocument SlipDocument(int paymentId)
    {
        var document = _services.Recovery.SlipDocument(paymentId, _user);

        if (document.Ok && document.Value is not null)
        {
            return document.Value;
        }

        // A failed slip must never stop the money that is already saved: hand the printer an empty
        // page and let PrintDocument's own check report the problem in the status line.
        SetStatus(document.Error ?? "The slip could not be built.", true);

        return new ReceiptDocument { Title = "Recovery slip", Width = 32 };
    }

    private void SetStatus(string message, bool isError)
    {
        StatusText = message;
        IsStatusError = isError;
    }
}
