using System.Collections.ObjectModel;
using System.Windows;
using Area11.Core.Models;

// empty-page: none - a dialog opened from Counter/Purchases, not a page of the window frame,
// so no card of the frame is drawn for it; its own list says "no open draft".

namespace Area11.Desktop.ViewModels;

/// <summary>
/// The drafts drawer (master Portion 5): incomplete sales or purchases, shared with everyone who
/// holds the page permission. Open loads one into the page for review; Post now posts it as it is
/// through the same guarded Create; Discard puts it away with a reason in the audit.
/// </summary>
public sealed class DraftsViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;
    private readonly string _type;
    private readonly Action<int> _onOpen;
    private readonly Action _close;
    private DraftSummary? _selected;
    private string _statusText = string.Empty;
    private bool _isStatusError;
    private bool _listLoaded;
    private bool _busy;

    public DraftsViewModel(AppServices services, User user, string draftType, Action<int> onOpen, Action close)
    {
        _services = services;
        _user = user;
        _type = DraftType.Clean(draftType);
        _onOpen = onOpen;
        _close = close;

        OpenCommand = new RelayCommand(Open);
        PostCommand = new RelayCommand(Post);
        DiscardCommand = new RelayCommand(Discard);
        RefreshCommand = new RelayCommand(Refresh);

        Refresh();
    }

    /// <summary>The window title carries the shop's app name (v0.69.0, de-workshop).</summary>
    public string WindowTitle => AppDisplayName + " — Drafts";

    private string AppDisplayName
    {
        get
        {
            var name = _services.Settings.GetString("brand.appName").Trim();
            return name.Length == 0 ? "Area11" : name;
        }
    }

    public string Title => DraftType.LabelFor(_type) + " drafts";

    public string Subtitle => _type == DraftType.Purchase
        ? "Shared purchase drafts: page access lists metadata; opening, updating and posting also require View Purchase Cost and the main switch. Flagged interrupted posts must be checked before discarding."
        : "Incomplete work, shared with everyone holding the page permission. Nothing here has touched stock, money or reports until it is posted.";

    public ObservableCollection<DraftSummary> Drafts { get; } = new();

    public RelayCommand OpenCommand { get; }
    public RelayCommand PostCommand { get; }
    public RelayCommand DiscardCommand { get; }
    public RelayCommand RefreshCommand { get; }

    public DraftSummary? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    public bool HasSelection => Selected is not null;

    public bool HasDrafts => Drafts.Count > 0;

    public bool HasNoDraft => _listLoaded && Drafts.Count == 0;

    public bool HasStatus => StatusText.Length > 0;

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (SetProperty(ref _statusText, value))
            {
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    }

    public bool IsStatusError
    {
        get => _isStatusError;
        private set => SetProperty(ref _isStatusError, value);
    }

    public bool RefreshReadAccess()
    {
        var access = _services.Drafts.CheckReadAccess(_user, _type);
        if (access.Ok) return true;
        ClearDrafts();
        SetStatus(access.Error ?? "Current draft access is required.", true);
        return false;
    }

    private void ClearDrafts()
    {
        _listLoaded = false;
        Selected = null;
        Drafts.Clear();
        OnPropertyChanged(nameof(HasDrafts));
        OnPropertyChanged(nameof(HasNoDraft));
    }

    private bool CurrentSelection(DraftSummary draft, bool includePayload)
    {
        if (!RefreshReadAccess()) return false;
        if (!_listLoaded || !Drafts.Contains(draft))
        {
            SetStatus("Refresh drafts and choose a current row.", true);
            return false;
        }
        var access = _services.Drafts.CheckReadAccess(_user, _type, includePayload);
        if (access.Ok) return true;
        Selected = null;
        SetStatus(access.Error ?? "Current payload access is required.", true);
        return false;
    }

    public void Refresh()
    {
        var id = Selected?.Id;
        ClearDrafts();
        if (!RefreshReadAccess()) return;
        var result = _services.Drafts.List(_user, _type);
        if (!result.Ok || result.Value is null)
        {
            SetStatus(result.Error ?? "The drafts could not be loaded.", true);
            return;
        }

        Drafts.Clear();
        foreach (var draft in result.Value) Drafts.Add(draft);
        _listLoaded = true;
        Selected = Drafts.FirstOrDefault(draft => draft.Id == id) ?? Drafts.FirstOrDefault();
        OnPropertyChanged(nameof(HasDrafts));
        OnPropertyChanged(nameof(HasNoDraft));
        SetStatus(Drafts.Count == 0 ? "No open draft." : Drafts.Count + (Drafts.Count == 1 ? " open draft." : " open drafts."), false);
    }

    private void Open()
    {
        if (Selected is not { } draft)
        {
            SetStatus("Pick a draft first.", true);
            return;
        }
        if (!CurrentSelection(draft, includePayload: true)) return;
        if (draft.NeedsPostCheck)
        {
            SetStatus("That draft cannot be opened: a previous post was interrupted. If its document is on the page, discard this row.", true);
            return;
        }

        _onOpen(draft.Id);
        _close();
    }

    private void Post()
    {
        if (_busy || Selected is not { } draft) return;
        if (!CurrentSelection(draft, includePayload: true)) return;
        if (draft.NeedsPostCheck)
        {
            SetStatus("That draft cannot post again. Verify its document before discarding the marker.", true);
            return;
        }
        _busy = true;
        var saved = false;
        try
        {
            var answer = MessageBox.Show("Post '" + draft.DraftKey + "' now as a real " + _type + "? Stock, money and reports will move.",
                "Post draft", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes || !CurrentSelection(draft, includePayload: true)) return;
            var posted = _type == DraftType.Purchase ? PostPurchase(draft) : PostSale(draft);
            if (!posted.Ok)
            {
                Refresh();
                SetStatus(posted.Error ?? "Check the draft and document before retrying.", true);
                return;
            }
            saved = true;
            Selected = null;
            Drafts.Remove(draft);
            Refresh();
            SetStatus("SAVED. Posted as " + posted.Code + ". Do not post this draft again."
                + (_listLoaded ? string.Empty : " Drafts were not reloaded; refresh.") + posted.Warning, !_listLoaded || posted.Warning.Length > 0);
        }
        catch (Exception exception)
        {
            ClearDrafts();
            _services.Log.Error("Draft post or post-result refresh failed.", exception);
            SetStatus(saved ? "SAVED. Do not post this draft again; refresh drafts and verify its document."
                : "The outcome could not be confirmed. Refresh drafts and verify the document before retrying.", true);
        }
        finally { _busy = false; }
    }

    private (bool Ok, string? Error, string Code, string Warning) PostSale(DraftSummary draft)
    {
        var result = _services.Drafts.PostSale(_user, draft.Id, expected: draft.EditVersion);
        return result.Ok && result.Value is not null
            ? (true, null, result.Value.Code, (result.Value.Warnings.Count > 0 ? " " + string.Join(" ", result.Value.Warnings) : string.Empty))
            : (false, result.Error, string.Empty, string.Empty);
    }

    private (bool Ok, string? Error, string Code, string Warning) PostPurchase(DraftSummary draft)
    {
        var result = _services.Drafts.PostPurchase(_user, draft.Id, expected: draft.EditVersion);
        return result.Ok && result.Value is not null
            ? (true, null, result.Value.Code, (result.Value.Warnings.Count > 0 ? " " + string.Join(" ", result.Value.Warnings) : string.Empty))
            : (false, result.Error, string.Empty, string.Empty);
    }

    private void Discard()
    {
        if (_busy || Selected is not { } draft) return;
        if (!CurrentSelection(draft, includePayload: false)) return;
        _busy = true;
        var saved = false;
        try
        {
            var answer = MessageBox.Show("Put away '" + draft.DraftKey + "'? The row stays in history; nothing else changes.",
                "Discard draft", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes || !CurrentSelection(draft, includePayload: false)) return;
            var result = _services.Drafts.Discard(_user, draft.Id, expected: draft.EditVersion);
            if (!result.Ok)
            {
                Refresh();
                SetStatus(result.Error ?? "Check the draft before retrying.", true);
                return;
            }
            saved = true;
            Selected = null;
            Drafts.Remove(draft);
            Refresh();
            SetStatus("SAVED. Draft put away." + (_listLoaded ? string.Empty : " Drafts were not reloaded; refresh."), !_listLoaded);
        }
        catch (Exception exception)
        {
            ClearDrafts();
            _services.Log.Error("Draft discard or post-result refresh failed.", exception);
            SetStatus(saved ? "SAVED. Draft put away; refresh the list."
                : "The outcome could not be confirmed. Refresh drafts before retrying.", true);
        }
        finally { _busy = false; }
    }

    private void SetStatus(string text, bool isError)
    {
        StatusText = text;
        IsStatusError = isError;
    }
}
