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
public sealed class ImportExportViewModel : ObservableObject, IEmptyPage
{
    /// <summary>
    /// Why this page has nothing on it, in the shop's words (v0.33.0, D-89). **Null when it
    /// has rows**, so nothing in this page has to keep a second flag in step with its list.
    /// </summary>
    public EmptyHint? EmptyHint => EmptyPage.When(Rows.Count == 0, "Excel / CSV");

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

    private BulkUpdatePreview? _bulkPreview;
    private string _bulkFileName = string.Empty;
    private bool _includeSettings;

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
        DownloadAllCommand = new RelayCommand(DownloadAll);
        ChooseBulkFileCommand = new RelayCommand(ChooseBulkFile);
        ApplyBulkCommand = new RelayCommand(ApplyBulk);
        ClearBulkCommand = new RelayCommand(ClearBulk);
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

    public RelayCommand DownloadAllCommand { get; }

    public RelayCommand ChooseBulkFileCommand { get; }

    public RelayCommand ApplyBulkCommand { get; }

    public RelayCommand ClearBulkCommand { get; }

    public RelayCommand PreviewPricesCommand { get; }

    public RelayCommand ApplyPricesCommand { get; }

    public ObservableCollection<ImportRow> Rows { get; } = new();

    public ObservableCollection<BulkUpdateRow> BulkRows { get; } = new();

    public ObservableCollection<PriceChangeRow> PriceRows { get; } = new();

    public ObservableCollection<CategoryChoice> Categories { get; } = new();

