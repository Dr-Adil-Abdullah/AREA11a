using System.Collections.ObjectModel;
using Area11.Core.Models;
using Area11.Core.Services;
using Area11.Core.Utils;

namespace Area11.Desktop.ViewModels;

/// <summary>
/// Doctors and their prescriptions, the medical reps of the companies, and what each customer has
/// earned the shop (F-44, Spec §8.3.3, §10.4.1, 17.5, 17.7).
///
/// Two different people share this page because the shop plans its day the same way for both:
/// which rep comes on Tuesday, and which doctor sends the patients. Nothing is calculated here -
/// <see cref="RepService"/> owns the money - this class only carries the chosen rows and the form.
/// </summary>
public sealed class DoctorsViewModel : ObservableObject, IEmptyPage
{
    /// <summary>
    /// Why this page has nothing on it, in the shop's words (v0.33.0, D-89). **Null when it
    /// has rows**, so nothing in this page has to keep a second flag in step with its list.
    /// </summary>
    public EmptyHint? EmptyHint => EmptyPage.When(Rows.Count == 0, "Doctors & reps");

    private readonly AppServices _services;
    private readonly User _user;

    private RepCompanyRow? _selectedCompany;
    private VisitDayOption? _selectedDay;
    private string _repNameText = string.Empty;
    private string _repPhoneText = string.Empty;
    private bool _canEditReps;
    private bool _canSeeReps;
    private bool _hasDoctors = true;
    private bool _canSeeDoctors;
    private bool _canSeeProfit;
    private string _statusText = "Pick a company to write down who visits you and on which day.";
    private bool _isStatusError;
    private string _doctorCountText = "0 doctors";
    private string _customerCountText = "0 customers";

    public DoctorsViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;

        DayOptions = VisitDays.Options();
        _selectedDay = DayOptions[0];

        RefreshCommand = new RelayCommand(Load);
        SaveRepCommand = new RelayCommand(SaveRep);

