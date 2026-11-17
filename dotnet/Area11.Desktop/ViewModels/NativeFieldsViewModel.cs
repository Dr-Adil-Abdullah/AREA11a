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
    private string _statusText = string.Empty;

    public NativeFieldsViewModel(AppServices services, User user)
    {
        _services = services;
        _user = user;
        RequireCommand = new RelayCommand(() => Change(true));
        OptionalCommand = new RelayCommand(() => Change(false));
        Refresh();
    }

    public bool IsOwner => _user.IsOwner;
    public ObservableCollection<FormFieldDefinition> Fields { get; } = new();
    public RelayCommand RequireCommand { get; }
    public RelayCommand OptionalCommand { get; }
    public FormFieldDefinition? Selected
    {
        get => _selected;
        set => SetProperty(ref _selected, value);
    }
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public void Refresh()
    {
        if (!IsOwner) return;
        var id = Selected?.Id;
        var result = _services.NativeFields.List(_user);
        if (!result.Ok || result.Value is null)
        {
            StatusText = result.Error ?? "The fields could not be loaded.";
            return;
        }
        Fields.Clear();
        foreach (var field in result.Value) Fields.Add(field);
        Selected = Fields.FirstOrDefault(field => field.Id == id) ?? Fields.FirstOrDefault();
    }

    private void Change(bool required)
    {
        if (!IsOwner || Selected is not { } field)
        {
            StatusText = "The owner must select a field first.";
            return;
        }
        var confirmed = false;
        if (!required && field.Required && field.Critical && field.SafeRequired)
        {
            var answer = MessageBox.Show(
                "Making '" + field.Label + "' optional can create incomplete financial information. "
                + "This records your preference only: drafts are not available yet and an incomplete "
                + "sale or purchase still cannot be posted. Confirm this risk?",
                "Critical field warning", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes) return;
            confirmed = true;
        }
        var result = _services.NativeFields.SetRequired(_user, field.Id, required, confirmed);
        StatusText = result.Ok
            ? field.FieldKey is "product.generic" or "product.brand" or "product.rack" or "product.pack" or "product.retail"
                ? "Saved. The product form pops up a warning when these are empty - saving anyway stays your choice."
                : field.FieldKey is "customer.phone" or "supplier.phone" or "supplier.agency"
                    ? "Saved for file import. The customer and supplier forms do not check it yet."
                    : "Preference saved in the audit. Sale and purchase posting still enforce their own rules."
            : result.Error ?? "The preference could not be saved.";
        if (result.Ok) Refresh();
    }
}
