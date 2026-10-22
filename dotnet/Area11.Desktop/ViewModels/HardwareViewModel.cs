using System.Collections.ObjectModel;
using System.Globalization;
using Area11.Core.Models;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// The hardware test page (F-46, Spec 33.3.4): the thermal printer at 58 or 80 mm, the USB barcode
/// scanner, and the owner's own verdict written into the record.
///
/// No rule lives here. <see cref="HardwareService"/> builds the slip, the six statements and the
/// saved answer; the scanner lookup is <see cref="BarcodeService.Find"/>, the same code the counter
/// uses - so the test can never pass a scanner the counter would refuse. This class carries the rows,
/// the buttons and the one-line message at the bottom.
/// </summary>
public sealed class HardwareViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;

    private string _printerText = "Windows default printer";
    private string _paperText = "58 mm";
    private string _copiesText = "1 copy";
    private string _progressText = "0 of 6 passed";
    private string _lastRunText = "never checked on this PC";
    private string _recordWarning = string.Empty;
    private bool _hasRecordWarning;
    private string _scanText = string.Empty;
    private string _lastScanText = string.Empty;
    private string _scanHint = "Scan a box, or type a code and press Enter.";
    private bool _isScanFound;
    private bool _hasLastScan;
    private string _noteText = string.Empty;
    private string _statusText = "Print the slip, look at the paper, then tick what you saw.";
    private bool _isStatusError;

    public HardwareViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        PrintCommand = new RelayCommand(Print);
        ScanCommand = new RelayCommand(Scan);
        SaveCommand = new RelayCommand(Save);
        RefreshCommand = new RelayCommand(Load);

        Load();
    }

    public ObservableCollection<HardwareCheckRow> Rows { get; } = new();

    public ObservableCollection<HardwareScanRow> Scans { get; } = new();

    public RelayCommand PrintCommand { get; }

    public RelayCommand ScanCommand { get; }

    public RelayCommand SaveCommand { get; }

    public RelayCommand RefreshCommand { get; }

    // ------------------------------ the printer ------------------------------

    public string PrinterText
    {
        get => _printerText;
        private set => SetProperty(ref _printerText, value);
    }

    public string PaperText
    {
        get => _paperText;
        private set => SetProperty(ref _paperText, value);
    }

    public string CopiesText
    {
        get => _copiesText;
        private set => SetProperty(ref _copiesText, value);
    }

    // ------------------------------ the ticks ------------------------------

    public string ProgressText
    {
        get => _progressText;
        private set => SetProperty(ref _progressText, value);
    }

    public string LastRunText
    {
        get => _lastRunText;
        private set => SetProperty(ref _lastRunText, value);
    }

    /// <summary>Set when the saved ticks belong to a different paper width than the one in Settings.</summary>
    public string RecordWarning
    {
        get => _recordWarning;
        private set => SetProperty(ref _recordWarning, value);
    }

    public bool HasRecordWarning
    {
        get => _hasRecordWarning;
        private set => SetProperty(ref _hasRecordWarning, value);
    }

    // ------------------------------ the scanner ------------------------------

    public string ScanText
    {
        get => _scanText;
        set => SetProperty(ref _scanText, value);
    }

    public string LastScanText
    {
        get => _lastScanText;
        private set => SetProperty(ref _lastScanText, value);
    }

    public string ScanHint
    {
        get => _scanHint;
        private set => SetProperty(ref _scanHint, value);
    }

    /// <summary>Green when the code belongs to a medicine, plain when it does not - both are answers.</summary>
    public bool IsScanFound
    {
        get => _isScanFound;
        private set => SetProperty(ref _isScanFound, value);
    }

    public bool HasLastScan
    {
        get => _hasLastScan;
        private set => SetProperty(ref _hasLastScan, value);
    }

    // ------------------------------ the note and the status ------------------------------

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

    // ============================== the commands ==============================

    public void Load()
    {
        Rows.Clear();

        var loaded = _services.Hardware.Load(_user);

        if (!loaded.Ok || loaded.Value is null)
        {
            SetStatus(loaded.Error ?? "The hardware test could not be opened.", true);
            return;
        }

        var report = loaded.Value;

        PrinterText = report.PrinterLabel;
        PaperText = report.WidthLabel;
        CopiesText = report.Copies.ToString(CultureInfo.InvariantCulture)
                     + (report.AutoCut ? " copy, cut on" : " copy, no cut");

        foreach (var check in report.Checks)
        {
            Rows.Add(new HardwareCheckRow(check.Key, check.Text, check.Hint, check.Passed, Recount));
        }

        Recount();

        LastRunText = report.LastRunLabel;
        RecordWarning = report.RecordMatchesWidth
            ? string.Empty
            : "The saved ticks belong to " + report.CheckedWidthMm + " mm paper, but this shop now prints "
              + report.WidthMm + " mm. Print the slip again and tick it again.";
        HasRecordWarning = RecordWarning.Length > 0;

        if (string.IsNullOrWhiteSpace(NoteText) && !string.IsNullOrWhiteSpace(report.Note))
        {
            NoteText = report.Note!;
        }
    }

    private void Recount()
    {
        ProgressText = Rows.Count(row => row.Passed).ToString(CultureInfo.InvariantCulture)
                       + " of " + Rows.Count + " passed";
    }

    private void Print()
    {
        var slip = _services.Hardware.BuildSlip(_user);

        if (!slip.Ok || slip.Value is null)
        {
            SetStatus(slip.Error ?? "The slip could not be built.", true);
            return;
        }

        var printed = _services.Printer.Print(slip.Value);

        SetStatus(
            printed.Ok
                ? "The slip went to " + PrinterText + ". Hold the paper and count the digits - the line must end at "
                  + slip.Value.Width + "."
                : printed.Error ?? "The printer did not answer.",
            !printed.Ok);
    }

    private void Scan()
    {
        var code = (ScanText ?? string.Empty).Trim();

        if (code.Length == 0)
        {
            SetStatus("Scan a barcode or type its digits, then press Enter.", true);
            return;
        }

        var found = _services.Barcodes.Find(_user, code);
        ScanText = string.Empty;
        HasLastScan = true;

        if (found.Ok && found.Value is not null)
        {
            IsScanFound = true;
            LastScanText = found.Value.Caption;
            ScanHint = "The scanner typed " + found.Value.Barcode + " and Area11 found this medicine.";
            Scans.Insert(0, new HardwareScanRow(found.Value.Barcode + " - " + found.Value.Caption, true));
            SetStatus("Found: " + found.Value.Caption + ". That is the scanner working.", false);
            return;
        }

        // A code the shop does not have is still proof that the scanner typed: the message never says
        // the scanner is broken, because it usually is not.
        IsScanFound = false;
        LastScanText = code;
        ScanHint = found.Error ?? "That code is not on this shop's list.";
        Scans.Insert(0, new HardwareScanRow(code + " - not on the shop's list", false));
        SetStatus("The scanner typed " + code + ". " + ScanHint, false);
    }

    private void Save()
    {
        var passed = Rows.Where(row => row.Passed).Select(row => row.Key).ToList();
        var saved = _services.Hardware.Save(_user, passed, NoteText);

        if (!saved.Ok || saved.Value is null)
        {
            SetStatus(saved.Error ?? "The result could not be saved.", true);
            return;
        }

        var report = saved.Value;
        Load();

        SetStatus(
            "Saved: " + report.ProgressLabel + " on " + report.WidthLabel + ". " +
            (report.IsComplete
                ? "This PC is checked - the owner's record is in the black box."
                : "Still missing: " + report.MissingLabel),
            !report.IsComplete);
    }

    private void SetStatus(string text, bool isError)
    {
        StatusText = text;
        IsStatusError = isError;
    }
}