        Load();
    }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand SaveRepCommand { get; }

    // ------------------------------ the reps ------------------------------

    public ObservableCollection<RepCompanyRow> Rows { get; } = new();

    /// <summary>"No fixed day", "Monday", ... - the same list the database numbers 1 to 7.</summary>
    public IReadOnlyList<VisitDayOption> DayOptions { get; }

    public RepCompanyRow? SelectedCompany
    {
        get => _selectedCompany;
        set
        {
            if (!SetProperty(ref _selectedCompany, value))
            {
                return;
            }

            // Choosing a company fills the form with its rep, so the owner edits instead of retyping.
            _repNameText = value?.RepName ?? string.Empty;
            _repPhoneText = value?.RepPhone ?? string.Empty;
            _selectedDay = DayOptions.FirstOrDefault(option => option.Day == (value?.VisitDay ?? VisitDays.None))
                           ?? DayOptions[0];

            OnPropertyChanged(nameof(RepNameText));
            OnPropertyChanged(nameof(RepPhoneText));
            OnPropertyChanged(nameof(SelectedDay));
            OnPropertyChanged(nameof(SelectedCompanyName));
        }
    }

    public string SelectedCompanyName => _selectedCompany?.Name ?? "Choose a company on the left";

    public string RepNameText
    {
        get => _repNameText;
        set => SetProperty(ref _repNameText, value);
    }

    public string RepPhoneText
    {
        get => _repPhoneText;
        set => SetProperty(ref _repPhoneText, value);
    }

    public VisitDayOption? SelectedDay
    {
        get => _selectedDay;
        set
        {
            if (value is not null)
            {
                SetProperty(ref _selectedDay, value);
            }
        }
    }

    /// <summary>False when this login may not see the companies at all - the card is then hidden.</summary>
    public bool CanSeeReps
    {
        get => _canSeeReps;
        private set => SetProperty(ref _canSeeReps, value);
    }

    /// <summary>True for the owner or a manager - the same permission that edits the products.</summary>
    public bool CanEditReps
    {
        get => _canEditReps;
        private set => SetProperty(ref _canEditReps, value);
    }

    // ------------------------------ the doctors ------------------------------

    public ObservableCollection<DoctorRow> Doctors { get; } = new();

    public string DoctorCountText
    {
        get => _doctorCountText;
        private set => SetProperty(ref _doctorCountText, value);
    }

    /// <summary>False when this login may not open the customer list at all.</summary>
    public bool CanSeeDoctors
    {
        get => _canSeeDoctors;
        private set => SetProperty(ref _canSeeDoctors, value);
    }

    public bool HasDoctors
    {
        get => _hasDoctors;
        private set
        {
            if (SetProperty(ref _hasDoctors, value))
            {
                OnPropertyChanged(nameof(HasNoDoctors));
            }
        }
    }

    /// <summary>True when the doctor tab is open but empty - one line explains how a doctor is made.</summary>
    public bool HasNoDoctors => !HasDoctors;

    /// <summary>False for a manager: the profit tab is hidden, not filled with zeros.</summary>
    public bool CanSeeProfit
    {
        get => _canSeeProfit;
        private set => SetProperty(ref _canSeeProfit, value);
    }

    // ------------------------------ lifetime profit ------------------------------

    public ObservableCollection<CustomerProfitRow> Customers { get; } = new();

    public string CustomerCountText
    {
        get => _customerCountText;
        private set => SetProperty(ref _customerCountText, value);
    }

    // ------------------------------ the footer ------------------------------

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

    // ------------------------------ work ------------------------------

    public void Load()
    {
        LoadReps();
        LoadDoctors();
        LoadCustomerProfit();
    }

    private void LoadReps()
    {
        var companies = _services.Reps.ListReps(_user);

        if (!companies.Ok || companies.Value is null)
        {
            Rows.Clear();
            CanSeeReps = false;
            CanEditReps = false;
            SetStatus(companies.Error ?? "The companies could not be loaded.", true);
            return;
        }

        var keep = _selectedCompany?.Id ?? 0;
        var canEdit = _services.Permissions.Can(_user, Modules.Products);

        Rows.Clear();

        foreach (var company in companies.Value)
        {
            Rows.Add(company);
        }

        CanSeeReps = true;
        CanEditReps = canEdit;
        SelectedCompany = Rows.FirstOrDefault(row => row.Id == keep) ?? Rows.FirstOrDefault();
    }

    private void LoadDoctors()
    {
        var doctors = _services.Reps.ListDoctors(_user);

        Doctors.Clear();

        if (!doctors.Ok || doctors.Value is null)
        {
            CanSeeDoctors = false;
            HasDoctors = false;
            DoctorCountText = "not available";
            return;
        }

        CanSeeDoctors = true;

        foreach (var doctor in doctors.Value)
        {
            Doctors.Add(doctor);
        }

        HasDoctors = Doctors.Count > 0;

        var withBills = doctors.Value.Count(row => row.Bills > 0);
        DoctorCountText = Doctors.Count + (Doctors.Count == 1 ? " doctor" : " doctors")
                          + " · " + withBills + " with prescriptions";
    }

    private void LoadCustomerProfit()
    {
        var profit = _services.Reps.ListCustomerProfit(_user);

        Customers.Clear();
        CanSeeProfit = profit.Ok && profit.Value is not null;

        if (!CanSeeProfit || profit.Value is null)
        {
            CustomerCountText = "owner only";
            return;
        }

        foreach (var customer in profit.Value)
        {
            Customers.Add(customer);
        }

        CustomerCountText = Customers.Count == 1 ? "1 customer" : Customers.Count + " customers";
    }

    private void SaveRep()
    {
        if (_selectedCompany is null)
        {
            SetStatus("Pick a company on the left first.", true);
            return;
        }

        var saved = _services.Reps.SaveRep(
            _user, _selectedCompany.Id, RepNameText, RepPhoneText, SelectedDay?.Day);

        if (!saved.Ok)
        {
            SetStatus(saved.Error ?? "The rep could not be saved.", true);
            return;
        }

        var company = _selectedCompany.Name;

        LoadReps();
        SetStatus(RepNameText.Trim().Length == 0
            ? "The rep of " + company + " is cleared. The old line stays in the black box."
            : "Saved: " + company + " · " + RepNameText.Trim() + " · " + (SelectedDay?.Label ?? "no fixed day") + ".",
            false);
    }

    private void SetStatus(string text, bool isError)
    {
        StatusText = text;
        IsStatusError = isError;
    }
}
