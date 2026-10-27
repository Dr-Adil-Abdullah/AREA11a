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

    public UnitWindowViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;
        CanEdit = user.CanManage;
        NewCommand = new RelayCommand(NewUnit);
        SaveCommand = new RelayCommand(Save);
        ToggleCommand = new RelayCommand(Toggle);
        RefreshCommand = new RelayCommand(Load);
        Load();
    }

    public ObservableCollection<Unit> Units { get; } = new();

    public RelayCommand NewCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand ToggleCommand { get; }
    public RelayCommand RefreshCommand { get; }

    public bool CanEdit { get; }
    public bool IsReadOnly => !CanEdit;
    public bool HasNoUnits => Units.Count == 0;
    public bool CanToggle => CanEdit && SelectedUnit is not null;
    public string ToggleLabel => SelectedUnit is { Active: false } ? "Bring back" : "Put away";

    public Unit? SelectedUnit
    {
        get => _selectedUnit;
        set
        {
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

    private void Load()
    {
        var result = _services.Products.GetUnits(activeOnly: false);
        if (!result.Ok || result.Value is null)
        {
            SetStatus(result.Error ?? "Units could not be loaded.", true);
            return;
        }

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
        Name = SelectedUnit?.Name ?? string.Empty;
        OrderText = SelectedUnit?.SortOrder.ToString() ?? "0";
        WholeOnly = SelectedUnit?.WholeOnly ?? false;
        OnPropertyChanged(nameof(HasNoUnits));
    }

    private void NewUnit()
    {
        SelectedUnit = null;
        Name = string.Empty;
        OrderText = ((Units.Select(row => row.SortOrder).DefaultIfEmpty(0).Max() + 10)).ToString();
        WholeOnly = false;
        SetStatus("New unit: enter one canonical name, choose its quantity rule, then Save.", false);
    }

    private void Save()
    {
        if (!int.TryParse(OrderText, out var order) || order < 0)
        {
            SetStatus("Order must be a whole number of zero or more.", true);
            return;
        }

        if (SelectedUnit is null)
        {
            var added = _services.Products.AddUnit(_user, Name, order, WholeOnly);
            if (!added.Ok)
            {
                SetStatus(added.Error ?? "The unit could not be saved.", true);
                return;
            }
            Load();
            SelectedUnit = Units.FirstOrDefault(row => row.Id == added.Value);
        }
        else
        {
            var updated = _services.Products.UpdateUnit(_user, SelectedUnit.Id, Name, order, WholeOnly);
            if (!updated.Ok)
            {
                SetStatus(updated.Error ?? "The unit could not be saved.", true);
                return;
            }
            var id = SelectedUnit.Id;
            Load();
            SelectedUnit = Units.FirstOrDefault(row => row.Id == id);
        }
        SetStatus("Unit saved. Current product labels use the canonical name; stock quantities are unchanged. The whole-unit rule follows the unit, not its name.", false);
    }

    private void Toggle()
    {
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
            SetStatus(result.Error ?? "The unit could not be changed.", true);
            return;
        }
        Load();
        SelectedUnit = Units.FirstOrDefault(row => row.Id == id);
        SetStatus(bringBack ? "Unit brought back." : "Unit put away; no product or history was deleted.", false);
    }

    private void SetStatus(string text, bool error)
    {
        StatusText = text;
        IsStatusError = error;
    }
}
