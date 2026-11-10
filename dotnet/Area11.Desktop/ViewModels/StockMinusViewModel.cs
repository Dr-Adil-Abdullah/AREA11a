using System.Collections.ObjectModel;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// The stock-minus registry (F-39, Spec §33.6.6) - the monthly page the owner opens to see **which
/// shelves are below zero and for how long**, and to write down what he found.
///
/// The list is built by <see cref="StockMinusService"/>; this class filters nothing, changes nothing
/// and only collects the note. A review never touches the stock: the stock moves through a
/// stock-take, a return or a write-off, and pretending otherwise would hide the problem the page
/// exists to show (D-56).
/// </summary>
public sealed class StockMinusViewModel : ObservableObject, IEmptyPage
{
    /// <summary>
    /// Why this page has nothing on it, in the shop's words (v0.33.0, D-89). **Null when it
    /// has rows**, so nothing in this page has to keep a second flag in step with its list.
    /// </summary>
    public EmptyHint? EmptyHint => EmptyPage.When(Rows.Count == 0, "Stock minus");

    private readonly AppServices _services;
    private readonly User _user;

    private StockMinusRow? _selected;
    private string _noteText = string.Empty;
    private string _statusText = "Choose a medicine and write what you found. A review changes no stock.";
    private bool _isStatusError;
    private bool _canRead = true;
    private string _summaryText = string.Empty;
    private string _countText = "0";
    private string _totalText = "Rs 0";
    private string _oldestText = "-";
    private string _reviewedText = "0 of 0";

    public StockMinusViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        RefreshCommand = new RelayCommand(Load);
        ReviewCommand = new RelayCommand(Review);

        Load();
    }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand ReviewCommand { get; }

    public ObservableCollection<StockMinusRow> Rows { get; } = new();

    public StockMinusRow? Selected
    {
        get => _selected;
        set
        {
            if (!SetProperty(ref _selected, value))
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedName));
            OnPropertyChanged(nameof(SelectedStock));
            OnPropertyChanged(nameof(SelectedCause));
            OnPropertyChanged(nameof(SelectedReview));
            OnPropertyChanged(nameof(CanReview));
        }
    }

    public string SelectedName => _selected?.NameLabel ?? "Choose a medicine on the left";

    public string SelectedStock => _selected is null
        ? string.Empty
        : _selected.StockText + " - worth " + _selected.ValueText + " at cost";

    public string SelectedCause => _selected?.CauseLabel ?? string.Empty;

    public string SelectedReview => _selected?.ReviewLabel ?? string.Empty;

    /// <summary>True while the chosen medicine still needs the owner's note for this episode.</summary>
    public bool CanReview => _selected is not null && !_selected.IsReviewed;

    public string NoteText
    {
        get => _noteText;
        set => SetProperty(ref _noteText, value);
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

    /// <summary>False when the service refused the page (no permission, or the query failed).</summary>
    public bool CanRead
    {
        get => _canRead;
        private set
        {
            if (SetProperty(ref _canRead, value))
            {
                OnPropertyChanged(nameof(IsNotAllowed));
            }
        }
    }

    public bool IsNotAllowed => !CanRead;

    public bool HasNoRows => Rows.Count == 0;

    public string SummaryText
    {
        get => _summaryText;
        private set => SetProperty(ref _summaryText, value);
    }

    public string CountText
    {
        get => _countText;
        private set => SetProperty(ref _countText, value);
    }

    public string TotalText
    {
        get => _totalText;
        private set => SetProperty(ref _totalText, value);
    }

    public string OldestText
    {
        get => _oldestText;
        private set => SetProperty(ref _oldestText, value);
    }

    public string ReviewedText
    {
        get => _reviewedText;
        private set => SetProperty(ref _reviewedText, value);
    }

    // ------------------------------------------------------------------ loading

    private void Load()
    {
        var keep = _selected?.ProductId;

        var result = _services.StockMinus.Report(_user);

        if (!result.Ok || result.Value is null)
        {
            Rows.Clear();
            CanRead = false;
            SummaryText = result.Error ?? "The stock-minus registry could not be built.";
            SetStatus(result.Error ?? "The stock-minus registry could not be built.", true);
            RaiseListState();
            return;
        }

        var report = result.Value;

        CanRead = true;
        Rows.Clear();

        foreach (var row in report.Rows)
        {
            Rows.Add(row);
        }

        SummaryText = report.SummaryText;
        CountText = report.Count.ToString();
        TotalText = report.TotalText;
        OldestText = report.OldestText;
        ReviewedText = report.ReviewedCount + " of " + report.Count;

        Selected = keep is null
            ? Rows.FirstOrDefault()
            : Rows.FirstOrDefault(row => row.ProductId == keep) ?? Rows.FirstOrDefault();

        RaiseListState();

        SetStatus(report.Count == 0
            ? "Nothing is below zero. Keep this page for the monthly check - it will be empty."
            : "Choose a medicine and write what you found. A review changes no stock.", false);
    }

    private void RaiseListState()
    {
        OnPropertyChanged(nameof(HasNoRows));
        OnPropertyChanged(nameof(CanReview));
    }

    // ------------------------------------------------------------------ review

    private void Review()
    {
        if (_selected is null)
        {
            SetStatus("Choose a medicine on the left first.", true);
            return;
        }

        var name = _selected.Name;
        var saved = _services.StockMinus.Review(_user, _selected.ProductId, NoteText);

        if (!saved.Ok)
        {
            SetStatus(saved.Error ?? "The review could not be saved.", true);
            return;
        }

        NoteText = string.Empty;
        var keep = _selected.ProductId;

        Load();

        // Load() moves the selection back to the same medicine, then this line replaces its status.
        Selected = Rows.FirstOrDefault(row => row.ProductId == keep) ?? Rows.FirstOrDefault();

        SetStatus("Review saved for " + name + ". The stock is unchanged - reconcile it with a physical stock-take.", false);
    }

    private void SetStatus(string text, bool isError)
    {
        StatusText = text;
        IsStatusError = isError;
    }
}
