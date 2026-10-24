using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// Backup and restore (F-36, Spec 23) - the page the owner opens before an update and after a bad day.
///
/// Three things it refuses to hide:
///   • **where** the files are (a full path he can copy to a USB stick);
///   • **what** a backup contains (photos or only the database - an old database-only copy lost them);
///   • **that a restore replaces everything** - so it is two clicks, and the second one is red.
///
/// The work itself is in <see cref="BackupService"/>: this class moves text and buttons only.
/// </summary>
public sealed class BackupViewModel : ObservableObject, IEmptyPage
{
    /// <summary>
    /// Why this page has nothing on it, in the shop's words (v0.33.0, D-89). **Null when it
    /// has rows**, so nothing in this page has to keep a second flag in step with its list.
    /// </summary>
    public EmptyHint? EmptyHint => EmptyPage.When(Files.Count == 0, "Backup & restore");

    private readonly AppServices _services;
    private readonly User _user;
    private readonly Action? _onRestored;

    private BackupFile? _selected;
    private bool _includePhotos = true;
    private bool _confirming;
    private bool _autoEnabled;
    private string _everyHoursText = "24";
    private string _keepText = "7";
    private string _statusText = "Press \"Back up now\" and copy the zip to a USB stick or your e-mail.";
    private bool _isStatusError;
    private string _lastAutomatic = "No automatic backup yet.";

    public BackupViewModel(AppServices services, User user, Action? onRestored = null)
    {
        _services = services;
        _user = user;
        _onRestored = onRestored;

        CreateCommand = new RelayCommand(CreateBackup);
        RefreshCommand = new RelayCommand(Load);
        AskRestoreCommand = new RelayCommand(AskRestore);
        CancelRestoreCommand = new RelayCommand(CancelRestore);
        RestoreCommand = new RelayCommand(Restore);
        SaveScheduleCommand = new RelayCommand(SaveSchedule);
        OpenFolderCommand = new RelayCommand(OpenFolder);

        Load();
    }

    public ObservableCollection<BackupFile> Files { get; } = new();

    public RelayCommand CreateCommand { get; }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand AskRestoreCommand { get; }

    public RelayCommand CancelRestoreCommand { get; }

    /// <summary>The red button: it really replaces the shop's database.</summary>
    public RelayCommand RestoreCommand { get; }

    public RelayCommand SaveScheduleCommand { get; }

    public RelayCommand OpenFolderCommand { get; }

    /// <summary>Owner only by default; the owner may grant it to one trusted person in Settings.</summary>
    public bool CanUse => _services.Permissions.Can(_user, Modules.Backup);

    public bool CannotUse => !CanUse;

    public string FolderText => _services.Backup.BackupsFolder;

    public string LastAutomatic
    {
        get => _lastAutomatic;
        private set => SetProperty(ref _lastAutomatic, value);
    }

    /// <summary>
    /// "No backup yet" is a state, not an empty list: the page says what to press, and the words have
    /// to disappear by themselves once a backup exists.
    /// </summary>
    public bool HasNoFiles => Files.Count == 0;

