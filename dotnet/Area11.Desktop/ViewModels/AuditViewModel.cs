using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// The black box (F-35, Spec 22). Read-only by design: the page can search and export, and there is
/// no command here that could change or remove a line - a mistake in the shop is corrected by a
/// second line, not by editing history.
///
/// All the rules live in `AuditHistoryService`: it refuses anyone without the `audit` permission
/// (owner only by default) and it builds the CSV. This class only turns the answer into text.
/// </summary>
public sealed class AuditViewModel : ObservableObject, IEmptyPage
{
    /// <summary>
    /// Why this page has nothing on it, in the shop's words (v0.33.0, D-89). **Null when it
    /// has rows**, so nothing in this page has to keep a second flag in step with its list.
    /// </summary>
    public EmptyHint? EmptyHint => EmptyPage.When(
        Rows.Count == 0,
        "Black box",
        searched: SearchText.Trim().Length > 0);
    private const string AllTime = "All time";
    private const string Today = "Today";
    private const string Last7 = "Last 7 days";
    private const string Last30 = "Last 30 days";

    private readonly AppServices _services;
    private readonly User _user;

    private string _searchText = string.Empty;
    private string _selectedModule = string.Empty;
    private string _selectedAction = string.Empty;
    private string _range = Last7;
    private int _pageSize = 500;

    private string _countText = "0 line(s)";
    private string _statusText = string.Empty;
    private bool _isStatusError;
    private bool _canExport;
    private AuditPage? _page;

    public AuditViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        RefreshCommand = new RelayCommand(Load);
        TodayCommand = new RelayCommand(() => SelectRange(Today));
        WeekCommand = new RelayCommand(() => SelectRange(Last7));
        MonthCommand = new RelayCommand(() => SelectRange(Last30));
        AllCommand = new RelayCommand(() => SelectRange(AllTime));
        ClearCommand = new RelayCommand(Clear);
        ExportCommand = new RelayCommand(Export);

        Load();
    }

    public ObservableCollection<AuditRow> Rows { get; } = new();

    public ObservableCollection<string> Modules { get; } = new();

    public ObservableCollection<string> Actions { get; } = new();

    public ObservableCollection<int> PageSizes { get; } = new(AuditHistoryService.PageSizes);

    public RelayCommand RefreshCommand { get; }

    public RelayCommand TodayCommand { get; }

    public RelayCommand WeekCommand { get; }

    public RelayCommand MonthCommand { get; }

    public RelayCommand AllCommand { get; }

    public RelayCommand ClearCommand { get; }

    public RelayCommand ExportCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    /// <summary>Empty means "every module"; the item is called `(every module)` on the screen.</summary>
    public string SelectedModule
    {
        get => _selectedModule;
        set => SetProperty(ref _selectedModule, value ?? string.Empty);
    }

    public string SelectedAction
    {
        get => _selectedAction;
        set => SetProperty(ref _selectedAction, value ?? string.Empty);
    }

    public string Range
    {
        get => _range;
        private set => SetProperty(ref _range, value);
    }

    public int PageSize
    {
        get => _pageSize;
        set
        {
            if (SetProperty(ref _pageSize, value <= 0 ? 500 : value))
            {
                Load();
            }
        }
    }

    public string CountText
    {
        get => _countText;
        private set => SetProperty(ref _countText, value);
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

    public bool CanExport
    {
        get => _canExport;
        private set => SetProperty(ref _canExport, value);
    }

    private void SelectRange(string range)
    {
        Range = range;
        Load();
    }

    private void Clear()
    {
        SearchText = string.Empty;
        SelectedModule = string.Empty;
        SelectedAction = string.Empty;
        Range = Last7;
        Load();
    }

    private void Load()
    {
        var filter = new AuditFilter
        {
            Search = SearchText,
            Module = SelectedModule,
            Action = SelectedAction,
            Limit = PageSize
        };

        var (from, to) = Dates();

        filter.FromDate = from;
        filter.ToDate = to;

        var answer = _services.AuditHistory.Read(filter, _user);

        Rows.Clear();
        Modules.Clear();
        Actions.Clear();

        if (!answer.Ok)
        {
            _page = null;
            CanExport = false;
            CountText = "0 line(s)";
            IsStatusError = true;
            StatusText = answer.Error ?? "The black box could not be read.";
            return;
        }

        var page = answer.Value!;
        _page = page;

        foreach (var row in page.Rows)
        {
            Rows.Add(row);
        }

        foreach (var module in page.Modules)
        {
            Modules.Add(module);
        }

        foreach (var action in page.Actions)
        {
            Actions.Add(action);
        }

        CountText = page.CountLabel;
        CanExport = page.Rows.Count > 0;
        IsStatusError = false;

        StatusText = page.IsEmpty
            ? "Nothing matches this search. Widen the dates or clear the boxes."
            : "Newest line first. No line can be edited or deleted - by anyone, ever (Spec 22.1).";
    }

    private (string From, string To) Dates()
    {
        var today = DateTime.ParseExact(Clock.Today(), "yyyy-MM-dd", CultureInfo.InvariantCulture);

        return Range switch
        {
            Today => (today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                      today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            Last7 => (today.AddDays(-6).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                      today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            Last30 => (today.AddDays(-29).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                       today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            _ => (string.Empty, string.Empty)
        };
    }

    /// <summary>
    /// Writes exactly what is on the screen - the same filter, the same rows, the same order. An export
    /// that quietly contains more or other lines than the owner is looking at is worse than none.
    /// </summary>
    private void Export()
    {
        if (_page is null || _page.Rows.Count == 0)
        {
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save the black box",
            FileName = "Area11-audit-" + Clock.Today() + ".csv",
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
            // UTF-8 WITH a BOM: without it Excel reads Urdu names as gibberish, and an export the
            // owner cannot read is not an export.
            File.WriteAllText(dialog.FileName, _page.ToCsv(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            IsStatusError = false;
            StatusText = _page.Rows.Count.ToString(CultureInfo.InvariantCulture)
                         + " line(s) saved to " + dialog.FileName;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            IsStatusError = true;
            StatusText = "The file could not be written. Close it if it is open in Excel, then try again. ("
                         + exception.Message + ")";
        }
    }
}
