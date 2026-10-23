using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// The reorder pad (F-38, Spec 10.5 &amp; 20.3). One page answers two questions at the counter's back:
/// **what is running out**, and **what do I send the supplier**.
///
/// The list is built by <see cref="ReorderService"/>; this class only filters it, ticks it and turns
/// the ticked rows into the WhatsApp text. Nothing here touches the database, and nothing here
/// decides a band - the three bands live in one place so the screen and the test cannot disagree.
///
/// The pad is per supplier on purpose: a message that mixes two suppliers is a message nobody can
/// act on, so mixing the ticks is shown as a warning and the send button stays closed.
/// </summary>
public sealed class ReorderViewModel : ObservableObject, IEmptyPage
{
    /// <summary>
    /// Why this page has nothing on it, in the shop's words (v0.33.0, D-89). **Null when it
    /// has rows**, so nothing in this page has to keep a second flag in step with its list.
    /// </summary>
    public EmptyHint? EmptyHint => EmptyPage.When(Rows.Count == 0, "Reorder pad");
    public const string AllSuppliers = "All suppliers";
    public const string NoSupplier = "No supplier yet";

    private readonly AppServices _services;
    private readonly User _user;
    private readonly List<ReorderPadLine> _all = new();

    private ReorderReport? _report;
    private SupplierChoice? _supplier;
    private string _tier = "Everything";
    private string _searchText = string.Empty;
    private ReorderPadLine? _selected;
    private string _levelText = string.Empty;
    private string _phoneText = string.Empty;
    private string _noteText = string.Empty;
    private string _orderText = string.Empty;
    private string _statusText = "Tick the medicines you need and send the text on WhatsApp.";
    private bool _isStatusError;
    private bool _isSwitchedOff;
    private bool _hasMixedSuppliers;
    private string _summaryText = string.Empty;

    public ReorderViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        StoreName = services.Settings.GetString("store.name");

        RefreshCommand = new RelayCommand(Load);
        SaveLevelCommand = new RelayCommand(SaveLevel);
        CopyCommand = new RelayCommand(CopyText);
        OpenWhatsAppCommand = new RelayCommand(OpenWhatsApp);
        TickAllCommand = new RelayCommand(() => SetAllTicked(true));
        UntickAllCommand = new RelayCommand(() => SetAllTicked(false));

