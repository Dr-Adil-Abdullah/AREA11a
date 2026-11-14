using System.Globalization;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>Edits a proposal only; the caller quotes the whole cart through Core before acceptance.</summary>
// empty-page: none - this owned item-edit form always shows a proposal or a validation message.
public sealed class LinePricingViewModel : ObservableObject
{
    private readonly Func<string, long, decimal, string, ServiceResult<SaleLinePricing>> _quote;
    private readonly bool _showCost;
    private string _mode, _basis, _rate, _percent, _preview = string.Empty;

    public LinePricingViewModel(string appName, string title, string reference, string mode, string basis, string rate,
        decimal percent, bool showCost, Func<string, long, decimal, string, ServiceResult<SaleLinePricing>> quote)
    {
        WindowTitle = (string.IsNullOrWhiteSpace(appName) ? "Area11" : appName) + " — Item price / discount";
        Title = title;
        Reference = reference;
        _mode = mode;
        _basis = basis;
        _rate = rate;
        _percent = percent.ToString("0.##", CultureInfo.InvariantCulture);
        _showCost = showCost;
        _quote = quote;
        ApplyCommand = new RelayCommand(Apply);
        ResetCommand = new RelayCommand(() => { Mode = "normal"; Apply(); });
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(false));
        RefreshPreview();
    }

    public string WindowTitle { get; }
    public string Title { get; }
    public string Reference { get; }
    public IReadOnlyList<string> Modes { get; } = new[] { "normal", "manual", "discount" };
    public IReadOnlyList<string> Bases { get; } = new[] { "profit", "retail" };
    public string Mode { get => _mode; set { if (SetProperty(ref _mode, value)) RefreshPreview(); } }
    public string Basis { get => _basis; set { if (SetProperty(ref _basis, value)) RefreshPreview(); } }
    public string Rate { get => _rate; set { if (SetProperty(ref _rate, value)) RefreshPreview(); } }
    public string Percent { get => _percent; set { if (SetProperty(ref _percent, value)) RefreshPreview(); } }
    public string Preview { get => _preview; private set => SetProperty(ref _preview, value); }
    public RelayCommand ApplyCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand CancelCommand { get; }
    public Action<bool>? CloseRequested { get; set; }
    public bool Accepted { get; private set; }
    public long RatePaisa { get; private set; }
    public decimal DiscountPercent { get; private set; }

    private bool RefreshPreview()
    {
        var rate = 0m;
        var percent = 0m;
        if ((Mode == "manual" && (!Money.TryParse(Rate, out rate) || rate <= 0 || rate > long.MaxValue / 100m))
            || (Mode == "discount" && !decimal.TryParse(Percent, NumberStyles.Number, CultureInfo.InvariantCulture, out percent)))
        { Preview = "Enter a valid positive rate and a numeric discount percentage."; return false; }
        RatePaisa = Mode == "manual" ? Money.ToPaisa(rate) : 0;
        DiscountPercent = Mode == "discount" ? percent : 0;
        var quote = _quote(Mode, RatePaisa, DiscountPercent, Basis);
        if (!quote.Ok || quote.Value is null) { Preview = quote.Error ?? "This change is not allowed."; return false; }
        var line = quote.Value;
        Preview = "This line before bill rounding/points: " + Money.Format(line.NetPaisa);
        if (_showCost)
        {
            var profit = line.NetPaisa - line.CostPaisa;
            var margin = line.NetPaisa > 0 ? profit * 100m / line.NetPaisa : 0;
            Preview += "\nActual allocated batch cost: " + Money.Format(line.CostPaisa)
                + "\nGross profit: " + Money.Format(profit)
                + " · margin on selling amount: " + margin.ToString("0.##", CultureInfo.InvariantCulture) + "%";
        }
        return true;
    }

    private void Apply()
    {
        if (!RefreshPreview()) return;
        Accepted = true;
        CloseRequested?.Invoke(true);
    }
}
