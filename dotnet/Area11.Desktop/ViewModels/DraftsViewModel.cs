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

    public string Subtitle => "Incomplete work, shared with everyone holding the page permission. " +
        "Nothing here has touched stock, money or reports until it is posted.";

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

    public bool HasNoDraft => Drafts.Count == 0;

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

    public void Refresh()
    {
        var id = Selected?.Id;
        var result = _services.Drafts.List(_user, _type);
        if (!result.Ok || result.Value is null)
        {
            SetStatus(result.Error ?? "The drafts could not be loaded.", true);
            return;
        }

        Drafts.Clear();
        foreach (var draft in result.Value) Drafts.Add(draft);
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
        if (Selected is not { } draft)
        {
            SetStatus("Pick a draft first.", true);
            return;
        }
        if (draft.NeedsPostCheck)
        {
            SetStatus("That draft can never post again: a previous post was interrupted. Discard it once its document is verified.", true);
            return;
        }

        var answer = MessageBox.Show(
            "Post '" + draft.DraftKey + "' now as a real " + _type + "? Stock, money and reports will move.",
            "Post draft", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        var posted = _type == DraftType.Purchase
            ? PostPurchase(draft.Id)
            : PostSale(draft.Id);
        if (!posted.Ok)
        {
            SetStatus(posted.Error ?? "The draft could not be posted. It is kept.", true);
            return;
        }

        SetStatus("Posted as " + posted.Code + ".", false);
        Refresh();
    }

    private (bool Ok, string? Error, string Code) PostSale(int draftId)
    {
        var result = _services.Drafts.PostSale(_user, draftId);
        return result.Ok && result.Value is not null
            ? (true, null, result.Value.Code)
            : (false, result.Error, string.Empty);
    }

    private (bool Ok, string? Error, string Code) PostPurchase(int draftId)
    {
        var result = _services.Drafts.PostPurchase(_user, draftId);
        return result.Ok && result.Value is not null
            ? (true, null, result.Value.Code)
            : (false, result.Error, string.Empty);
    }

    private void Discard()
    {
        if (Selected is not { } draft)
        {
            SetStatus("Pick a draft first.", true);
            return;
        }

        var answer = MessageBox.Show(
            "Put away '" + draft.DraftKey + "'? The row stays in history; nothing else changes.",
            "Discard draft", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        var result = _services.Drafts.Discard(_user, draft.Id);
        if (!result.Ok)
        {
            SetStatus(result.Error ?? "The draft could not be discarded.", true);
            return;
        }

        SetStatus("Draft put away.", false);
        Refresh();
    }

    private void SetStatus(string text, bool isError)
    {
        StatusText = text;
        IsStatusError = isError;
    }
}
