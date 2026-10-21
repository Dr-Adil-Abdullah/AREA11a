using System.Collections.ObjectModel;
using Area11.Core.Models;
using Area11.Core.Services;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// The alert board (F-24). Two lists:
///   • live alerts - what is wrong right now (minus stock, goods in quarantine);
///   • ended alerts - every reason the owner ever wrote, with a button to bring one back.
///
/// Only the owner sees the reason box and the buttons: everybody else is told, in plain words, that
/// the owner has to end it (U-31). The rule itself is enforced in <see cref="AlertService"/>, so a
/// hidden button is never the security.
/// </summary>
public sealed class AlertsViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;
    private readonly Action? _onChanged;

    private string _statusText = "Every alert stays until the owner ends it with a written reason.";
    private bool _isStatusError;
    private string _reasonText = string.Empty;
    private AlertRow? _selected;

    /// <param name="onChanged">
    /// Called after an alert is ended or brought back, so the red bar in the window frame updates
    /// immediately instead of waiting for the next page change.
    /// </param>
    public AlertsViewModel(AppServices services, User user, Action? onChanged = null)
    {
        _services = services;
        _user = user;
        _onChanged = onChanged;

        RefreshCommand = new RelayCommand(Load);
        DismissCommand = new RelayCommand(Dismiss);
        RestoreCommand = new RelayCommand(Restore);

        Load();
    }

    public ObservableCollection<AlertRow> Live { get; } = new();

    public ObservableCollection<AlertRow> Ended { get; } = new();

    public RelayCommand RefreshCommand { get; }

    public RelayCommand DismissCommand { get; }

    public RelayCommand RestoreCommand { get; }

    public bool IsOwner => _user.IsOwner;

    public bool IsNotOwner => !_user.IsOwner;

    public string SummaryText { get; private set; } = "Nothing needs attention.";

    public string ReasonText
    {
        get => _reasonText;
        set => SetProperty(ref _reasonText, value);
    }

    public AlertRow? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                OnPropertyChanged(nameof(SelectedCaption));
            }
        }
    }

    public string SelectedCaption => _selected is null
        ? "Pick the alert above, write the reason, then press End this alert."
        : $"{_selected.TypeLabel}: {_selected.Title}";

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

    public void Load()
    {
        var report = _services.Alert.Report();

        Live.Clear();
        foreach (var alert in report.Live)
        {
            Live.Add(new AlertRow(alert));
        }

        Ended.Clear();
        foreach (var alert in _services.Alert.Ended())
        {
            Ended.Add(new AlertRow(alert));
        }

        Selected = Live.FirstOrDefault();

        SummaryText = report.LiveCount == 0
            ? "Nothing needs attention. Ended alerts stay on record below."
            : $"{report.NegativeCount} medicine(s) with minus stock · {report.QuarantineCount} item(s) in quarantine.";

        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(SelectedCaption));
    }

    private void Dismiss()
    {
        if (!IsOwner)
        {
            SetStatus("Only the owner can end an alert by writing a reason.", true);
            return;
        }

        if (Selected is null)
        {
            SetStatus("Pick the alert you want to end.", true);
            return;
        }

        var result = _services.Alert.Dismiss(_user, Selected.Key, ReasonText);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The alert could not be ended.", true);
            return;
        }

        var caption = $"{Selected.TypeLabel}: {Selected.Title}";
        ReasonText = string.Empty;
        Load();
        _onChanged?.Invoke();
        SetStatus($"{caption} - ended. Your reason is on record.", false);
    }

    private void Restore()
    {
        if (Selected is null || !Selected.IsEnded)
        {
            SetStatus("Pick an ended alert from the lower list first.", true);
            return;
        }

        var result = _services.Alert.Restore(_user, Selected.Key);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The alert could not be brought back.", true);
            return;
        }

        var caption = $"{Selected.TypeLabel}: {Selected.Title}";
        Load();
        _onChanged?.Invoke();
        SetStatus($"{caption} is back on the board.", false);
    }

    private void SetStatus(string message, bool isError)
    {
        StatusText = message;
        IsStatusError = isError;
    }
}

/// <summary>One alert line. Read-only: every action is a command on the page, not on the row.</summary>
public sealed class AlertRow
{
    public AlertRow(AlertItem alert)
    {
        Key = alert.Key;
        Type = alert.Type;
        TypeLabel = alert.TypeLabel;
        Title = alert.Title;
        Detail = alert.Detail;
        IsEnded = alert.Ended;
        Reason = alert.Reason ?? string.Empty;
        EndedBy = alert.EndedBy ?? string.Empty;
        EndedAt = alert.EndedAt ?? string.Empty;
        IsCritical = alert.IsCritical;
        IsAmber = alert.IsAmber;
    }

    public string Key { get; }

    public string Type { get; }

    public string TypeLabel { get; }

    public string Title { get; }

    public string Detail { get; }

    public bool IsEnded { get; }

    public string Reason { get; }

    public string EndedBy { get; }

    public string EndedAt { get; }

    public bool IsCritical { get; }

    public bool IsAmber { get; }
}
