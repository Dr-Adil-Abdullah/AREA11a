using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// The counter. The total on the screen is always produced by SaleService.Quote — the same
/// calculation that saves the bill — so what the cashier reads is what is stored.
/// </summary>
public sealed class PosViewModel : ObservableObject, IEmptyPage
{
    /// <summary>
    /// Why this page has nothing on it, in the shop's words (v0.33.0, D-89). **Null when it
    /// has rows**, so nothing in this page has to keep a second flag in step with its list.
    /// </summary>
    public EmptyHint? EmptyHint => EmptyPage.When(
        Products.Count == 0,
        "Counter (POS)",
        searched: SearchText.Trim().Length > 0);

    private readonly AppServices _services;
    private readonly User _user;
    private readonly List<Product> _catalog = new();

    // Every live barcode of every medicine, grouped by product, so one keystroke filters without a
    // database call (the scanner types a whole code in a few milliseconds).
    private readonly Dictionary<int, List<string>> _codesByProduct = new();

    private string _searchText = string.Empty;
    private string _searchHint = string.Empty;
    private bool _isSearchGuessing;
    private Product? _selectedProduct;
    private string _unit = "strip";
    private string _quantity = "1";
    private Customer? _selectedCustomer;
    private Customer? _selectedDoctor;
    private string _redeemPointsText = string.Empty;
    private string _pointsUsedText = string.Empty;
    private bool _hasPointsUsed;
    private string _discountPercent = "0";
    private string _paymentMethod = "cash";
    private string _tenderedText = string.Empty;
    private string _secondMethod = "online";
    private string _secondAmountText = string.Empty;
    private bool _isAddingCustomer;
    private string _newCustomerName = string.Empty;
    private string _newCustomerPhone = string.Empty;

    private string _subtotalText = "Rs 0";
    private string _discountText = "Rs 0";
    private string _totalText = "Rs 0";
    private string _changeText = "Rs 0";
    private string _cashPartText = "Rs 0";
    private string _creditPartText = "Rs 0";
    private string _statusMessage = "Ready.";
    private bool _isStatusError;
    private string _lastBill = string.Empty;
    private int _lastSaleId;
    private bool _autoPrint;
    private bool _showSaleSuccess;
    private string _successChangeText = string.Empty;
    private bool _hasSuccessChange;
    private bool _shiftClosed;
    private string _handoverNote = string.Empty;
    private string _blockedText = string.Empty;

    public PosViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        AddCommand = new RelayCommand(AddToCart);
        ScanCommand = new RelayCommand(ScanBarcode);
        RemoveCommand = new RelayCommand(RemoveLast);
        ClearCommand = new RelayCommand(ClearCart);
        CompleteCommand = new RelayCommand(CompleteSale);
        DoneCommand = new RelayCommand(() => ShowSaleSuccess = false);
        PrintLastCommand = new RelayCommand(PrintLast, () => _lastSaleId > 0);
        RefreshCommand = new RelayCommand(LoadLookups);
        OpenProductPhotoCommand = new RelayCommand<int>(OpenProductPhoto);
        HoldCommand = new RelayCommand(HoldCart);
        RefreshHeldCommand = new RelayCommand(LoadHeldCarts);
        BeginAddCustomerCommand = new RelayCommand(BeginAddCustomer);
        CancelAddCustomerCommand = new RelayCommand(CancelAddCustomer);
        SaveCustomerCommand = new RelayCommand(SaveNewCustomer);
        OpenShiftCommand = new RelayCommand(() => OpenShiftRequested?.Invoke());

        _autoPrint = _services.Settings.GetBool("printer.autoPrint", true);