    public BackupFile? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                // A different file means a different question: never leave "restore this one?" on
                // the screen pointing at a row the owner has already moved away from.
                ConfirmRestore = false;
            }
        }
    }

    /// <summary>Ticked = the zip carries the photos as well. Unticked = smaller, but photos are lost.</summary>
    public bool IncludePhotos
    {
        get => _includePhotos;
        set => SetProperty(ref _includePhotos, value);
    }

    /// <summary>True while the red "this replaces everything" panel is showing.</summary>
    public bool ConfirmRestore
    {
        get => _confirming;
        private set => SetProperty(ref _confirming, value);
    }

    /// <summary>What is about to be replaced, spelled out in the panel.</summary>
    public string ConfirmText => Selected is null
        ? string.Empty
        : $"Everything in the shop will be replaced by {Selected.FileName} ({Selected.Stamp}). " +
          "A copy of today's data is kept first. This cannot be undone from inside the program.";

    public bool AutoEnabled
    {
        get => _autoEnabled;
        set => SetProperty(ref _autoEnabled, value);
    }

    public string EveryHoursText
    {
        get => _everyHoursText;
        set => SetProperty(ref _everyHoursText, value);
    }

    public string KeepText
    {
        get => _keepText;
        set => SetProperty(ref _keepText, value);
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

    // ------------------------------ Loading ------------------------------

    public void Load()
    {
        var backup = _services.Backup;

        // The first sign-in of the day is the natural moment for the daily copy: the shop is open,
        // nothing is being billed yet, and the owner is standing at the computer anyway.
        backup.RunAutomaticIfDue();

        AutoEnabled = backup.AutomaticEnabled;
        EveryHoursText = backup.EveryHours.ToString(CultureInfo.InvariantCulture);
        KeepText = backup.KeepCount.ToString(CultureInfo.InvariantCulture);
        LastAutomatic = backup.LastBackupText;

        Files.Clear();

        foreach (var file in backup.List())
        {
            Files.Add(file);
        }

        OnPropertyChanged(nameof(HasNoFiles));

        // Keep the highlighted row if it is still there; otherwise start on the newest.
        var keep = Selected?.FileName;
        Selected = Files.FirstOrDefault(file => file.FileName == keep) ?? Files.FirstOrDefault();
    }

    // ------------------------------ Actions ------------------------------

    private void CreateBackup()
    {
        var result = _services.Backup.Create(_user, IncludePhotos);

        if (!result.Ok || result.Value is null)
        {
            SetStatus(result.Error ?? "The backup could not be made.", true);
            return;
        }

        Load();
        Selected = Files.FirstOrDefault(file => file.FileName == result.Value.FileName);

        SetStatus(
            $"Backup made: {result.Value.FileName} ({result.Value.SizeLabel}). " +
            "Copy it to a USB stick, or keep it on another computer - a backup on the same disk is not a backup.",
            false);
    }

    private void AskRestore()
    {
        if (Selected is null)
        {
            SetStatus("Choose the backup you want to restore first.", true);
            return;
        }

        ConfirmRestore = true;
        OnPropertyChanged(nameof(ConfirmText));
        SetStatus("Read the red panel, then press \"Restore now\" - or Cancel.", false);
    }

    private void CancelRestore()
    {
        ConfirmRestore = false;
        SetStatus("Restore cancelled. Nothing was changed.", false);
    }

    private void Restore()
    {
        if (Selected is null)
        {
            SetStatus("Choose the backup you want to restore first.", true);
            return;
        }

        var chosen = Selected;
        ConfirmRestore = false;

        var result = _services.Backup.Restore(chosen.FullPath, _user);

        if (!result.Ok || result.Value is null)
        {
            SetStatus(result.Error ?? "The backup could not be restored.", true);
            return;
        }

        // The database on disk is now the old one: every page on the screen holds numbers read from
        // a file that no longer exists. The window is rebuilt from scratch a moment from now.
        SetStatus(result.Value.SummaryText, false);
        _onRestored?.Invoke();
    }

    private void SaveSchedule()
    {
        if (!int.TryParse(EveryHoursText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours))
        {
            SetStatus("The interval must be a whole number of hours, for example 24.", true);
            return;
        }

        if (!int.TryParse(KeepText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var keep))
        {
            SetStatus("How many to keep must be a whole number, for example 7.", true);
            return;
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["backup.autoEnabled"] = AutoEnabled,
            ["backup.everyHours"] = hours,
            ["backup.keep"] = keep
        };

        var result = _services.Settings.SetMany(values, _user);

        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The schedule could not be saved.", true);
            return;
        }

        Load();
        SetStatus(
            AutoEnabled
                ? $"Saved. A backup is made every {hours} hour(s); the last {keep} automatic ones are kept."
                : "Saved. Automatic backups are off.",
            false);
    }

    private void OpenFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo(_services.Backup.BackupsFolder) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            SetStatus("The folder could not be opened. Its path is: " + _services.Backup.BackupsFolder, true);
        }
    }

    private void SetStatus(string text, bool isError)
    {
        StatusText = text;
        IsStatusError = isError;
    }
}
