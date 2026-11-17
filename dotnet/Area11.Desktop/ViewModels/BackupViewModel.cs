using System.Windows;
using Area11.Desktop.Views;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;
using Microsoft.Win32;

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
    private string _statusText = "Press \"Back up now\" and copy the encrypted package to a USB stick or your e-mail.";
    private bool _isStatusError;
    private string _lastAutomatic = "No automatic backup yet.";

    public BackupViewModel(AppServices services, User user, Action? onRestored = null)
    {
        _services = services;
        _user = user;
        _onRestored = onRestored;

        ConfigurePasswordCommand = new RelayCommand(ConfigurePassword);
        RestoreFileCommand = new RelayCommand(RestoreFile);
        CreateCommand = new RelayCommand(CreateBackup);
        CreateAsCommand = new RelayCommand(CreateBackupAs);
        RefreshCommand = new RelayCommand(Load);
        AskRestoreCommand = new RelayCommand(AskRestore);
        CancelRestoreCommand = new RelayCommand(CancelRestore);
        RestoreCommand = new RelayCommand(Restore);
        SaveScheduleCommand = new RelayCommand(SaveSchedule);
        OpenFolderCommand = new RelayCommand(OpenFolder);

        Load();
    }

    public ObservableCollection<BackupFile> Files { get; } = new();

    public RelayCommand ConfigurePasswordCommand { get; }

    public RelayCommand RestoreFileCommand { get; }

    public string PasswordStatus => _services.Backup.PasswordConfigured
        ? "Protected local backup password configured. Older files may require an older password."
        : "BACKUPS BLOCKED: Owner must set a backup password for this Windows account. No plaintext fallback.";

    public RelayCommand CreateCommand { get; }

    /// <summary>Save one full backup to a USB stick or another owner-chosen folder.</summary>
    public RelayCommand CreateAsCommand { get; }

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

    public string LastRestoreSummary => _services.Backup.LastRestoreSummary;

    public bool HasRestoreSummary => !string.IsNullOrWhiteSpace(LastRestoreSummary);

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

    /// <summary>Include supported photos in the package; unticked does not delete existing live photos.</summary>
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
        : $"The shop database will be replaced by {Selected.FileName}; included photos will be merged. Stop all shop work first. " +
          $"Filename date (UNVERIFIED hint): {(string.IsNullOrWhiteSpace(Selected.MadeAt) ? Selected.Stamp : Selected.MadeAt)} " +
          $"({_services.Backup.BackupAge(Selected)}). " +
          $"Backup version: {(string.IsNullOrWhiteSpace(Selected.BackupVersion) ? "not recorded" : Selected.BackupVersion)}. " +
          "A full database-and-photos encrypted recovery package of the live shop is kept first. " +
          "If this was the wrong file, restore that encrypted recovery package to return." +
          _services.Backup.RestoreWarning(Selected);

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
        OnPropertyChanged(nameof(PasswordStatus));
        if (!string.IsNullOrWhiteSpace(backup.LastFailure)) SetStatus(backup.LastFailure, true);

        Files.Clear();

        foreach (var file in backup.List())
        {
            Files.Add(file);
        }

        OnPropertyChanged(nameof(HasNoFiles));
        OnPropertyChanged(nameof(LastRestoreSummary));
        OnPropertyChanged(nameof(HasRestoreSummary));

        // Keep the highlighted row if it is still there; otherwise start on the newest.
        var keep = Selected?.FileName;
        Selected = Files.FirstOrDefault(file => file.FileName == keep) ?? Files.FirstOrDefault();
    }

    // ------------------------------ Actions ------------------------------

    private void CreateBackup() => CreateBackup(null);

    private void CreateBackupAs()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Choose where to save this Area11 backup",
            Filter = "Encrypted Area11 backup (*.a11bak)|*.a11bak",
            FileName = $"area11-backup-{DateTime.Now:yyyyMMdd-HHmmss}.a11bak",
            DefaultExt = ".a11bak",
            AddExtension = true,
            // The service deliberately adds a suffix rather than overwriting an earlier backup.
            OverwritePrompt = false
        };

        if (dialog.ShowDialog() == true)
        {
            CreateBackup(dialog.FileName);
        }
    }

    private void CreateBackup(string? destinationFile)
    {
        var result = _services.Backup.Create(_user, IncludePhotos, destinationFile: destinationFile);

        if (!result.Ok || result.Value is null)
        {
            SetStatus(result.Error ?? "The backup could not be made.", true);
            return;
        }

        Load();
        Selected = Files.FirstOrDefault(file => file.FileName == result.Value.FileName);

        if (destinationFile is not null)
        {
            OpenFolder(result.Value.FullPath);
        }

        // Keep the successful result visible even if Explorer itself is unavailable: the full path
        // is the proof the owner needs, and a shell problem must not look like a failed backup.
        SetStatus(
            $"Backup made: {result.Value.FullPath} ({result.Value.SizeLabel}). " +
            "The live database stayed in place. Keep a copy on another disk or computer too.",
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

        RestorePath(chosen.FullPath);
    }

    private void ConfigurePassword()
    {
        if (!_user.IsOwner) { SetStatus("Only the current owner can set or change the backup password.", true); return; }
        var credentials = BackupPasswordDialog.Prompt(setup: true);
        if (credentials is null) return;
        var result = _services.Backup.ConfigurePassword(_user, credentials.Value.Password, credentials.Value.Confirmation);
        OnPropertyChanged(nameof(PasswordStatus));
        SetStatus(result.Ok ? "Backup password stored for this Windows account. Older backups still need their original password. Make and verify a new encrypted backup now."
            : result.Error ?? "Password setup failed.", !result.Ok);
    }

    private void RestoreFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose backup — restore REPLACES the current shop",
            Filter = "Encrypted Area11 backup (*.a11bak)|*.a11bak|Legacy UNVERIFIED ZIP / DB (*.zip;*.db)|*.zip;*.db",
            CheckFileExists = true
        };
        if (dialog.ShowDialog() != true) return;
        if (MessageBox.Show("This replaces the current shop database and restores any included photos. An encrypted full safety copy is required first. Continue?",
            "Restore confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        RestorePath(dialog.FileName);
    }

    private void RestorePath(string path)
    {
        Area11.Core.Utils.ServiceResult<RestoreResult> result;
        if (System.IO.Path.GetExtension(path).Equals(BackupPackage.Extension, StringComparison.OrdinalIgnoreCase))
        {
            var credentials = BackupPasswordDialog.Prompt(setup: false);
            if (credentials is null) return;
            result = _services.Backup.Restore(path, _user, credentials.Value.Password);
        }
        else
        {
            if (MessageBox.Show("OWNER-ONLY LEGACY RECOVERY: this file is NOT authenticated or confidential. Its origin and changes cannot be verified. Use only a trusted legacy ZIP/DB. Accept this risk?",
                "UNVERIFIED legacy recovery", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            result = _services.Backup.RestoreLegacy(path, _user, acknowledgeUnverified: true);
        }
        if (!result.Ok || result.Value is null)
        {
            SetStatus(result.Error ?? "The backup could not be restored.", true);
            return;
        }
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

    private void OpenFolder() => OpenFolder(null);

    private void OpenFolder(string? selectedFile)
    {
        var path = selectedFile ?? _services.Backup.BackupsFolder;

        try
        {
            var start = selectedFile is null
                ? new ProcessStartInfo(path)
                : new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"");
            start.UseShellExecute = true;
            Process.Start(start);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            SetStatus("The folder could not be opened. Its path is: " + path, true);
        }
    }

    private void SetStatus(string text, bool isError)
    {
        StatusText = text;
        IsStatusError = isError;
    }
}
