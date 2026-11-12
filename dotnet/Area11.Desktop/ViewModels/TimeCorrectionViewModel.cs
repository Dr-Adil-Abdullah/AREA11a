using System.Collections.ObjectModel;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// Fixes bills that were stamped with the wrong clock (v0.68.0, owner's word Nov 2026): the
/// wrongly-stamped bills are listed TOGETHER, each with the time it would get, and ONE press moves
/// them all.
///
/// No rule lives here. <see cref="Area11.Core.Services.TimeCorrectionService"/> decides the shifts,
/// moves the money rows with their bills, and writes both times into the black box; this class
/// carries the list and the buttons.
///
/// Why the fix refuses while offline: with no time-server answer in this run there is no true time
/// to move TO - pressing ahead would drag true-stamped bills back onto the computer clock.
/// </summary>
public sealed class TimeCorrectionViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;

    private string _fromText = string.Empty;
    private string _toText = string.Empty;
    private string _driftText = string.Empty;
    private string _countText = string.Empty;
    private string _statusText = "Pick a range and press List bills.";
    private bool _isStatusError;
    private bool _canFix;

    public TimeCorrectionViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        // The range the owner almost always wants: the last month, ending today.
        _fromText = Clock.DateDaysAgo(30);
        _toText = Clock.Today();

        ListCommand = new RelayCommand(List);
        FixAllCommand = new RelayCommand(FixAll);

        List();
    }

    /// <summary>The window title carries the shop's app name (v0.69.0, de-workshop).</summary>
    public string WindowTitle => AppDisplayName + " — Fix the time on bills";

    private string AppDisplayName
    {
        get
        {
            var name = _services.Settings.GetString("brand.appName").Trim();
            return name.Length == 0 ? "Area11" : name;
        }
    }

    public ObservableCollection<TimeCorrectionBill> Bills { get; } = new();

    public RelayCommand ListCommand { get; }

    public RelayCommand FixAllCommand { get; }

    public string FromText
    {
        get => _fromText;
        set => SetProperty(ref _fromText, value);
    }

    public string ToText
    {
        get => _toText;
        set => SetProperty(ref _toText, value);
    }

    /// <summary>Where the true time comes from, in the owner's words - the same line as Settings.</summary>
    public string DriftText
    {
        get => _driftText;
        private set => SetProperty(ref _driftText, value);
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

    /// <summary>True when a server has answered and the list holds at least one bill to move.</summary>
    public bool CanFix
    {
        get => _canFix;
        private set => SetProperty(ref _canFix, value);
    }

    public void List()
    {
        // The reference time must be real: without a server answer in this run, the "fix" would be
        // a guess, so the list stays empty and says why instead of proposing false corrections.
        if (!TimeSource.IsOnline)
        {
            Bills.Clear();
            CountText = string.Empty;
            CanFix = false;
            DriftText = TimeSource.DescribeForOwner();
            SetStatus("No time server has answered in this run, so there is no true time to fix by. " +
                      "Connect the internet, wait for the next ask (every 20 minutes), and open this again.",
                true);
            return;
        }

        DriftText = TimeSource.DescribeForOwner() +
                    " Bills below were stamped with the computer clock - one press moves them all to true time.";

        var listed = _services.TimeCorrection.ListBills(_user, FromText, ToText);

        if (!listed.Ok || listed.Value is null)
        {
            Bills.Clear();
            CountText = string.Empty;
            CanFix = false;
            SetStatus(listed.Error ?? "The bills could not be listed.", true);
            return;
        }

        Bills.Clear();

        foreach (var bill in listed.Value)
        {
            Bills.Add(bill);
        }

        var count = listed.Value.Count;
        CountText = count == 0
            ? "No bill in this range needs a fix."
            : count + (count == 1 ? " bill carries" : " bills carry") + " the wrong time.";
        CanFix = count > 0;

        SetStatus(
            count == 0
                ? "Every bill in this range is already on true time."
                : "Check the times above, then press 'Fix every bill above'. Nothing moves until you do.",
            false);
    }

    private void FixAll()
    {
        if (!TimeSource.IsOnline)
        {
            CanFix = false;
            SetStatus("No time server has answered in this run, so there is no true time to fix by. " +
                      "Nothing was changed.",
                true);
            return;
        }

        var ids = Bills.Select(bill => bill.Id).ToList();

        if (ids.Count == 0)
        {
            return;
        }

        var done = _services.TimeCorrection.ApplyCorrection(_user, ids);

        if (!done.Ok || done.Value is null)
        {
            SetStatus(done.Error ?? "The bill times could not be fixed.", true);
            return;
        }

        var result = done.Value;

        // The fixed bills disappear from the list, so a second press can never move them twice.
        List();

        SetStatus("Fixed " + result.Fixed + (result.Fixed == 1 ? " bill." : " bills.") +
                  (result.Skipped == 0
                      ? string.Empty
                      : " " + result.Skipped + " already right, skipped."),
            false);
    }

    private void SetStatus(string text, bool isError)
    {
        StatusText = text;
        IsStatusError = isError;
    }
}
