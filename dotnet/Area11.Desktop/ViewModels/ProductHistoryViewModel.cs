using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// Portion 13: the life story of one medicine on the screen. Every purchase (when, from whom, how
/// much, at what rate) and every sale (when, on which bill, to whom) - oldest first, rates as they
/// were on the day. The profit column shows only behind the same gate as every other margin.
///
/// No rule lives here: <c>ProductHistoryService</c> decides what may be seen and builds the paper,
/// the CSV and the WhatsApp file; this class carries the two lists and the four buttons.
/// </summary>
public sealed class ProductHistoryViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;
    private readonly int _productId;

    private string _totalsText = string.Empty;
    private string _profitText = string.Empty;
    private bool _profitVisible;
    private string _statusText = string.Empty;
    private bool _isStatusError;

    public ProductHistoryViewModel(AppServices services, User user, int productId, string productName)
    {
        _services = services;
        _user = user;
        _productId = productId;

        ProductName = productName;
        Title = "History - " + productName;

        PrintCommand = new RelayCommand(Print);
        WhatsAppCommand = new RelayCommand(SendOnWhatsApp);
        SaveCsvCommand = new RelayCommand(SaveCsv);

        Load();
    }

    /// <summary>The window title carries the shop's app name (v0.69.0, de-workshop).</summary>
    public string WindowTitle => AppDisplayName + " — " + Title;

    private string AppDisplayName
    {
        get
        {
            var name = _services.Settings.GetString("brand.appName").Trim();
            return name.Length == 0 ? "Area11" : name;
        }
    }

    public string Title { get; }

    public string ProductName { get; }

    public ObservableCollection<ProductPurchaseRow> Purchases { get; } = new();

    public ObservableCollection<ProductSaleRow> Sales { get; } = new();

    /// <summary>Bought X / sold Y - numbers everyone on this page may see.</summary>
    public string TotalsText
    {
        get => _totalsText;
        private set => SetProperty(ref _totalsText, value);
    }

    /// <summary>The earned margin, or "hidden" when the gate refuses it.</summary>
    public string ProfitText
    {
        get => _profitText;
        private set => SetProperty(ref _profitText, value);
    }

    /// <summary>True when the per-line profit column may be seen at all.</summary>
    public bool ProfitVisible
    {
        get => _profitVisible;
        private set => SetProperty(ref _profitVisible, value);
    }

    public RelayCommand PrintCommand { get; }

    public RelayCommand WhatsAppCommand { get; }

    public RelayCommand SaveCsvCommand { get; }

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

    private void Load()
    {
        Purchases.Clear();
        Sales.Clear();

        var loaded = _services.ProductHistory.Load(_user, _productId);
        if (!loaded.Ok || loaded.Value is null)
        {
            SetStatus(loaded.Error ?? "The history could not be read.", true);
            return;
        }

        var history = loaded.Value;
        foreach (var row in history.Purchases)
        {
            Purchases.Add(row);
        }

        foreach (var row in history.Sales)
        {
            Sales.Add(row);
        }

        TotalsText = "Bought " + history.BoughtQtyLabel + " (" + Money.Format(history.BoughtPaisa) + ")"
            + "  ·  Sold before bill Extra Discount " + history.SoldQtyLabel + " (" + Money.Format(history.SoldPaisa) + ")";
        ProfitText = history.CanSeeProfit ? Money.Format(history.ProfitPaisa) : "hidden";
        ProfitVisible = history.CanSeeProfit;

        if (!history.HasPurchases && !history.HasSales)
        {
            SetStatus("Nothing bought or sold yet - the story starts with the first bill.", false);
        }
    }

    private void Print()
    {
        var document = _services.ProductHistory.BuildDocument(_user, _productId);
        if (!document.Ok || document.Value is null)
        {
            SetStatus(document.Error ?? "The history could not be prepared.", true);
            return;
        }

        var printed = _services.Printer.Print(document.Value);
        SetStatus(printed.Ok
            ? "History sent to the printer."
            : "The printer did not answer: " + printed.Error, !printed.Ok);
    }

    private void SendOnWhatsApp()
    {
        var document = _services.ProductHistory.BuildDocument(_user, _productId);
        if (!document.Ok || document.Value is null)
        {
            SetStatus(document.Error ?? "The history could not be prepared.", true);
            return;
        }

        // No phone is known here, so the owner picks the contact after the app opens.
        var sent = WhatsAppFlow.Send(_services, null, document.Value.PlainText,
            document.Value.Title, document.Value.Lines.Select(line => line.Text),
            "history-" + ProductName + ".pdf");
        SetStatus(sent.Message, !sent.Ok);
    }

    /// <summary>The story as a CSV file, the Audit precedent: BOM so Excel reads every name.</summary>
    private void SaveCsv()
    {
        var csv = _services.ProductHistory.BuildCsv(_user, _productId);
        if (!csv.Ok || csv.Value is null)
        {
            SetStatus(csv.Error ?? "The history CSV could not be prepared.", true);
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save the history",
            FileName = "Area11-history-" + Clock.Today() + ".csv",
            DefaultExt = ".csv",
            Filter = "CSV file (*.csv)|*.csv|All files (*.*)|*.*",
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            File.WriteAllText(dialog.FileName, csv.Value, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            IsStatusError = false;
            StatusText = (Purchases.Count + Sales.Count).ToString(CultureInfo.InvariantCulture)
                + " line(s) saved to " + dialog.FileName;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SetStatus("The file could not be saved (" + exception.Message + ").", true);
        }
    }

    private void SetStatus(string text, bool isError)
    {
        StatusText = text;
        IsStatusError = isError;
    }
}