        Load();
    }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand SaveLevelCommand { get; }

    public RelayCommand CopyCommand { get; }

    public RelayCommand OpenWhatsAppCommand { get; }

    public RelayCommand TickAllCommand { get; }

    public RelayCommand UntickAllCommand { get; }

    public ObservableCollection<ReorderPadLine> Rows { get; } = new();

    public ObservableCollection<SupplierChoice> Suppliers { get; } = new();

    public IReadOnlyList<string> TierOptions { get; } = new[]
    {
        "Everything", "Out of stock", "Order now", "Running low"
    };

    public string StoreName { get; }

    /// <summary>The supplier the pad is being built for; `null` until the first load finishes.</summary>
    public SupplierChoice? Supplier
    {
        get => _supplier;
        set
        {
            if (!SetProperty(ref _supplier, value) || value is null)
            {
                return;
            }

            // The phone follows the supplier, so the common case needs no typing at all.
            var phone = value.IsAll || value.IsNone
                ? string.Empty
                : _all.FirstOrDefault(line =>
                    string.Equals(line.Row.SupplierName, value.Name, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(line.Row.SupplierPhone))?.Row.SupplierPhone ?? string.Empty;

            PhoneText = phone;
            ApplyFilter();
        }
    }

    public string Tier
    {
        get => _tier;
        set
        {
            if (SetProperty(ref _tier, value))
            {
                ApplyFilter();
            }
        }
    }

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

    /// <summary>The row whose minimum is being edited in the right-hand card.</summary>
    public ReorderPadLine? Selected
    {
        get => _selected;
        set
        {
            if (!SetProperty(ref _selected, value) || value is null)
            {
                return;
            }

            LevelText = Qty.Edit(value.Row.LevelQty);
            OnPropertyChanged(nameof(SelectedName));
            OnPropertyChanged(nameof(SelectedHint));
        }
    }

    public string SelectedName => _selected?.Row.NameLabel ?? "Choose a medicine on the left";

    public string SelectedHint => _selected is null
        ? "Its minimum is its own number - changing it here changes nothing else: not the cost, not the rate, not the pack."
        : _selected.Row.PackLabel + " · " + _selected.Row.StockText + " on the shelf";

    /// <summary>New minimum, as typed. The unit is the medicine's base unit, never a box.</summary>
    public string LevelText
    {
        get => _levelText;
        set => SetProperty(ref _levelText, value);
    }

    /// <summary>The mobile the order goes to. Prefilled from the supplier, editable (an agent's new number).</summary>
    public string PhoneText
    {
        get => _phoneText;
        set => SetProperty(ref _phoneText, value);
    }

    /// <summary>Anything the owner wants added: "Also send 2 boxes of masks".</summary>
    public string NoteText
    {
        get => _noteText;
        set
        {
            if (SetProperty(ref _noteText, value))
            {
                BuildPreview();
            }
        }
    }

    public string OrderText
    {
        get => _orderText;
        private set => SetProperty(ref _orderText, value);
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

    /// <summary>True when the owner turned the reorder list off in Settings.</summary>
    public bool IsSwitchedOff
    {
        get => _isSwitchedOff;
        private set => SetProperty(ref _isSwitchedOff, value);
    }

    /// <summary>True when the ticks belong to more than one supplier - sending is disabled.</summary>
    public bool HasMixedSuppliers
    {
        get => _hasMixedSuppliers;
        private set
        {
            if (SetProperty(ref _hasMixedSuppliers, value))
            {
                OnPropertyChanged(nameof(CanSend));
            }
        }
    }

    public string SummaryText
    {
        get => _summaryText;
        private set => SetProperty(ref _summaryText, value);
    }

    /// <summary>True when the chosen filter shows nothing - the page then says why.</summary>
    public bool HasNoRows => Rows.Count == 0;

    /// <summary>Send is allowed only with a real line on the pad and a single supplier.</summary>
    public bool CanSend => SendableCount > 0 && !HasMixedSuppliers && !IsSwitchedOff;

    public int TickedCount => Rows.Count(line => line.IsSelected);

    /// <summary>Ticked lines that carry a quantity - a tick with an empty box is not an order.</summary>
    public int SendableCount => Rows.Count(line => line.IsSelected && line.OrderQty > 0);

    public string ShownText => Rows.Count == 0
        ? "Nothing to show with this filter."
        : Rows.Count + " shown - " + SendableCount + " on the pad";

    // ------------------------------------------------------------------ loading

    private void Load()
    {
        var result = _services.Reorder.Report(_user);

        if (!result.Ok || result.Value is null)
        {
            _all.Clear();
            Suppliers.Clear();
            Rows.Clear();
            _report = null;
            SummaryText = result.Error ?? "The reorder list could not be built.";
            SetStatus(result.Error ?? "The reorder list could not be built.", true);
            RaiseListState();
            return;
        }

        _report = result.Value;
        IsSwitchedOff = _report.IsSwitchedOff;
        SummaryText = _report.SummaryText;

        var keep = _supplier?.Name ?? AllSuppliers;

        Suppliers.Clear();
        Suppliers.Add(new SupplierChoice(AllSuppliers));

        foreach (var name in _report.SupplierNames)
        {
            Suppliers.Add(new SupplierChoice(name));
        }

        if (_report.Rows.Any(row => !row.HasSupplier))
        {
            Suppliers.Add(new SupplierChoice(NoSupplier));
        }

        // Opening the page should offer something to send: the supplier with the longest list wins,
        // because that is the order the shop would otherwise forget (D-55).
        _supplier = Suppliers.FirstOrDefault(choice => choice.Name == keep)
            ?? Suppliers.FirstOrDefault(choice => !choice.IsAll)
            ?? Suppliers[0];

        if (!_supplier.IsAll && !_supplier.IsNone)
        {
            PhoneText = _report.Rows
                .Where(row => string.Equals(row.SupplierName, _supplier.Name, StringComparison.OrdinalIgnoreCase))
                .Select(row => row.SupplierPhone)
                .FirstOrDefault(phone => !string.IsNullOrWhiteSpace(phone)) ?? string.Empty;
        }

        OnPropertyChanged(nameof(Supplier));

        _all.Clear();
        foreach (var row in _report.Rows)
        {
            _all.Add(new ReorderPadLine(row, BuildPreview));
        }

        ApplyFilter();
        SetStatus(IsSwitchedOff
            ? "The reorder list is switched off in Settings. Nothing will be suggested until it is on."
            : "Tick the medicines you need and send the text on WhatsApp.", false);
    }

    private void ApplyFilter()
    {
        var term = (SearchText ?? string.Empty).Trim();
        var keep = _selected;

        Rows.Clear();

        foreach (var line in _all)
        {
            if (!MatchesSupplier(line.Row))
            {
                continue;
            }

            if (!MatchesTier(line.Row))
            {
                continue;
            }

            if (term.Length > 0
                && !line.Row.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                && !(line.Row.Generic ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Rows.Add(line);
        }

        // The right-hand card keeps its medicine when the filter is nudged; otherwise it takes the
        // first row so the page is never half-empty for no reason.
        Selected = keep is not null && Rows.Contains(keep) ? keep : Rows.FirstOrDefault();

        OnPropertyChanged(nameof(SelectedName));
        OnPropertyChanged(nameof(SelectedHint));

        BuildPreview();
        RaiseListState();
    }

    private bool MatchesSupplier(ReorderRow row)
    {
        if (_supplier is null || _supplier.IsAll)
        {
            return true;
        }

        if (_supplier.IsNone)
        {
            return !row.HasSupplier;
        }

        return string.Equals(row.SupplierName, _supplier.Name, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesTier(ReorderRow row) => Tier switch
    {
        "Out of stock" => row.Tier == ReorderTier.Out,
        "Order now" => row.Tier == ReorderTier.Critical,
        "Running low" => row.Tier == ReorderTier.Low,
        _ => true
    };

    private void RaiseListState()
    {
        OnPropertyChanged(nameof(HasNoRows));
        OnPropertyChanged(nameof(ShownText));
        OnPropertyChanged(nameof(TickedCount));
        OnPropertyChanged(nameof(SendableCount));
        OnPropertyChanged(nameof(CanSend));
    }

    // ------------------------------------------------------------------ the pad

    /// <summary>
    /// Rebuilds the message from the ticked, visible rows. Only the rows on the screen can be sent -
    /// a hidden tick behind a filter must never sneak into an order.
    /// </summary>
    private void BuildPreview()
    {
        var ticked = Rows.Where(line => line.IsSelected && line.OrderQty > 0).ToList();
        var groups = ticked
            .Select(line => line.Row.SupplierName ?? string.Empty)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        HasMixedSuppliers = groups.Count > 1;

        var oneSupplier = groups.Count == 1 && !string.IsNullOrWhiteSpace(groups[0])
            ? groups[0]
            : null;

        var lines = ticked.Select(line => line.ToLine()).ToList();

        OrderText = ReorderService.BuildText(lines, oneSupplier, NoteText, StoreName);

        if (!IsStatusError && groups.Count > 1)
        {
            SetStatus("You have ticked medicines from more than one supplier. Tick one supplier at a time, or choose a supplier above.", true);
        }

        OnPropertyChanged(nameof(TickedCount));
        OnPropertyChanged(nameof(SendableCount));
        OnPropertyChanged(nameof(ShownText));
        OnPropertyChanged(nameof(CanSend));
    }

    private void SetAllTicked(bool ticked)
    {
        foreach (var line in Rows)
        {
            line.IsSelected = ticked;
        }

        BuildPreview();
    }

    // ------------------------------------------------------------------ actions

    private void SaveLevel()
    {
        if (_selected is null)
        {
            SetStatus("Choose a medicine on the left first.", true);
            return;
        }

        if (!Qty.TryParse(LevelText, out var level))
        {
            SetStatus("The minimum must be a number, for example 20.", true);
            return;
        }

        var productId = _selected.Row.ProductId;
        var unit = _selected.Row.BaseUnit;
        var saved = _services.Reorder.SetLevel(_user, productId, level);

        if (!saved.Ok)
        {
            SetStatus(saved.Error ?? "The minimum could not be saved.", true);
            return;
        }

        Load();

        // Come back to the same medicine: the owner is usually setting several minima in a row.
        var again = _all.FirstOrDefault(line => line.Row.ProductId == productId);
        if (again is not null)
        {
            Selected = again;
        }

        SetStatus("Minimum saved: " + Qty.Format(level) + " " + unit + ".", false);
    }

    private void CopyText()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(OrderText))
            {
                SetStatus("There is nothing to copy yet.", true);
                return;
            }

            Clipboard.SetText(OrderText);
            SetStatus("The order text is on the clipboard. Paste it into WhatsApp or a message.", false);
        }
        catch (Exception exception)
        {
            // The clipboard can be held by another program; the text is still on the screen.
            SetStatus("The text could not be copied (" + exception.Message + "). Select it by hand.", true);
        }
    }

    private void OpenWhatsApp()
    {
        if (!CanSend)
        {
            SetStatus(HasMixedSuppliers
                ? "One order, one supplier. Untick the other supplier's medicines first."
                : "Tick at least one medicine with a quantity.", true);
            return;
        }

        // The audit names the supplier the MESSAGE names, not the one in the filter: the owner may
        // be looking at "All suppliers" while every ticked line belongs to one agent.
        var supplierName = TickedSupplier();
        var link = ReorderService.BuildLink(PhoneText, OrderText);

        try
        {
            Process.Start(new ProcessStartInfo { FileName = link, UseShellExecute = true });

            // Leaving the shop is a business event: who, how many, and when (Spec 22).
            _services.Reorder.RecordOrder(_user, supplierName, SendableCount);

            SetStatus("WhatsApp opened with the order. Send it from there.", false);
        }
        catch (Exception exception)
        {
            SetStatus("WhatsApp could not be opened (" + exception.Message + "). Copy the text instead.", true);
        }
    }

    /// <summary>The single supplier behind the ticked, sendable rows - null when they are mixed or blank.</summary>
    private string? TickedSupplier()
    {
        var groups = Rows
            .Where(line => line.IsSelected && line.OrderQty > 0)
            .Select(line => line.Row.SupplierName ?? string.Empty)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return groups.Count == 1 && !string.IsNullOrWhiteSpace(groups[0]) ? groups[0] : null;
    }

    private void SetStatus(string text, bool isError)
    {
        StatusText = text;
        IsStatusError = isError;
    }
}

/// <summary>One supplier in the filter, or "All suppliers" / "No supplier yet".</summary>
public sealed class SupplierChoice
{
    public SupplierChoice(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public bool IsAll => Name == ReorderViewModel.AllSuppliers;

    public bool IsNone => Name == ReorderViewModel.NoSupplier;

    public override string ToString() => Name;
}

/// <summary>
/// One row of the pad: the service's row plus the two things only the screen owns - the tick and the
/// quantity the owner typed. Any change tells the page to rebuild the message at once.
/// </summary>
public sealed class ReorderPadLine : ObservableObject
{
    private readonly Action _changed;
    private bool _isSelected;

    private string _orderQtyText;

    public ReorderPadLine(ReorderRow row, Action changed)
    {
        Row = row;
        _changed = changed;
        _orderQtyText = Qty.Edit(row.SuggestQty);

        // Everything the shop is short of starts ticked: the common case is "send the whole list",
        // and unticking one line is faster than ticking twenty.
        _isSelected = row.SuggestQty > 0;
    }

    public ReorderRow Row { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                _changed();
            }
        }
    }

    public string OrderQtyText
    {
        get => _orderQtyText;
        set
        {
            if (SetProperty(ref _orderQtyText, value))
            {
                _changed();
            }
        }
    }

    public double OrderQty => Qty.TryParse(OrderQtyText, out var value) && value > 0 ? value : 0;

    /// <summary>"out" / "critical" / "low" - the colour trigger on the row.</summary>
    public string Tone => Row.Tier;

    // The grid binds to the row, not to the page, and the checker resolves those names here - so the
    // wrapper repeats the few labels it shows instead of making the view reach through "Row.".
    public string NameLabel => Row.NameLabel;

    public string StockText => Row.StockText;

    public string LevelText => Row.LevelText;

    public string SupplierLabel => Row.SupplierLabel;

    public string TierLabel => Row.TierLabel;

    public ReorderLine ToLine() => new()
    {
        Name = Row.Name,
        Generic = Row.Generic,
        Company = Row.Company,
        Quantity = OrderQty,
        BaseUnit = Row.BaseUnit,
        BoxStrips = Row.BoxStrips,
        StripTablets = Row.StripTablets
    };
}
