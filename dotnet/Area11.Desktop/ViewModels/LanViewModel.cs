using System.Collections.ObjectModel;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// The second counter (F-45, Spec §34.9) - one page, two buttons and a straight answer.
///
/// The page never moves a file and never switches the running program to another database: it writes
/// the one line `shop.ini` and says **restart**. That is why "prepare" and "join" are two-step
/// buttons - the next click is the one that changes the shop (D-62).
/// </summary>
public sealed class LanViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;

    private string _modeLabel = "One computer";
    private string _folderLabel = string.Empty;
    private string _journalText = "?";
    private string _reachText = "checking...";
    private string _checkMessage = string.Empty;
    private string _thisPc = string.Empty;
    private string _iniFile = string.Empty;
    private string _restartLabel = string.Empty;
    private bool _restartPending;
    private bool _reachable;
    private bool _canManage;
    private string _sharePathText = @"\\MAIN-PC\Area11";
    private bool _confirmPrepare;
    private bool _confirmJoin;
    private string _statusText = "This page decides where the shop's database lives. Read the warning below before changing anything.";
    private bool _isStatusError;

    public LanViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        RefreshCommand = new RelayCommand(Load);
        PrepareCommand = new RelayCommand(Prepare);
        JoinCommand = new RelayCommand(Join);
        TestCommand = new RelayCommand(Test);

        Load();
    }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand PrepareCommand { get; }

    public RelayCommand JoinCommand { get; }

    public RelayCommand TestCommand { get; }

    public ObservableCollection<LanConnectionRow> Connections { get; } = new();

    public string ModeLabel
    {
        get => _modeLabel;
        private set => SetProperty(ref _modeLabel, value);
    }

    public string FolderLabel
    {
        get => _folderLabel;
        private set => SetProperty(ref _folderLabel, value);
    }

    public string JournalText
    {
        get => _journalText;
        private set => SetProperty(ref _journalText, value);
    }

    public string ReachText
    {
        get => _reachText;
        private set => SetProperty(ref _reachText, value);
    }

    public string CheckMessage
    {
        get => _checkMessage;
        private set => SetProperty(ref _checkMessage, value);
    }

    public string ThisPc
    {
        get => _thisPc;
        private set => SetProperty(ref _thisPc, value);
    }

    public string IniFile
    {
        get => _iniFile;
        private set => SetProperty(ref _iniFile, value);
    }

    public string RestartLabel
    {
        get => _restartLabel;
        private set => SetProperty(ref _restartLabel, value);
    }

    public bool RestartPending
    {
        get => _restartPending;
        private set => SetProperty(ref _restartPending, value);
    }

    public bool Reachable
    {
        get => _reachable;
        private set => SetProperty(ref _reachable, value);
    }

    public bool CanManage
    {
        get => _canManage;
        private set => SetProperty(ref _canManage, value);
    }

    public string SharePathText
    {
        get => _sharePathText;
        set => SetProperty(ref _sharePathText, value);
    }

    /// <summary>True while the first press waits for the second one.</summary>
    public bool ConfirmPrepare
    {
        get => _confirmPrepare;
        private set
        {
            if (SetProperty(ref _confirmPrepare, value))
            {
                OnPropertyChanged(nameof(PrepareLabel));
            }
        }
    }

    public bool ConfirmJoin
    {
        get => _confirmJoin;
        private set
        {
            if (SetProperty(ref _confirmJoin, value))
            {
                OnPropertyChanged(nameof(JoinLabel));
            }
        }
    }

    public string PrepareLabel => ConfirmPrepare
        ? "Press again: turn on sharing on this PC"
        : "Prepare this PC as the main counter";

    public string JoinLabel => ConfirmJoin
        ? "Press again: use that folder from now on"
        : "Join the main counter";

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
        var status = _services.Lan.Status(_user);

        Connections.Clear();

        if (!status.Ok || status.Value is null)
        {
            CanManage = false;
            StatusText = status.Error ?? "The connection could not be checked.";
            IsStatusError = true;
            return;
        }

        var value = status.Value;

        CanManage = _user.IsOwner;
        ModeLabel = value.ModeLabel;
        FolderLabel = value.FolderLabel;
        JournalText = value.JournalMode;
        ReachText = value.ReachLabel;
        Reachable = value.Reachable;
        CheckMessage = value.CheckMessage;
        ThisPc = value.ThisPc;
        IniFile = value.IniFile;
        RestartPending = value.RestartPending;
        RestartLabel = value.RestartLabel;

        foreach (var row in value.Connections)
        {
            Connections.Add(row);
        }
    }

    private void Prepare()
    {
        if (!ConfirmPrepare)
        {
            ConfirmPrepare = true;
            SetStatus(
                "This PC keeps the database and shares its folder. Press the same button again to do it. " +
                "Bills on this PC keep working; nothing is moved.", false);
            return;
        }

        ConfirmPrepare = false;

        var result = _services.Lan.PrepareAsMain(_user);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The main counter could not be prepared.", true);
            return;
        }

        Load();
        SetStatus(
            "Done. Restart Area11 on this PC, then double-click SHARE-AREA11.cmd - it is in the Area11 " +
            "program folder (C:\\Program Files\\Area11) and in tools\\ in the repository. It shares the " +
            "folder shown above and prints the address for the second PC (\\\\" + Environment.MachineName + "\\Area11). " +
            "On the second PC open this page, write that address and press Join.", false);
    }

    private void Join()
    {
        var path = SharePathText;

        if (!ConfirmJoin)
        {
            var check = ShopConfig.Validate(ShopMode.Second, path);

            if (check is not null)
            {
                SetStatus(check, true);
                return;
            }

            ConfirmJoin = true;
            SetStatus("This PC will stop using its own database and open the main counter's file after a restart. " +
                      "Press the same button again to do it.", false);
            return;
        }

        ConfirmJoin = false;

        var result = _services.Lan.JoinAsSecond(_user, path);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The main counter could not be joined.", true);
            return;
        }

        Load();
        SetStatus("Done. Restart Area11 on this PC; it will then open the main counter's database. " +
                  "This PC's own old database stays where it was and is not touched.", false);
    }

    private void Test()
    {
        ConfirmPrepare = false;
        ConfirmJoin = false;

        var result = _services.Lan.TestConnection(_user);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The database could not be locked.", true);
            return;
        }

        Load();
        SetStatus("The database answered: readable and writable, no other counter is holding it.", false);
    }

    private void SetStatus(string text, bool isError)
    {
        StatusText = text;
        IsStatusError = isError;
    }
}
