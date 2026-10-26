using System.Collections.ObjectModel;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// Cloud sync (F-48, Spec 25): the shop's data copied to Supabase so the owner can look at the numbers
/// from home.
///
/// No rule lives here. <see cref="Area11.Core.Services.SyncService"/> decides what may travel, what a
/// push and a pull may write, and when a backup is needed; this class carries the counts, the three
/// buttons and the sentence at the bottom.
///
/// Why the apply button stays disabled until the box is ticked: pulling writes the cloud's older rows
/// over this computer's rows, and the one thing that must never happen by itself is that write. The
/// page makes it two deliberate clicks - see the cloud, then agree (D-66).
/// </summary>
public sealed class CloudSyncViewModel : ObservableObject, IEmptyPage
{
    /// <summary>
    /// Why this page has nothing on it, in the shop's words (v0.33.0, D-89). **Null when it
    /// has rows**, so nothing in this page has to keep a second flag in step with its list.
    /// </summary>
    public EmptyHint? EmptyHint => EmptyPage.When(CloudRows.Count == 0, "Cloud sync");

    private readonly AppServices _services;
    private readonly User _user;

    private SyncPreview? _preview;

    private string _configText = "Cloud sync is not set up yet";
    private string _urlText = "-";
    private string _tableText = "-";
    private string _shopIdText = "-";
    private string _keyText = "no key";
    private string _lastSyncText = "never sent from this computer";
    private string _previewText = "Press \"See what the cloud has\" to read the copy without writing anything.";
    private string _sentText = string.Empty;
    private bool _hasPreview;
    private bool _confirmApply;
    private string _statusText = "This page copies the shop's data to the cloud. Nothing is written on this computer until you tick the box and press the last button.";
    private bool _isStatusError;

    public CloudSyncViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        PushCommand = new RelayCommand(Push);
        PreviewCommand = new RelayCommand(Preview);
        ApplyCommand = new RelayCommand(Apply);
        RefreshCommand = new RelayCommand(Refresh);

        Load();
    }

    public RelayCommand PushCommand { get; }

    public RelayCommand PreviewCommand { get; }

    public RelayCommand ApplyCommand { get; }

    public RelayCommand RefreshCommand { get; }

    /// <summary>What this computer holds, per table (the cloud column shows "-" until it is read).</summary>
    public ObservableCollection<SyncTableCount> Rows { get; } = new();

    /// <summary>What the cloud holds, next to what is here - the comparison the owner decides on.</summary>
    public ObservableCollection<SyncTableCount> CloudRows { get; } = new();

    // ------------------------------ the setup ------------------------------

    public string ConfigText
    {
        get => _configText;
        private set => SetProperty(ref _configText, value);
    }

    public string UrlText
    {
        get => _urlText;
        private set => SetProperty(ref _urlText, value);
    }

    public string TableText
    {
        get => _tableText;
        private set => SetProperty(ref _tableText, value);
    }

    public string ShopIdText
    {
        get => _shopIdText;
        private set => SetProperty(ref _shopIdText, value);
    }

    public string KeyText
    {
        get => _keyText;
        private set => SetProperty(ref _keyText, value);
    }

    public string LastSyncText
    {
        get => _lastSyncText;
        private set => SetProperty(ref _lastSyncText, value);
    }

    // ------------------------------ the comparison ------------------------------

    public string PreviewText
    {
        get => _previewText;
        private set => SetProperty(ref _previewText, value);
    }

    public string SentText
    {
        get => _sentText;
        private set => SetProperty(ref _sentText, value);
    }

    public bool HasPreview
    {
        get => _hasPreview;
        private set
        {
            if (SetProperty(ref _hasPreview, value))
            {
                OnPropertyChanged(nameof(CanApply));
            }
        }
    }

    /// <summary>The owner's own hand on the rule: no tick, no write.</summary>
    public bool ConfirmApply
    {
        get => _confirmApply;
        set
        {
            if (SetProperty(ref _confirmApply, value))
            {
                OnPropertyChanged(nameof(CanApply));
            }
        }
    }

    public bool CanApply => HasPreview && ConfirmApply && !IsStatusError;

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
        var status = _services.Cloud.Status(_user);

        if (!status.Ok || status.Value is null)
        {
            SetStatus(status.Error ?? "The cloud sync page could not be opened.", true);
            return;
        }

        var value = status.Value;

        ConfigText = value.ConfigLabel;
        UrlText = value.Url.Length == 0 ? "not filled in" : value.Url;
        TableText = value.Table;
        ShopIdText = value.ShopId.Length == 0 ? "not filled in" : value.ShopId;
        KeyText = value.KeyLabel;
        LastSyncText = value.LastSyncLabel + " - " + value.StatusLabel;

        Rows.Clear();

        foreach (var count in value.Counts)
        {
            Rows.Add(new SyncTableCount(count.Table, -1, count.Here));
        }
    }

    private void Push()
    {
        var run = _services.Cloud.Push(_user);

        if (!run.Ok || run.Value is null)
        {
            SetStatus(run.Error ?? "The data could not be sent.", true);
            return;
        }

        // A push makes the previously previewed cloud document stale. It must not remain applicable.
        ClearPreview();
        SetStatus("Sent. " + run.Value.SummaryLabel, false);
        Load();
    }

    private void Preview()
    {
        var preview = _services.Cloud.Preview(_user);

        if (!preview.Ok || preview.Value is null)
        {
            ClearPreview();
            SetStatus(preview.Error ?? "The cloud copy could not be read.", true);
            return;
        }

        _preview = preview.Value;
        ConfirmApply = false;

        CloudRows.Clear();

        foreach (var table in preview.Value.Tables)
        {
            CloudRows.Add(table);
        }

        PreviewText = preview.Value.SummaryLabel;
        SentText = preview.Value.SentLabel;
        HasPreview = true;

        SetStatus(
            preview.Value.IsEmpty
                ? "The cloud has no rows for '" + ShopIdText + "' yet. Push from the shop first."
                : "Read the cloud copy - nothing on this computer has been written. "
                  + (preview.Value.HasDifferences
                      ? preview.Value.DifferentTables + " table(s) differ from this computer."
                      : "Every table has the same number of rows."),
            preview.Value.IsEmpty);
    }

    private void Apply()
    {
        if (_preview is null)
        {
            SetStatus("Press \"See what the cloud has\" first.", true);
            return;
        }

        var result = _services.Cloud.Apply(_user, _preview.Envelope, ConfirmApply);

        if (!result.Ok || result.Value is null)
        {
            SetStatus(result.Error ?? "The cloud copy could not be written.", true);
            return;
        }

        ClearPreview();

        SetStatus("Done. " + result.Value.SummaryLabel
                  + " Nothing was deleted - every row this computer had before is still here.", false);
        Load();
    }

    private void Refresh()
    {
        // Refresh can follow a settings edit or another counter's work. The old comparison is not
        // proof of what is in the cloud now, so require a fresh read before the next apply.
        ClearPreview();
        Load();
    }

    private void ClearPreview()
    {
        _preview = null;
        HasPreview = false;
        ConfirmApply = false;
        CloudRows.Clear();
        SentText = string.Empty;
        PreviewText = "Nothing is loaded. Press \"See what the cloud has\" before the next pull.";
    }

    private void SetStatus(string text, bool isError)
    {
        StatusText = text;
        IsStatusError = isError;
        OnPropertyChanged(nameof(CanApply));
    }
}
