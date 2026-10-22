using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// Import, export and a bulk price change (F-37, Spec 24) - three tabs, one file at a time.
///
/// The shape is the same on every tab, and it is the shape the owner asked for without naming it:
/// **look first, write second.** A file is read and shown line by line (OK / already there / the
/// reason), and nothing reaches the shop until the button at the bottom is pressed. A shop that
/// imports a wrong stock list on its first day spends a week finding out which numbers are wrong.
///
/// The work - parsing, checking, one transaction, the audit lines - is in <see cref="ImportService"/>.
/// This class opens dialogs and moves text.
/// </summary>
public sealed class ImportExportViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;

    private int _tab;
    private string _kind = "Products";
    private ImportPreview? _preview;
    private string _fileName = string.Empty;
    private string _statusText = "Choose a file - nothing is written until you press Import.";
    private bool _isStatusError;

    private PriceChangeKind _priceKind = PriceChangeKind.Percent;
    private string _priceAmountText = "0";
    private bool _includeCost;
    private CategoryChoice? _priceCategory;
    private PricePreview? _pricePreview;

    public ImportExportViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        ChooseFileCommand = new RelayCommand(ChooseFile);
        ImportCommand = new RelayCommand(ApplyImport);
        ClearCommand = new RelayCommand(ClearPreview);
        SaveTemplateCommand = new RelayCommand(SaveTemplate);
        ExportProductsCommand = new RelayCommand(() => Export("Products"));
        ExportCustomersCommand = new RelayCommand(() => Export("Customers"));
        ExportSuppliersCommand = new RelayCommand(() => Export("Suppliers"));
        PreviewPricesCommand = new RelayCommand(PreviewPrices);
        ApplyPricesCommand = new RelayCommand(ApplyPrices);

        LoadCategories();
    }

    public RelayCommand ChooseFileCommand { get; }

    public RelayCommand ImportCommand { get; }

    public RelayCommand ClearCommand { get; }

    public RelayCommand SaveTemplateCommand { get; }

    public RelayCommand ExportProductsCommand { get; }

    public RelayCommand ExportCustomersCommand { get; }

    public RelayCommand ExportSuppliersCommand { get; }

    public RelayCommand PreviewPricesCommand { get; }

    public RelayCommand ApplyPricesCommand { get; }

    public ObservableCollection<ImportRow> Rows { get; } = new();

    public ObservableCollection<PriceChangeRow> PriceRows { get; } = new();

    public ObservableCollection<CategoryChoice> Categories { get; } = new();

    /// <summary>0 = Import, 1 = Export, 2 = Prices.</summary>
    public int SelectedTabIndex
    {
        get => _tab;
        set => SetProperty(ref _tab, Math.Clamp(value, 0, 2));
    }

    public IReadOnlyList<string> Kinds { get; } = new[] { "Products", "Customers", "Suppliers" };

    /// <summary>Which list is being imported. Changing it throws away the preview of the old one.</summary>
    public string Kind
    {
        get => _kind;
        set
        {
            if (SetProperty(ref _kind, value))
            {
                ClearPreview();
                OnPropertyChanged(nameof(KindHint));
            }
        }
    }

    public string KindHint => Kind switch
    {
        "Customers" => "Name, Phone, Category, Opening udhaar, Credit limit. The udhaar becomes the first line of the khata.",
        "Suppliers" => "Name, Phone, Agency, Opening payable. The payable is what the shop already owes him.",
        _ => "Name, Retail, Cost, Opening qty, Batch no, Expiry. A medicine with opening stock must have an expiry date."
    };

    public string FileNameText => string.IsNullOrWhiteSpace(_fileName) ? "No file chosen yet." : _fileName;

    public string PreviewText => _preview is null
        ? "Choose a CSV file made in Excel (File → Save As → CSV UTF-8)."
        : _preview.SummaryText;

    public bool HasPreview => _preview is not null && _preview.Rows.Count > 0;

    /// <summary>
    /// The opposite, written as its own property: the only converter in this program is
    /// BoolToVis, and it cannot be told to invert. A ConverterParameter is silently ignored by WPF,
    /// so the empty-state text would simply never appear.
    /// </summary>
    public bool HasNoPreview => !HasPreview;

    public bool CanImport => _preview?.CanApply ?? false;

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

    // ------------------------------ Prices ------------------------------

    public bool IsPercent
    {
        get => _priceKind == PriceChangeKind.Percent;
        set
        {
            if (value)
            {
                PriceKind = PriceChangeKind.Percent;
            }
        }
    }

    public bool IsRupees
    {
        get => _priceKind == PriceChangeKind.Rupees;
        set
        {
            if (value)
            {
                PriceKind = PriceChangeKind.Rupees;
            }
        }
    }

    public PriceChangeKind PriceKind
    {
        get => _priceKind;
        private set
        {
            if (SetProperty(ref _priceKind, value))
            {
                OnPropertyChanged(nameof(IsPercent));
                OnPropertyChanged(nameof(IsRupees));
                OnPropertyChanged(nameof(AmountHint));
                _pricePreview = null;
                PriceRows.Clear();
                RaisePriceState();
            }
        }
    }

    public string AmountHint => IsPercent
        ? "Percent: 10 raises every price by ten percent, -5 lowers it. Decimals are allowed."
        : "Rupees: 5 adds five rupees to every price, -5 takes five off.";

    public string PriceAmountText
    {
        get => _priceAmountText;
        set => SetProperty(ref _priceAmountText, value);
    }

    public bool IncludeCost
    {
        get => _includeCost;
        set => SetProperty(ref _includeCost, value);
    }

    /// <summary>null = every category.</summary>
    public CategoryChoice? PriceCategory
    {
        get => _priceCategory;
        set => SetProperty(ref _priceCategory, value);
    }

    public string PriceSummary => _pricePreview?.SummaryText ?? "Press Preview to see every price before and after.";

    public bool HasPriceRows => PriceRows.Count > 0;

    public bool HasNoPriceRows => !HasPriceRows;

    public bool CanApplyPrices => _pricePreview?.CanApply ?? false;

    // ------------------------------ Import ------------------------------

    private void ChooseFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose the " + Kind.ToLowerInvariant() + " file",
            Filter = "CSV file (*.csv)|*.csv|Text file (*.txt)|*.txt|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            // Shared read/write: the owner may still have the file open in Excel, and refusing then
            // is a support call. Excel holds its copy open, not this one.
            using var stream = new FileStream(dialog.FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            var text = reader.ReadToEnd();

            _fileName = Path.GetFileName(dialog.FileName);
            _preview = Kind switch
            {
                "Customers" => _services.Import.PreviewCustomers(text),
                "Suppliers" => _services.Import.PreviewSuppliers(text),
                _ => _services.Import.PreviewProducts(text)
            };

            FillRows();

            IsStatusError = _preview.ErrorCount > 0;
            StatusText = _preview.ErrorCount > 0
                ? "The file has lines with a problem. Nothing will be imported until they are fixed."
                : _preview.CanApply
                    ? "Read the list, then press Import."
                    : "There is nothing new in this file.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            IsStatusError = true;
            StatusText = "The file could not be read. Is it still open in Excel? (" + exception.Message + ")";
        }

        RaisePreviewState();
    }

    private void FillRows()
    {
        Rows.Clear();

        if (_preview is null)
        {
            return;
        }

        foreach (var row in _preview.Rows)
        {
            Rows.Add(row);
        }
    }

    private void ApplyImport()
    {
        if (_preview is null || !_preview.CanApply)
        {
            SetStatus("There is nothing to import - choose a file first.", true);
            return;
        }

        var result = Kind switch
        {
            "Customers" => Wrap(_services.Import.ApplyCustomers(_user, _preview)),
            "Suppliers" => Wrap(_services.Import.ApplySuppliers(_user, _preview)),
            _ => Wrap(_services.Import.ApplyProducts(_user, _preview))
        };

        if (result.Outcome is null)
        {
            SetStatus(result.Error ?? "The import failed.", true);
            return;
        }

        SetStatus(result.Outcome.SummaryText, false);

        _preview = null;
        Rows.Clear();

        RaisePreviewState();
    }

    /// <summary>The three Apply methods return the same shape but as different generic types.</summary>
    private static (ImportOutcome? Outcome, string? Error) Wrap(ServiceResult<ImportOutcome> result) =>
        result.Ok && result.Value is not null ? (result.Value, null) : (null, result.Error);

    private void ClearPreview()
    {
        _preview = null;
        _fileName = string.Empty;
        Rows.Clear();

        RaisePreviewState();
    }

    /// <summary>Every property the Import tab reads, after the file or the preview changed.</summary>
    private void RaisePreviewState()
    {
        OnPropertyChanged(nameof(FileNameText));
        OnPropertyChanged(nameof(PreviewText));
        OnPropertyChanged(nameof(HasPreview));
        OnPropertyChanged(nameof(HasNoPreview));
        OnPropertyChanged(nameof(CanImport));
    }

    private void SaveTemplate()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save the template",
            FileName = "Area11-" + Kind.ToLowerInvariant() + "-template.csv",
            DefaultExt = ".csv",
            Filter = "CSV file (*.csv)|*.csv",
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            File.WriteAllText(dialog.FileName, ImportService.Template(Kind),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            SetStatus("Template saved. Fill it in Excel and save it as CSV UTF-8, then choose it here.", false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SetStatus("The template could not be written. (" + exception.Message + ")", true);
        }
    }

    // ------------------------------ Export ------------------------------

    private void Export(string kind)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save the " + kind.ToLowerInvariant() + " list",
            FileName = "Area11-" + kind.ToLowerInvariant() + "-" + Clock.Today() + ".csv",
            DefaultExt = ".csv",
            Filter = "CSV file (*.csv)|*.csv",
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var exported = kind switch
        {
            "Customers" => _services.Import.ExportCustomers(_user),
            "Suppliers" => _services.Import.ExportSuppliers(_user),
            _ => _services.Import.ExportProducts(_user)
        };

        if (!exported.Ok || exported.Value is null)
        {
            SetStatus(exported.Error ?? "The list could not be exported.", true);
            return;
        }

        try
        {
            // UTF-8 WITH a BOM: Excel needs it, or the Urdu names arrive as gibberish.
            File.WriteAllText(dialog.FileName, exported.Value,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            SetStatus("Saved to " + dialog.FileName, false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SetStatus("The file could not be written. Close it if it is open in Excel. (" + exception.Message + ")", true);
        }
    }

    // ------------------------------ Prices ------------------------------

    private void LoadCategories()
    {
        Categories.Clear();
        Categories.Add(new CategoryChoice(0, "Every category"));

        var result = _services.Products.GetCategories();

        if (!result.Ok || result.Value is null)
        {
            return;
        }

        foreach (var category in result.Value)
        {
            Categories.Add(new CategoryChoice(category.Id, category.Name));
        }

        PriceCategory = Categories[0];
    }

    private void PreviewPrices()
    {
        if (!decimal.TryParse(PriceAmountText.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            SetStatus("The amount must be a number, for example 10 or -5.", true);
            return;
        }

        var request = new PriceChangeRequest
        {
            Kind = PriceKind,
            Amount = amount,
            CategoryId = PriceCategory?.Id ?? 0,
            IncludeCost = IncludeCost
        };

        _pricePreview = _services.Import.PreviewPrices(_user, request);

        PriceRows.Clear();

        foreach (var row in _pricePreview.Rows.Where(row => row.Changes || row.BelowCost))
        {
            PriceRows.Add(row);
        }

        RaisePriceState();

        SetStatus(_pricePreview.SummaryText, _pricePreview.BelowCostCount > 0);
    }

    private void ApplyPrices()
    {
        if (_pricePreview is null || !_pricePreview.CanApply)
        {
            SetStatus("Preview first - and fix the lines that would fall below cost.", true);
            return;
        }

        var result = _services.Import.ApplyPrices(_user, _pricePreview);

        if (!result.Ok || result.Value is null)
        {
            SetStatus(result.Error ?? "The prices could not be changed.", true);
            return;
        }

        _pricePreview = null;
        PriceRows.Clear();
        RaisePriceState();

        SetStatus(result.Value.SummaryText, false);
    }

    private void RaisePriceState()
    {
        OnPropertyChanged(nameof(PriceSummary));
        OnPropertyChanged(nameof(CanApplyPrices));
        OnPropertyChanged(nameof(HasPriceRows));
        OnPropertyChanged(nameof(HasNoPriceRows));
    }

    private void SetStatus(string text, bool isError)
    {
        StatusText = text;
        IsStatusError = isError;
    }
}

/// <summary>One line of the price screen's category drop-down ("Every category" is id 0).</summary>
public sealed class CategoryChoice
{
    public CategoryChoice(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public int Id { get; }

    public string Name { get; }

    public override string ToString() => Name;
}
