using System.Collections.ObjectModel;
using Area11.Core.Models;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>Small managed-unit editor. Core owns uniqueness, usage and audit rules.</summary>
public sealed class UnitWindowViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;
    private Unit? _selectedUnit;
    private string _name = string.Empty;
    private string _orderText = "0";
    private bool _wholeOnly;
    private string _statusText = "Add a unit such as injection or ml. Used units cannot be put away.";
    private bool _isStatusError;
    private bool _loading;
    private bool _lastLoadSucceeded;
    private bool _canEdit;

    public UnitWindowViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;
        NewCommand = new RelayCommand(NewUnit);
        SaveCommand = new RelayCommand(Save);
        ToggleCommand = new RelayCommand(Toggle);
        RefreshCommand = new RelayCommand(() => Load());
        Load();
    }

    /// <summary>The window title carries the shop's app name (v0.69.0, de-workshop).</summary>
    public string WindowTitle => AppDisplayName + " — Manage units";

    private string AppDisplayName
    {
        get
        {
            var name = _services.Settings.GetString("brand.appName").Trim();
            return name.Length == 0 ? "Area11" : name;
        }
    }

    public ObservableCollection<Unit> Units { get; } = new();

    public RelayCommand NewCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand ToggleCommand { get; }
    public RelayCommand RefreshCommand { get; }

    public bool CanEdit
    {
        get => _canEdit;
        private set
        {
            if (!SetProperty(ref _canEdit, value)) return;
            OnPropertyChanged(nameof(IsReadOnly));
            OnPropertyChanged(nameof(CanToggle));
        }
    }
    public bool IsReadOnly => !CanEdit;
    public bool HasNoUnits => _lastLoadSucceeded && Units.Count == 0;
    public bool CanToggle => CanEdit && SelectedUnit is not null;
    public string ToggleLabel => SelectedUnit is { Active: false } ? "Bring back" : "Put away";

    public Unit? SelectedUnit
    {
        get => _selectedUnit;
        set
        {
            if (!_loading && value is not null && (!RefreshAccess() || !_lastLoadSucceeded || !Units.Contains(value))) return;
            if (!SetProperty(ref _selectedUnit, value))
            {
                return;
            }
            OnPropertyChanged(nameof(CanToggle));
            OnPropertyChanged(nameof(ToggleLabel));
            if (!_loading)
            {
                Name = value?.Name ?? string.Empty;
                OrderText = value?.SortOrder.ToString() ?? "0";
                WholeOnly = value?.WholeOnly ?? false;
            }
        }
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string OrderText
    {
        get => _orderText;
        set => SetProperty(ref _orderText, value);
    }

    public bool WholeOnly
    {
        get => _wholeOnly;
        set => SetProperty(ref _wholeOnly, value);
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

    private bool Load()
    {
        if (!RefreshAccess()) return false;
        var result = _services.Products.GetUnits(activeOnly: false);
        if (!result.Ok || result.Value is null)
        {
            ClearManager(result.Error ?? "Units could not be loaded.");
            return false;
        }

        if (!RefreshAccess()) return false;
        var selectedId = SelectedUnit?.Id;
        _loading = true;
        try
        {
            Units.Clear();
            foreach (var unit in result.Value)
            {
                Units.Add(unit);
            }
            SelectedUnit = Units.FirstOrDefault(row => row.Id == selectedId) ?? Units.FirstOrDefault();
        }
        finally
        {
            _loading = false;
        }
        _lastLoadSucceeded = true;
        if (!RefreshAccess()) return false;
        Name = SelectedUnit?.Name ?? string.Empty;
        OrderText = SelectedUnit?.SortOrder.ToString() ?? "0";
        WholeOnly = SelectedUnit?.WholeOnly ?? false;
        OnPropertyChanged(nameof(HasNoUnits));
        return true;
    }

    private void NewUnit()
    {
        if (!RequireEdit()) return;
        SelectedUnit = null;
        Name = string.Empty;
        OrderText = ((Units.Select(row => row.SortOrder).DefaultIfEmpty(0).Max() + 10)).ToString();
        WholeOnly = false;
        SetStatus("New unit: enter one canonical name, choose its quantity rule, then Save.", false);
    }

    private void Save()
    {
        if (!RequireEdit()) return;
        if (!int.TryParse(OrderText, out var order) || order < 0)
        {
            SetStatus("Order must be a whole number of zero or more.", true);
            return;
        }

        int id;
        if (SelectedUnit is null)
        {
            var added = _services.Products.AddUnit(_user, Name, order, WholeOnly);
            if (!added.Ok)
            {
                if (!RefreshAccess()) return;
                SetStatus(added.Error ?? "The unit could not be saved.", true);
                return;
            }
            id = added.Value;
        }
        else
        {
            id = SelectedUnit.Id;
            var updated = _services.Products.UpdateUnit(_user, id, Name, order, WholeOnly);
            if (!updated.Ok)
            {
                if (!RefreshAccess()) return;
                SetStatus(updated.Error ?? "The unit could not be saved.", true);
                return;
            }
        }
        if (!ReloadAfterWrite(id)) return;
        SetStatus("Unit saved. Current product labels use the canonical name; stock quantities are unchanged. The whole-unit rule follows the unit, not its name.", false);
    }

    private void Toggle()
    {
        if (!RequireEdit()) return;
        if (SelectedUnit is null)
        {
            SetStatus("Pick a unit first.", true);
            return;
        }
        var id = SelectedUnit.Id;
        var bringBack = !SelectedUnit.Active;
        var result = _services.Products.SetUnitActive(_user, id, bringBack);
        if (!result.Ok)
        {
            if (!RefreshAccess()) return;
            SetStatus(result.Error ?? "The unit could not be changed.", true);
            return;
        }
        if (!ReloadAfterWrite(id)) return;
        SetStatus(bringBack ? "Unit brought back." : "Unit put away; no product or history was deleted.", false);
    }

    /// <summary>Access-only tick. Regrant requires explicit Refresh, not stale input restoration.</summary>
    public bool RefreshAccess()
    {
        var access = _services.Products.CheckCatalogAccess(_user);
        if (!access.Ok)
        {
            ClearManager(access.Error ?? "Current Products access is required.");
            return false;
        }
        CanEdit = _lastLoadSucceeded && access.Value;
        return true;
    }

    private bool RequireEdit()
    {
        if (!RefreshAccess()) return false;
        if (CanEdit) return true;
        SetStatus("Current Products Owner/Manager access and an explicit successful Refresh are required before editing.", true);
        return false;
    }

    private void ClearManager(string error)
    {
        CanEdit = false;
        _lastLoadSucceeded = false;
        _loading = true;
        try
        {
            SelectedUnit = null;
            Units.Clear();
            Name = OrderText = string.Empty;
            WholeOnly = false;
            OnPropertyChanged(nameof(HasNoUnits));
        }
        finally { _loading = false; }
        SetStatus(error + " Unsaved unit inputs were discarded. Refresh and review before editing.", true);
    }

    private bool ReloadAfterWrite(int id)
    {
        try
        {
            if (Load())
            {
                SelectedUnit = Units.FirstOrDefault(row => row.Id == id);
                if (SelectedUnit?.Id == id && RefreshAccess() && _lastLoadSucceeded) return true;
            }
            ClearManager("The saved unit selection could not be reloaded.");
        }
        catch (Exception exception)
        {
            _services.Log.Error("Unit change was saved but could not reload.", exception);
            ClearManager("The saved unit could not be reloaded.");
        }
        SetStatus("SAVED. The unit change was stored, but could not reload. Do not repeat the action; refresh and review first.", true);
        return false;
    }

    private void SetStatus(string text, bool error)
    {
        StatusText = text;
        IsStatusError = error;
    }
}
