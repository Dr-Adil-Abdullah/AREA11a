using System.Collections.ObjectModel;
using Area11.Core.Models;

namespace Area11.Desktop.ViewModels;

/// <summary>Read-only inspector. Each refresh replaces its data; failed owner checks clear names.</summary>
public sealed class PermissionDetailsViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly User _user;
    private readonly string _module;
    private string _title = "Permission details";
    private string _scope = string.Empty;
    private string _prerequisites = string.Empty;
    private string _rolloutNote = string.Empty;
    private string _mainSwitchLabel = string.Empty;
    private string _status = string.Empty;
    private string _counts = string.Empty;

    public PermissionDetailsViewModel(AppServices services, User user, string module)
    {
        _services = services;
        _user = user;
        _module = module;
        RefreshCommand = new RelayCommand(Refresh);
        Refresh();
    }

    public ObservableCollection<PermissionAssignment> People { get; } = new();
    public RelayCommand RefreshCommand { get; }
    public string Title { get => _title; private set => SetProperty(ref _title, value); }
    public string Scope { get => _scope; private set => SetProperty(ref _scope, value); }
    public string Prerequisites { get => _prerequisites; private set => SetProperty(ref _prerequisites, value); }
    public string RolloutNote { get => _rolloutNote; private set => SetProperty(ref _rolloutNote, value); }
    public string MainSwitchLabel { get => _mainSwitchLabel; private set => SetProperty(ref _mainSwitchLabel, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string Counts { get => _counts; private set => SetProperty(ref _counts, value); }

    public void Refresh()
    {
        // Never retain a successful list on a failed refresh or revoked owner account.
        People.Clear();
        Counts = string.Empty;
        MainSwitchLabel = string.Empty;
        var result = _services.PermissionInspector.Inspect(_user, _module);
        if (!result.Ok || result.Value is null)
        {
            Scope = string.Empty; Prerequisites = string.Empty; RolloutNote = string.Empty;
            Title = "Permission details";
            Status = result.Error ?? "Assignments could not be read.";
            return;
        }
        var data = result.Value;
        Title = data.Definition.Title;
        Scope = data.Definition.Scope;
        Prerequisites = data.Definition.Prerequisites;
        RolloutNote = data.Definition.RolloutNote;
        MainSwitchLabel = data.MainSwitchLabel;
        foreach (var person in data.People) People.Add(person);
        Counts = People.Count(person => person.Assigned) + " enabled / "
            + People.Count(person => !person.Assigned) + " disabled (includes inactive accounts; see Access column).";
        Status = "Read-only assignments. Auto-refresh every 3 seconds; not instantaneous revocation. Last read: "
            + DateTime.Now.ToString("HH:mm:ss") + ".";
    }
}