        LoadLookups();
        LoadHeldCarts();
    }

    public ObservableCollection<Product> Products { get; } = new();

    public ObservableCollection<PosCartLine> Cart { get; } = new();

    public ObservableCollection<Customer> Customers { get; } = new();

    /// <summary>The doctors a bill may name (F-44); the first row is "(no prescription)".</summary>
    public ObservableCollection<Customer> Doctors { get; } = new();

    public ObservableCollection<string> Units { get; } = new() { "base", "strip", "box" };

    public ObservableCollection<string> PaymentMethods { get; } = new() { "cash", "credit", "split" };

    /// <summary>The second half of a split payment. Cash is the rest of the bill, so it is not here.</summary>
    public ObservableCollection<string> SplitMethods { get; } = new() { "online", "card", "credit" };

    public ObservableCollection<HeldCartRow> HeldCarts { get; } = new();

    public RelayCommand AddCommand { get; }

    /// <summary>A scan (or Enter after typing a code): find the pack, add one of it (F-43).</summary>
    public RelayCommand ScanCommand { get; }

    public RelayCommand RemoveCommand { get; }

    public RelayCommand ClearCommand { get; }

    public RelayCommand CompleteCommand { get; }

    /// <summary>Dismisses only the confirmation card; it never changes the committed bill or new cart.</summary>
    public RelayCommand DoneCommand { get; }

    public RelayCommand PrintLastCommand { get; }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand<int> OpenProductPhotoCommand { get; }

    public RelayCommand HoldCommand { get; }

    public RelayCommand RefreshHeldCommand { get; }

    /// <summary>Takes the operator to the Shift (galla) page when the drawer is closed (D-92).</summary>
    public RelayCommand OpenShiftCommand { get; }

    /// <summary>The frame listens, so this page does not need to know how the menu works.</summary>
    public event Action? OpenShiftRequested;

    /// <summary>
    /// True while no shift is open (v0.35.0, D-92). The owner's rule: **no work happens before the
    /// drawer is counted** - a bill made outside a drawer is money nobody counted at closing time,
    /// and the difference looks like theft. The page says so and offers one button, instead of
    /// letting the cashier fill a cart and be refused at the end.
    /// </summary>
    public bool ShiftClosed
    {
        get => _shiftClosed;
        private set => SetProperty(ref _shiftClosed, value);
    }

    /// <summary>The sentence on the card. Built in one place, so the screen cannot drift.</summary>
    public string BlockedText
    {
        get => _blockedText;
        private set => SetProperty(ref _blockedText, value);
    }

    /// <summary>
    /// True while a bill is impossible: no drawer open, or a drawer that a forgotten session left
    /// uncounted (v0.35.0, D-92). One flag for the card, so the page has one thing to bind to.
    /// </summary>
    public bool IsBlocked => ShiftClosed || _handoverNote.Length > 0;

    /// <summary>Open the small "new customer" card under the customer box (F-11).</summary>
    public RelayCommand BeginAddCustomerCommand { get; }

    public RelayCommand CancelAddCustomerCommand { get; }

    public RelayCommand SaveCustomerCommand { get; }

    public string CashierName => _user.Name;

    public int MaxDiscountPercent => _user.Kind switch
    {
        UserRole.Owner => 100,
        UserRole.Manager => _services.Settings.GetInt("discount.maxPercentManager", 20),
        _ => _services.Settings.GetInt("discount.maxPercentCashier", 5)
    };

    /// <summary>Arbitrary rates are owner/manager only; a cashier may still accept a configured special rate.</summary>
    public bool CanEditRates => _user.CanManage;

    /// <summary>
    /// The sentence under the search box (F-50). Empty for an exact search; a "did you mean" line
    /// when the list below is a guess, so no cashier ever reads a suggestion as an answer.
    /// </summary>
    public string SearchHint
    {
        get => _searchHint;
        private set => SetProperty(ref _searchHint, value);
    }

    /// <summary>True while the list below is a typo suggestion - the hint turns amber to say so.</summary>
    public bool IsSearchGuessing
    {
        get => _isSearchGuessing;
        private set => SetProperty(ref _isSearchGuessing, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                FilterProducts();
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
                OnPropertyChanged(nameof(PriceHint));
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
                OnPropertyChanged(nameof(PriceHint));
            }
        }
    }

    public string Quantity
    {
        get => _quantity;
        set => SetProperty(ref _quantity, value);
    }

    public Customer? SelectedCustomer
    {
        get => _selectedCustomer;
        set
        {
            if (SetProperty(ref _selectedCustomer, value))
            {
                // Points belong to the person, so a new customer starts with an empty box - otherwise
                // the second customer in the queue would pay with the first one's loyalty.
                RedeemPointsText = string.Empty;
                OnPropertyChanged(nameof(CustomerHint));
                OnPropertyChanged(nameof(CanRedeem));
                OnPropertyChanged(nameof(RedeemHint));
                Recalculate();
            }
        }
    }

    /// <summary>
    /// On whose prescription this bill is being sold (F-44). Optional and reset with each bill, so a
    /// doctor never follows the next customer by accident.
    /// </summary>
    public Customer? SelectedDoctor
    {
        get => _selectedDoctor;
        set
        {
            if (SetProperty(ref _selectedDoctor, value))
            {
                OnPropertyChanged(nameof(DoctorHint));
            }
        }
    }

    public string DoctorHint => SelectedDoctor is { Id: > 0 }
        ? "Saved on this bill: " + SelectedDoctor.Name
        : "No prescription - most bills.";

    public string DiscountPercent
    {
        get => _discountPercent;
        set
        {
            if (SetProperty(ref _discountPercent, value))
            {
                Recalculate();
            }
        }
    }

    public string PaymentMethod
    {
        get => _paymentMethod;
        set
        {
            if (!SetProperty(ref _paymentMethod, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsCash));
            OnPropertyChanged(nameof(IsSplit));
            OnPropertyChanged(nameof(IsCashOrSplit));
            Recalculate();
        }
    }

    public bool IsCash => string.Equals(PaymentMethod, "cash", StringComparison.OrdinalIgnoreCase);

    /// <summary>Part cash, part online / card / khata (Spec 13.3).</summary>
    public bool IsSplit => string.Equals(PaymentMethod, "split", StringComparison.OrdinalIgnoreCase);

    public bool IsCashOrSplit => IsCash || IsSplit;

    /// <summary>The second half of a split bill: what it is paid by, and how much of the bill it covers.</summary>
    public string SecondMethod
    {
        get => _secondMethod;
        set
        {
            if (SetProperty(ref _secondMethod, value))
            {
                Recalculate();
            }
        }
    }

    public string SecondAmountText
    {
        get => _secondAmountText;
        set
        {
            if (SetProperty(ref _secondAmountText, value))
            {
                Recalculate();
            }
        }
    }

    /// <summary>
    /// Auto or manual printing - the owner asked for this to be a setting, not a hard-coded
    /// behaviour (D-26). It is stored in `printer.autoPrint`, so the choice survives a restart and
    /// will appear on the Settings screen (F-17) without any further work.
    /// </summary>
    public bool AutoPrint
    {
        get => _autoPrint;
        set
        {
            if (!SetProperty(ref _autoPrint, value))
            {
                return;
            }

            var saved = _services.Settings.Set("printer.autoPrint", value, _user);
            SetStatus(saved.Ok
                ? (value ? "The receipt will print by itself after each sale." : "Printing is manual now.")
                : (saved.Error ?? "The setting could not be saved."), !saved.Ok);
        }
    }

    public string TenderedText
    {
        get => _tenderedText;
        set
        {
            if (SetProperty(ref _tenderedText, value))
            {
                Recalculate();
            }
        }
    }

    public string SubtotalText
    {
        get => _subtotalText;
        private set => SetProperty(ref _subtotalText, value);
    }

    public string DiscountText
    {
        get => _discountText;
        private set => SetProperty(ref _discountText, value);
    }

    public string TotalText
    {
        get => _totalText;
        private set => SetProperty(ref _totalText, value);
    }

    public string ChangeText
    {
        get => _changeText;
        private set => SetProperty(ref _changeText, value);
    }

    /// <summary>How much of a split bill is cash (the rest is the second method).</summary>
    public string CashPartText
    {
        get => _cashPartText;
        private set => SetProperty(ref _cashPartText, value);
    }

    /// <summary>How much of a split bill goes on the customer's khata.</summary>
    public string CreditPartText
    {
        get => _creditPartText;
        private set => SetProperty(ref _creditPartText, value);
    }

    public string HeldCartsCaption => HeldCarts.Count == 0
        ? "No parked carts. Parked carts survive a restart."
        : $"{HeldCarts.Count} parked cart(s) - press 'Bring back' to serve that customer again.";

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsStatusError
    {
        get => _isStatusError;
        private set => SetProperty(ref _isStatusError, value);
    }

    public string LastBill
    {
        get => _lastBill;
        private set => SetProperty(ref _lastBill, value);
    }

    public bool ShowSaleSuccess
    {
        get => _showSaleSuccess;
        private set => SetProperty(ref _showSaleSuccess, value);
    }

    public string SuccessChangeText
    {
        get => _successChangeText;
        private set => SetProperty(ref _successChangeText, value);
    }

    public bool HasSuccessChange
    {
        get => _hasSuccessChange;
        private set => SetProperty(ref _hasSuccessChange, value);
    }

    /// <summary>
    /// Quick creation at the counter (F-11, D-50). A customer walks in with no khata in the shop and
    /// still wants the bill on credit: without this card the cashier has to abandon the cart, open
    /// Customers, add the person and start again - and on a busy evening the bill goes through as
    /// walk-in cash instead. The rules are PartnerService's, the same ones the Customers page uses.
    /// </summary>
    public bool IsAddingCustomer
    {
        get => _isAddingCustomer;
        private set => SetProperty(ref _isAddingCustomer, value);
    }

    public string NewCustomerName
    {
        get => _newCustomerName;
        set => SetProperty(ref _newCustomerName, value);
    }

    public string NewCustomerPhone
    {
        get => _newCustomerPhone;
        set => SetProperty(ref _newCustomerPhone, value);
    }

    /// <summary>Only a person who may see the khata book may add a name to it.</summary>
    public bool CanAddCustomer => _services.Permissions.Can(_user, Modules.Customers);

    public string CustomerHint => SelectedCustomer is null || SelectedCustomer.Id == 0
        ? "Cash customer"
        : $"{SelectedCustomer.Category} · balance {Money.Format(SelectedCustomer.BalancePaisa)}";

    /// <summary>
    /// True when the points box is worth showing: loyalty is switched on in Settings and **this**
    /// customer has points. A walk-in customer has no khata and therefore no points.
    /// </summary>
    public bool CanRedeem => _services.Settings.GetBool("loyalty.enabled", false)
        && SelectedCustomer is { Id: > 0, LoyaltyPoints: > 0 };

    public string RedeemHint => CanRedeem && SelectedCustomer is not null
        ? SelectedCustomer.LoyaltyPoints + " point(s) available = "
          + Loyalty.ValueText(SelectedCustomer.LoyaltyPoints) + " off this bill"
        : string.Empty;

    /// <summary>How many points the cashier wants to spend; the Core caps it (never refuses).</summary>
    public string RedeemPointsText
    {
        get => _redeemPointsText;
        set
        {
            if (SetProperty(ref _redeemPointsText, value))
            {
                Recalculate();
            }
        }
    }

    /// <summary>"Points used -Rs 5" once the bill knows how many were really accepted.</summary>
    public string PointsUsedText
    {
        get => _pointsUsedText;
        private set => SetProperty(ref _pointsUsedText, value);
    }

    public bool HasPointsUsed
    {
        get => _hasPointsUsed;
        private set => SetProperty(ref _hasPointsUsed, value);
    }

    public string PriceHint
    {
        get
        {
            if (SelectedProduct is null)
            {
                return "Pick a medicine";
            }

            var units = SelectedProduct.QuantityPerSelectedUnit(Unit);
            var price = SelectedProduct.RetailPaisa * units;
            return $"{Unit}: {Money.Format(Calc.Round(price))}";
        }
    }

    private void LoadLookups()
    {
        var products = _services.Products.Search();
        _catalog.Clear();
        if (products.Ok && products.Value is not null)
        {
            foreach (var product in products.Value)
            {
                product.PhotoPath = ResolvePhotoPath(product);
            }
            _catalog.AddRange(products.Value);
        }

        LoadBarcodeMap();

        var customers = _services.Partners.GetCustomers();
        Customers.Clear();
        Customers.Add(new Customer { Id = 0, Name = "Walk-in customer", Category = "normal" });
        if (customers.Ok && customers.Value is not null)
        {
            foreach (var customer in customers.Value)
            {
                Customers.Add(customer);
            }
        }

        SelectedCustomer = Customers[0];

        // The doctors come from the same customer list - one phone book, one rate tier (Spec 17.1).
        Doctors.Clear();
        Doctors.Add(new Customer { Id = 0, Name = "(no prescription)", Category = "normal" });

        foreach (var customer in Customers)
        {
            if (string.Equals(customer.Category, "doctor", StringComparison.OrdinalIgnoreCase))
            {
                Doctors.Add(customer);
            }
        }

        SelectedDoctor = Doctors[0];

        FilterProducts();
        CheckDrawer();
        Recalculate();
    }

    /// <summary>
    /// Reads whether a bill is possible. Called on every refresh, at the start of every bill and
    /// after every sign-out, so the card comes and goes with the real state rather than with a flag
    /// somebody has to remember (v0.35.0, D-92).
    /// </summary>
    private void CheckDrawer()
    {
        var shift = _services.Shifts.Current();
        ShiftClosed = shift is null;
        _handoverNote = SessionHandover.Read(_services.Settings);

        BlockedText = _handoverNote.Length > 0
            ? _handoverNote
            : "The drawer (galla) is closed, so no bill can be made. Count the opening cash on the Shift " +
              "(galla) page and open the drawer - the counter works again the moment it is open.";

        OnPropertyChanged(nameof(IsBlocked));
    }

    private void FilterProducts()
    {
        var term = SearchText.Trim();
        Products.Clear();

        var shown = new List<Product>();

        foreach (var product in _catalog)
        {
            var codeMatches = _codesByProduct.TryGetValue(product.Id, out var codes)
                              && codes.Any(code => code.Contains(term, StringComparison.OrdinalIgnoreCase));

            if (term.Length == 0
                || product.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (product.Generic ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase)
                || (product.Barcode ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase)
                || product.StrengthLabel.Contains(term, StringComparison.OrdinalIgnoreCase)
                || codeMatches)
            {
                shown.Add(product);
            }
        }

        // F-50: nothing spells like that, so show the neighbourhood - and say so. A suggestion only
        // reaches the cart by a second, human press of "Add to cart"; Enter stays a barcode, never a guess.
        if (shown.Count == 0 && Fuzzy.Words(term).Length > 0)
        {
            var close = Fuzzy.Rank(term, _catalog, product => product.Name, product => product.Generic,
                limit: 12);

            foreach (var hit in close)
            {
                shown.Add(hit.Item);
            }

            SearchHint = close.Count == 0
                ? "No medicine is spelt like \"" + term + "\", and nothing close is on the shelf."
                : Fuzzy.SuggestionLabel(term, close[0].Item.Name);

            IsSearchGuessing = close.Count > 0;
        }
        else
        {
            SearchHint = string.Empty;
            IsSearchGuessing = false;
        }

        foreach (var product in shown)
        {
            Products.Add(product);
        }

        if (SelectedProduct is null || !Products.Contains(SelectedProduct))
        {
            SelectedProduct = Products.FirstOrDefault();
        }
    }

    /// <summary>
    /// A USB scanner types the code and presses Enter (R-09) - this is the Enter. The barcode says
    /// which pack was scanned, so a box code adds a box and a strip code adds a strip; the cashier
    /// does not have to remember which button to press after the beep.
    /// </summary>
    private void ScanBarcode()
    {
        var code = SearchText.Trim();

        if (code.Length == 0)
        {
            return;
        }

        var found = _services.Barcodes.Find(_user, code);

        if (!found.Ok || found.Value is null)
        {
            SetStatus(found.Error ?? "That barcode is not in the shop.", true);
            return;
        }

        var match = found.Value;
        var product = _catalog.FirstOrDefault(item => item.Id == match.ProductId);

        if (product is null)
        {
            SetStatus("The medicine of this barcode is not in the active list any more.", true);
            return;
        }

        // Clearing the search first re-filters the list; setting the product after it stops the
        // filter from selecting somebody else at the last moment.
        SearchText = string.Empty;
        SelectedProduct = product;
        Unit = match.Pack is BarcodePack.Box or BarcodePack.Strip ? match.Pack : "base";
        Quantity = "1";

        AddToCart();
        SetStatus(match.Caption + " added.", false);
    }

    /// <summary>All live codes once, so the filter and the scan never disagree.</summary>
    private void LoadBarcodeMap()
    {
        _codesByProduct.Clear();

        var all = _services.Barcodes.All(_user);

        if (!all.Ok || all.Value is null)
        {
            return;
        }

        foreach (var match in all.Value)
        {
            if (!_codesByProduct.TryGetValue(match.ProductId, out var codes))
            {
                codes = new List<string>();
                _codesByProduct[match.ProductId] = codes;
            }

            codes.Add(match.Barcode);
        }
    }

    private static string? ResolvePhotoPath(Product product)
    {
        if (string.IsNullOrWhiteSpace(product.PhotoFileName))
        {
            return null;
        }
        var path = Path.Combine(AppPaths.PhotosFolder, product.PhotoFileName);
        return File.Exists(path) ? path : null;
    }

    private void OpenProductPhoto(int productId)
    {
        var product = _catalog.FirstOrDefault(row => row.Id == productId);
        if (product is null)
        {
            SetStatus("That medicine is no longer in the active catalog. Refresh and try again.", true);
            return;
        }
        PhotoWindow.Show(_services, _user, PhotoEntity.Product, product.Id, product.Name);
        LoadLookups();
    }

    /// <summary>
    /// A cart always starts with the ordinary retail rate. An edited rate is passed to Core as the
    /// existing integer-paisa override; leaving it equal to retail keeps the VIP/Doctor choice open.
    /// </summary>
    private PosCartLine NewCartLine(int productId, string name, string unit, double quantity, long savedRate)
    {
        var product = _catalog.FirstOrDefault(item => item.Id == productId);
        var guideRate = product is null ? 0 : Calc.Round(product.RetailPaisa * product.QuantityPerSelectedUnit(unit));
        var rate = savedRate > 0 ? savedRate : guideRate;
        return new PosCartLine
        {
            ProductId = productId,
            Name = name,
            Unit = unit,
            Quantity = quantity,
            GuideRatePaisa = guideRate,
            RateText = Money.Edit(rate),
            RateChanged = Recalculate
        };
    }

    private static long RateOverride(PosCartLine line)
    {
        if (!Money.TryParse(line.RateText, out var rupees))
        {
            return 0;
        }

        var rate = Money.ToPaisa(rupees);
        return rate == line.GuideRatePaisa ? 0 : rate;
    }

    private bool RatesAreValid()
    {
        foreach (var line in Cart)
        {
            if (!Money.TryParse(line.RateText, out var rupees) || Money.ToPaisa(rupees) <= 0)
            {
                SetStatus($"{line.Name}: selling rate must be a number above zero.", true);
                return false;
            }

            if (!_user.CanManage && RateOverride(line) != 0)
            {
                SetStatus($"{line.Name}: only the owner or a manager can enter a custom selling rate.", true);
                return false;
            }
        }

        return true;
    }

    private void AddToCart()
    {
        if (IsBlocked)
        {
            SetStatus("No bill can be made until the drawer is opened - see the card above.", true);
            return;
        }

        if (SelectedProduct is null)
        {
            SetStatus("Pick a medicine first.", true);
            return;
        }

        // Scanning/adding the next customer's first item is itself a quick continuation.
        ShowSaleSuccess = false;

        if (!Qty.TryParse(Quantity, out var quantity) || quantity <= 0)
        {
            SetStatus("Quantity must be greater than zero.", true);
            return;
        }

        var existing = Cart.FirstOrDefault(line => line.ProductId == SelectedProduct.Id && line.Unit == Unit);
        if (existing is not null)
        {
            existing.Quantity += quantity;
            Recalculate();
        }
        else
        {
            Cart.Add(NewCartLine(SelectedProduct.Id, SelectedProduct.Name, Unit, quantity, 0));
        }

        Quantity = "1";
        SetStatus($"{SelectedProduct.Name} added.", false);
        Recalculate();
    }

    private void RemoveLast()
    {
        if (Cart.Count == 0)
        {
            return;
        }

        Cart.RemoveAt(Cart.Count - 1);
        Recalculate();
    }

    private void ClearCart()
    {
        Cart.Clear();
        TenderedText = string.Empty;
        DiscountPercent = "0";
        SetStatus("Cart cleared.", false);
        Recalculate();
    }

    private void CompleteSale()
    {
        // Asked again here even though the page shows a card: the drawer can be closed from another
        // window while this cart is open, and the bill is the last place it may be discovered.
        ShowSaleSuccess = false;
        CheckDrawer();

        if (IsBlocked)
        {
            SetStatus("No bill can be made until the drawer is opened - see the card above.", true);
            return;
        }

        if (Cart.Count == 0)
        {
            SetStatus("The cart is empty.", true);
            return;
        }

        if (!RatesAreValid())
        {
            return;
        }

        if (!Money.TryParse(TenderedText, out _))
        {
            SetStatus("Cash received must be a number.", true);
            return;
        }

        var useSpecialRate = false;
        if (!TryGetDiscount(out var discount))
        {
            SetStatus("Discount must be a number between 0 and " + MaxDiscountPercent + ".", true);
            return;
        }

        // PRICE-01: with no explicit discount, an applicable special rate is a question, never a
        // default. No keeps retail; Yes quotes and commits the customer's VIP/Doctor rate. Editing
        // a line's actual rate remains the final explicit override inside SaleService.
        var customerCategory = SelectedCustomer?.Category?.Trim().ToLowerInvariant();
        if (discount == 0 && SelectedCustomer is { Id: > 0 } && customerCategory is "vip" or "doctor")
        {
            var specialQuote = _services.Sales.Quote(BuildInput(useSpecialRate: true), _user);
            var normalQuote = _services.Sales.Quote(BuildInput(), _user);
            if (specialQuote.Ok && specialQuote.Value is not null &&
                normalQuote.Ok && normalQuote.Value is not null &&
                specialQuote.Value.SubtotalPaisa != normalQuote.Value.SubtotalPaisa)
            {
                var categoryLabel = customerCategory == "doctor" ? "Doctor" : "VIP";
                var answer = MessageBox.Show(
                    $"{categoryLabel} special rate is available for {SelectedCustomer.Name}.\n\n" +
                    $"Retail subtotal: {Money.Format(normalQuote.Value.SubtotalPaisa)}\n" +
                    $"Special subtotal: {Money.Format(specialQuote.Value.SubtotalPaisa)}\n\n" +
                    "Yes = apply the special rate.  No = keep ordinary retail.",
                    "Special selling rate", MessageBoxButton.YesNo, MessageBoxImage.Question,
                    MessageBoxDefaultButton.Button2);
                useSpecialRate = answer == MessageBoxResult.Yes;
            }
        }

        var result = _services.Sales.Create(BuildInput(useSpecialRate), _user);
        if (!result.Ok || result.Value is null)
        {
            SetStatus(result.Error ?? "The bill could not be saved.", true);
            return;
        }

        var sale = result.Value;
        LastBill = $"{sale.Code} · total {Money.Format(sale.TotalPaisa)}" +
                   (sale.ChangePaisa > 0 ? $" · change {Money.Format(sale.ChangePaisa)}" : string.Empty) +
                   (sale.DuePaisa > 0 ? $" · due {Money.Format(sale.DuePaisa)}" : string.Empty);

        HasSuccessChange = sale.ChangePaisa > 0;
        SuccessChangeText = HasSuccessChange
            ? "Give this cash back: " + Money.Format(sale.ChangePaisa)
            : string.Empty;

        var warnings = sale.Warnings.Count > 0 ? "  ⚠ " + string.Join(" ", sale.Warnings) : string.Empty;
        SetStatus($"Bill saved.{warnings}", sale.Warnings.Count > 0);

        // Owner rules D-05 / D-23: printing is instant, with no dialog. The bill is already saved,
        // so a printer problem is a warning on this screen - never a lost sale (Z-07).
        _lastSaleId = sale.SaleId;
        PrintLastCommand.RaiseCanExecuteChanged();

        if (AutoPrint)
        {
            Print(_lastSaleId);
        }

        // Post-commit only: this lightweight card cannot save, undo, print, or duplicate anything.
        ShowSaleSuccess = _services.Settings.GetBool("sale.successPopup", true);

        Cart.Clear();
        TenderedText = string.Empty;
        SecondAmountText = string.Empty;
        DiscountPercent = "0";
        LoadLookups();
    }

    private bool TryGetDiscount(out decimal percent)
    {
        return decimal.TryParse(DiscountPercent, NumberStyles.Number, CultureInfo.InvariantCulture, out percent) &&
               percent >= 0 && percent <= MaxDiscountPercent;
    }

    private SaleInput BuildInput(bool useSpecialRate = false)
    {
        decimal.TryParse(DiscountPercent, NumberStyles.Number, CultureInfo.InvariantCulture, out var percent);

        var tendered = 0L;
        if (IsCashOrSplit && Money.TryParse(TenderedText, out var rupees) && rupees > 0)
        {
            tendered = Money.ToPaisa(rupees);
        }

        // Split: the cashier types only the second half. Core folds the difference into cash and
        // refuses the bill if the parts add up to more than the total (Spec 13.3.2).
        var payments = new List<SalePaymentInput>();
        if (IsSplit && Money.TryParse(SecondAmountText, out var secondRupees) && secondRupees > 0)
        {
            payments.Add(new SalePaymentInput
            {
                Method = SecondMethod,
                AmountPaisa = Money.ToPaisa(secondRupees)
            });
        }

        // A whole number of points, never more than the customer has; the Core caps again, so a
        // typed 9999 cannot spend more than the khata owns.
        var redeem = 0;
        if (CanRedeem && int.TryParse((RedeemPointsText ?? string.Empty).Trim(), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var typed))
        {
            redeem = Math.Max(0, Math.Min(typed, SelectedCustomer?.LoyaltyPoints ?? 0));
        }

        return new SaleInput
        {
            CustomerId = SelectedCustomer is { Id: > 0 } ? SelectedCustomer.Id : null,
            DoctorId = SelectedDoctor is { Id: > 0 } ? SelectedDoctor.Id : null,
            DiscountPercent = percent,
            UseSpecialRate = useSpecialRate,
            PaymentMethod = PaymentMethod,
            Payments = payments,
            RedeemPoints = redeem,
            TenderedPaisa = tendered,
            Lines = Cart.Select(line => new SaleLineInput
            {
                ProductId = line.ProductId,
                Unit = line.Unit,
                Quantity = line.Quantity,
                UnitPricePaisa = RateOverride(line)
            }).ToList()
        };
    }

    private void Recalculate()
    {
        if (Cart.Count == 0)
        {
            SubtotalText = "Rs 0";
            DiscountText = "Rs 0";
            TotalText = "Rs 0";
            ChangeText = "Rs 0";
            CashPartText = "Rs 0";
            CreditPartText = "Rs 0";
            PointsUsedText = string.Empty;
            HasPointsUsed = false;
            return;
        }

        var quote = _services.Sales.Quote(BuildInput(), _user);
        if (!quote.Ok || quote.Value is null)
        {
            SubtotalText = "—";
            DiscountText = "—";
            TotalText = "—";
            ChangeText = "—";
            SetStatus(quote.Error ?? "The total could not be calculated.", true);
            return;
        }

        SubtotalText = Money.Format(quote.Value.SubtotalPaisa);
        DiscountText = Money.Format(quote.Value.DiscountPaisa);
        TotalText = Money.Format(quote.Value.TotalPaisa);

        HasPointsUsed = quote.Value.RedeemPoints > 0;
        PointsUsedText = HasPointsUsed
            ? quote.Value.RedeemPoints + " point(s): -" + Money.Format(quote.Value.RedeemPaisa)
            : string.Empty;
        ChangeText = Money.Format(quote.Value.ChangePaisa);
        CashPartText = Money.Format(quote.Value.CashPartPaisa);
        CreditPartText = Money.Format(quote.Value.CreditPartPaisa);
    }

    private void PrintLast()
    {
        if (_lastSaleId <= 0)
        {
            SetStatus("There is no bill to print yet.", true);
            return;
        }

        Print(_lastSaleId);
    }

    private void Print(int saleId)
    {
        var document = _services.Receipts.Build(saleId);
        if (!document.Ok || document.Value is null)
        {
            SetStatus(document.Error ?? "The receipt could not be prepared.", true);
            return;
        }

        var printed = _services.Printer.Print(document.Value);
        if (!printed.Ok)
        {
            SetStatus(printed.Error ?? "The receipt could not be printed.", true);
            return;
        }

        SetStatus($"Receipt printed ({document.Value.Width} characters wide).", false);
    }

    // ------------------------------ Held carts ------------------------------

    /// <summary>Parks the cart so the next customer can be served (Spec 8.5.1). Nothing is sold.</summary>
    private void HoldCart()
    {
        if (Cart.Count == 0)
        {
            SetStatus("The cart is empty - there is nothing to park.", true);
            return;
        }

        if (!RatesAreValid())
        {
            return;
        }

        var result = _services.Holds.Hold(BuildInput(), _user);
        if (!result.Ok || string.IsNullOrEmpty(result.Value))
        {
            SetStatus(result.Error ?? "The cart could not be parked.", true);
            return;
        }

        var code = result.Value;
        Cart.Clear();
        TenderedText = string.Empty;
        SecondAmountText = string.Empty;
        DiscountPercent = "0";
        Recalculate();
        LoadHeldCarts();
        SetStatus($"{code} parked. The counter is free for the next customer.", false);
    }

    private void LoadHeldCarts()
    {
        var result = _services.Holds.List(_user);

        HeldCarts.Clear();

        if (result.Ok && result.Value is not null)
        {
            foreach (var summary in result.Value)
            {
                HeldCarts.Add(new HeldCartRow(summary, ResumeHeld, DiscardHeld));
            }
        }
        else if (result.Error is not null)
        {
            SetStatus(result.Error, true);
        }

        OnPropertyChanged(nameof(HeldCartsCaption));
    }

    private void ResumeHeld(HeldCartRow row)
    {
        if (Cart.Count > 0)
        {
            SetStatus("The current cart is not empty. Complete it or park it first.", true);
            return;
        }

        var result = _services.Holds.Resume(_user, row.Id);
        if (!result.Ok || result.Value is null)
        {
            SetStatus(result.Error ?? "The cart could not be brought back.", true);
            LoadHeldCarts();
            return;
        }

        var cart = result.Value;

        foreach (var line in cart.Lines)
        {
            // The parked cart already stores integer-paisa custom rates; do not silently replace
            // them with retail when the customer returns to the counter.
            Cart.Add(NewCartLine(line.ProductId, line.Name, line.Unit, line.Quantity, line.UnitPricePaisa));
        }

        DiscountPercent = cart.DiscountPercent.ToString("0.##", CultureInfo.InvariantCulture);

        // A parked customer may have been switched off since. Fall back to the walk-in line rather
        // than binding the bill to somebody the shop no longer serves.
        var walkIn = Customers.Count > 0 ? Customers[0] : null;
        var parkedCustomer = cart.CustomerId is int customerId
            ? Customers.FirstOrDefault(person => person.Id == customerId)
            : null;

        SelectedCustomer = parkedCustomer ?? walkIn;

        Recalculate();
        LoadHeldCarts();
        SetStatus($"{cart.Code} is back on the counter ({cart.Lines.Count} line(s)).", false);
    }

    private void DiscardHeld(HeldCartRow row)
    {
        var result = _services.Holds.Discard(_user, row.Id);
        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The parked cart could not be thrown away.", true);
            return;
        }

        LoadHeldCarts();
        SetStatus($"{row.Code} thrown away - nothing was sold.", false);
    }

    private void BeginAddCustomer()
    {
        if (!CanAddCustomer)
        {
            SetStatus("You are not allowed to add customers.", true);
            return;
        }

        NewCustomerName = string.Empty;
        NewCustomerPhone = string.Empty;
        IsAddingCustomer = true;

        SetStatus("Type the customer's name (and phone, if they give it) and press Save customer.", false);
    }

    private void CancelAddCustomer()
    {
        IsAddingCustomer = false;
        SetStatus("No customer was added - the cart was not touched.", false);
    }

    private void SaveNewCustomer()
    {
        var saved = _services.Partners.CreateCustomer(_user, NewCustomerName, NewCustomerPhone, "normal");

        if (!saved.Ok)
        {
            SetStatus(saved.Error ?? "The customer could not be saved.", true);
            return;
        }

        // The cart stays exactly as it is; only the name on the bill changes. Reloading the list is
        // what puts the new person in the box - the same list the khata page reads.
        var name = NewCustomerName.Trim();
        IsAddingCustomer = false;
        LoadLookups();

        SelectedCustomer = Customers.FirstOrDefault(person => person.Id == saved.Value) ?? SelectedCustomer;

        SetStatus("Added " + name + " - the khata starts at zero. Pick them on a credit bill.", false);
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsStatusError = isError;
    }
}