    /// <summary>0 = Import, 1 = Export, 2 = Bulk update, 3 = Prices.</summary>
    public int SelectedTabIndex
    {
        get => _tab;
        set => SetProperty(ref _tab, Math.Clamp(value, 0, 3));
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
        ? "Choose a real .xlsx workbook or a CSV UTF-8 file. Nothing is written before this preview."
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
            Filter = "Excel workbook (*.xlsx)|*.xlsx|CSV file (*.csv)|*.csv|Text file (*.txt)|*.txt|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var bytes = ReadSharedBytes(dialog.FileName);
            _fileName = Path.GetFileName(dialog.FileName);

            if (string.Equals(Path.GetExtension(dialog.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                _preview = _services.Import.PreviewWorkbook(Kind, bytes);
            }
            else
            {
                using var reader = new StreamReader(new MemoryStream(bytes, writable: false),
                    Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                var text = reader.ReadToEnd();
                _preview = Kind switch
                {
                    "Customers" => _services.Import.PreviewCustomers(text),
                    "Suppliers" => _services.Import.PreviewSuppliers(text),
                    _ => _services.Import.PreviewProducts(text)
                };
            }

            FillRows();

            IsStatusError = _preview.ErrorCount > 0;
            StatusText = _preview.ErrorCount > 0
                ? "The file has lines with a problem. Nothing will be imported until they are fixed."
                : _preview.CanApply
                    ? "Read the list, then press Import."
                    : "There is nothing new in this file.";
        }
        catch (Exception exception)
        {
            _preview = null;
            Rows.Clear();
            IsStatusError = true;
            StatusText = "The file could not be read. " + exception.Message;
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
            Title = "Save the native Excel template",
            FileName = "Area11-import-template.xlsx",
            DefaultExt = ".xlsx",
            Filter = "Excel workbook (*.xlsx)|*.xlsx",
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            File.WriteAllBytes(dialog.FileName, WorkbookService.Template());
            SetStatus("Workbook saved. Fill the Products, Customers and/or Suppliers sheets, then choose it here.", false);
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
            Title = "Save the " + kind.ToLowerInvariant() + " workbook",
            FileName = "Area11-" + kind.ToLowerInvariant() + "-" + Clock.Today() + ".xlsx",
            DefaultExt = ".xlsx",
            Filter = "Excel workbook (*.xlsx)|*.xlsx",
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var exported = _services.Workbooks.ExportList(_user, kind);
        if (!exported.Ok || exported.Value is null)
        {
            SetStatus(exported.Error ?? "The list could not be exported.", true);
            return;
        }

        try
        {
            File.WriteAllBytes(dialog.FileName, exported.Value);
            SetStatus("Native Excel workbook saved to " + dialog.FileName, false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SetStatus("The file could not be written. Close it if it is open in Excel. (" + exception.Message + ")", true);
        }
    }

    // ------------------------------ Native all-data workbook ------------------------------

    public bool IncludeSettings
    {
        get => _includeSettings;
        set
        {
            if (SetProperty(ref _includeSettings, value))
            {
                // A preview made with the other answer must never be applied with this answer.
                ClearBulk();
            }
        }
    }

    public string BulkFileNameText => string.IsNullOrWhiteSpace(_bulkFileName)
        ? "No edited workbook chosen yet."
        : _bulkFileName;

    public string BulkSummary => _bulkPreview?.SummaryText
        ?? "Download your data, edit the workbook, then choose it here for a before → after preview.";

    public bool HasBulkPreview => BulkRows.Count > 0;

    public bool HasNoBulkPreview => !HasBulkPreview;

    public bool CanApplyBulk => _bulkPreview?.CanApply ?? false;

    private void DownloadAll()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Download all shop data",
            FileName = "Area11-data-" + Clock.Today() + ".xlsx",
            DefaultExt = ".xlsx",
            Filter = "Excel workbook (*.xlsx)|*.xlsx",
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var exported = _services.Workbooks.ExportAll(_user);
        if (!exported.Ok || exported.Value is null)
        {
            SetStatus(exported.Error ?? "The all-data workbook could not be written.", true);
            return;
        }

        try
        {
            File.WriteAllBytes(dialog.FileName, exported.Value);
            SetStatus("All-data workbook saved. Edit it, then choose it on this tab for the before → after preview.", false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SetStatus("The workbook could not be written. Close it if it is open in Excel. (" + exception.Message + ")", true);
        }
    }

    private void ChooseBulkFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose the edited all-data workbook",
            Filter = "Excel workbook (*.xlsx)|*.xlsx",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var bytes = ReadSharedBytes(dialog.FileName);
            var result = _services.Workbooks.PreviewBulk(_user, bytes, IncludeSettings);
            if (!result.Ok || result.Value is null)
            {
                _bulkPreview = null;
                BulkRows.Clear();
                SetStatus(result.Error ?? "The workbook could not be previewed.", true);
                return;
            }

            _bulkFileName = Path.GetFileName(dialog.FileName);
            _bulkPreview = result.Value;
            BulkRows.Clear();
            foreach (var row in _bulkPreview.Rows)
            {
                BulkRows.Add(row);
            }

            SetStatus(_bulkPreview.ErrorCount > 0
                ? "Fix every red line and preview again. Nothing can be applied while an error remains."
                : _bulkPreview.CanApply
                    ? "Read every before → after line, then press Apply these changes."
                    : "The workbook has no changes to apply.", _bulkPreview.ErrorCount > 0);
        }
        catch (Exception exception)
        {
            _bulkPreview = null;
            BulkRows.Clear();
            SetStatus("The workbook could not be read. " + exception.Message, true);
        }
        finally
        {
            RaiseBulkState();
        }
    }

    private void ApplyBulk()
    {
        if (_bulkPreview is null || !_bulkPreview.CanApply)
        {
            SetStatus("Preview first, and fix every red line. There is nothing to apply.", true);
            return;
        }

        var result = _services.Workbooks.ApplyBulk(_user, _bulkPreview);
        if (!result.Ok || result.Value is null)
        {
            SetStatus(result.Error ?? "The workbook was not applied.", true);
            return;
        }

        ClearBulk();
        SetStatus(result.Value.SummaryText, false);
    }

    private void ClearBulk()
    {
        _bulkPreview = null;
        _bulkFileName = string.Empty;
        BulkRows.Clear();
        RaiseBulkState();
    }

    private void RaiseBulkState()
    {
        OnPropertyChanged(nameof(BulkFileNameText));
        OnPropertyChanged(nameof(BulkSummary));
        OnPropertyChanged(nameof(HasBulkPreview));
        OnPropertyChanged(nameof(HasNoBulkPreview));
        OnPropertyChanged(nameof(CanApplyBulk));
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

    private static byte[] ReadSharedBytes(string path)
    {
        // Excel may still have the workbook open. Read a shared snapshot, but never an unbounded file.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (stream.Length > 20 * 1024 * 1024)
        {
            throw new InvalidDataException("The file is larger than the 20 MB import limit.");
        }

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
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
