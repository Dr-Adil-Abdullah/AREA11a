using System.Collections.ObjectModel;
using System.Windows;
using Area11.Core.Models;

// empty-page: none - an embedded settings panel with explicit owner and loading status.
namespace Area11.Desktop.ViewModels;

/// <summary>Owner policy metadata; final-post safety stays in the transaction services.</summary>
public sealed class NativeFieldsViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;
    private FormFieldDefinition? _selected;
    private bool _lastLoadSucceeded;
    private bool _saving;
    private bool _needsReload;
    private string? _reviewStatus;
    private string _statusText = string.Empty;

    public NativeFieldsViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;
        RequireCommand = new RelayCommand(() => Change(true));
        OptionalCommand = new RelayCommand(() => Change(false));
        RefreshCommand = new RelayCommand(Refresh);
        Refresh();
    }

    public bool IsOwner => _user.IsOwner;
    public ObservableCollection<FormFieldDefinition> Fields { get; } = new();
    public RelayCommand RefreshCommand { get; }
    public RelayCommand RequireCommand { get; }
    public RelayCommand OptionalCommand { get; }
    public FormFieldDefinition? Selected
    {
        get => _selected;
        set
        {
            if (_saving || (value is not null && !Fields.Contains(value))) return;
            SetProperty(ref _selected, value);
        }
    }
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    private void ClearFields()
    {
        _lastLoadSucceeded = false;
        _selected = null; // Invalidate before any observable collection/binding callback.
        Fields.Clear();
        OnPropertyChanged(nameof(Selected));
    }

    public void Refresh()
    {
        if (_saving) return;
        var id = _selected?.Id;
        _needsReload = false;
        _reviewStatus = null;
        ClearFields();
        var result = _services.NativeFields.List(_user);
        if (!result.Ok || result.Value is null)
        {
            StatusText = result.Error ?? "The fields could not be loaded.";
            return;
        }
        foreach (var field in result.Value) Fields.Add(field);
        _selected = Fields.FirstOrDefault(field => field.Id == id) ?? Fields.FirstOrDefault();
        _lastLoadSucceeded = true;
        OnPropertyChanged(nameof(Selected));
        StatusText = string.Empty;
    }

    /// <summary>Allowed timer reads never replace the loaded baseline. An uncertain save requires
    /// explicit Refresh/review,not an automatic timer/regrant rebase.</summary>
    public bool RefreshAccess()
    {
        if (_saving) return _lastLoadSucceeded;
        if (_needsReload) return false;
        if (!_lastLoadSucceeded)
        {
            Refresh();
            return _lastLoadSucceeded;
        }
        var result = _services.NativeFields.List(_user);
        if (result.Ok && result.Value is not null) return true;
        ClearFields();
        StatusText = result.Error ?? "The fields could not be loaded.";
        return false;
    }

    // Persist the outcome/invalidation before any callback. A broken binding must not erase SAVED
    // or turn the next click/timer into another write. Explicit Refresh is a separate read/review.
    private void RequireReview(string message)
    {
        _needsReload = true;
        _lastLoadSucceeded = false;
        _selected = null;
        _reviewStatus = _statusText = message;
        try { OnPropertyChanged(nameof(StatusText)); }
        catch (Exception exception) { _services.Log.Error("Built-in field status notification failed.", exception); }
        try { Fields.Clear(); OnPropertyChanged(nameof(Selected)); }
        catch (Exception exception) { _services.Log.Error("Built-in field invalidation notification failed.", exception); }
    }

    private void Change(bool required)
    {
        if (_saving) return;
        if (_needsReload || !_lastLoadSucceeded || _selected is not { } selected || !Fields.Contains(selected))
        { StatusText = _reviewStatus ?? "Refresh Fields and select a field before changing its preference."; return; }
        _saving = true;
        var acknowledged = false;
        try
        {
            var prepared = _services.NativeFields.PrepareRequiredEdit(_user, selected.Id, selected.EditVersion);
            if (!prepared.Ok || prepared.Value is not { } field)
            {
                RequireReview(prepared.Error ?? "The selected field changed. Refresh and review before saving.");
                return;
            }
            var confirmed = false;
            if (!required && field.Required && field.Critical && field.SafeRequired)
            {
                var answer = MessageBox.Show(
                    "Making '" + field.Label + "' optional can create incomplete financial information. "
                    + "This records your preference only: an incomplete sale or purchase still cannot be posted. Confirm this risk?",
                    "Critical field warning", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                if (answer != MessageBoxResult.Yes) return;
                confirmed = true;
            }
            if (!ReferenceEquals(_selected, selected) || !Fields.Contains(selected))
            { RequireReview("The selection changed. Refresh and review before saving."); return; }
            var result = _services.NativeFields.SetRequiredChecked(_user, field.Id, required, confirmed, field.EditVersion);
            if (!result.Ok)
            {
                if (!result.NoCommitConfirmed) RequireReview(result.Error ?? "Built-in field saving is unconfirmed. Refresh and verify.");
                else StatusText = result.Error ?? "The preference was not saved; no commit was made.";
                return;
            }
            acknowledged = true;
            var saved = result.NativeFieldEdit;
            if (saved?.EditVersion is null || saved.Id != field.Id || !ReferenceEquals(_selected, selected) || !Fields.Contains(selected))
            { RequireReview("SAVED. The preference was stored, but fields could not be reloaded. Refresh before retrying."); return; }
            var index = Fields.IndexOf(selected);
            _selected = saved;
            Fields[index] = saved; // Exact post-write field only;never fresh-read/rebase other loaded rows.
            OnPropertyChanged(nameof(Selected));
            if (!ReferenceEquals(_selected, saved) || !Fields.Contains(saved) || saved.Required != required)
                throw new InvalidOperationException("The saved field handoff was not retained.");
            StatusText = saved.FieldKey is "product.generic" or "product.brand" or "product.rack" or "product.pack" or "product.retail"
                ? "SAVED. The product form pops up a warning when these are empty - saving anyway stays your choice."
                : saved.FieldKey is "customer.phone" or "supplier.phone" or "supplier.agency"
                    ? "SAVED for file import. The customer and supplier forms do not check it yet."
                    : "SAVED. Preference stored in the audit. Sale and purchase posting still enforce their own rules.";
        }
        catch (Exception exception)
        {
            _services.Log.Error("Built-in field save/refresh did not finish.", exception);
            RequireReview(acknowledged ? "SAVED. The preference was stored, but fields could not be reloaded. Refresh before retrying."
                : "Built-in field saving is unconfirmed. Refresh and verify before retrying.");
        }
        finally { _saving = false; }
    }
}