/// <summary>One cart line. Quantities are double because the schema stores REAL.</summary>
public sealed class PosCartLine : ObservableObject
{
    private double _quantity;
    private string _rateText = "0";

    public int ProductId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Unit { get; init; } = "base";

    /// <summary>Ordinary retail in paisa, used only to distinguish untouched and custom rates.</summary>
    public long GuideRatePaisa { get; init; }

    /// <summary>Editable rupees per selected unit; SaleService converts and verifies the final rate.</summary>
    public string RateText
    {
        get => _rateText;
        set
        {
            if (SetProperty(ref _rateText, value))
            {
                RateChanged?.Invoke();
            }
        }
    }

    public Action? RateChanged { get; init; }

    public double Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, value))
            {
                OnPropertyChanged(nameof(QuantityText));
            }
        }
    }

    public string QuantityText => Qty.Format(Quantity);

    public string UnitLabel => Unit;
}

/// <summary>
/// One row of the "parked carts" list. It owns its two buttons so the DataTemplate stays dumb; the
/// real work is in PosViewModel, because a row must not talk to a service on its own.
/// </summary>
public sealed class HeldCartRow
{
    public HeldCartRow(HeldCartSummary summary, Action<HeldCartRow> resume, Action<HeldCartRow> discard)
    {
        Id = summary.Id;
        Code = summary.Code;
        TotalLabel = summary.TotalLabel;
        ItemsLabel = summary.ItemsLabel;
        WhenLabel = summary.WhenLabel;
        CustomerLabel = summary.CustomerLabel;

        ResumeCommand = new RelayCommand(() => resume(this));
        DiscardCommand = new RelayCommand(() => discard(this));
    }

    public int Id { get; }

    public string Code { get; }

    public string TotalLabel { get; }

    public string ItemsLabel { get; }

    public string WhenLabel { get; }

    public string CustomerLabel { get; }

    public RelayCommand ResumeCommand { get; }

    public RelayCommand DiscardCommand { get; }
}
