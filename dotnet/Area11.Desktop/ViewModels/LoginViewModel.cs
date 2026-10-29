using Area11.Core.Models;
using Area11.Desktop;

namespace Area11.Desktop.ViewModels;

public sealed class LoginViewModel : ObservableObject
{
    private readonly AppServices _services;
    private string _userName = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isBusy;

    public LoginViewModel(AppServices services)
    {
        _services = services;
        CanChangeDefaultPassword = services.UsesDefaultOwnerPassword;
        SignInCommand = new RelayCommand(() => SignIn(Secret));
    }

    public event Action<User>? SignedIn;

    public RelayCommand SignInCommand { get; }

    public bool CanChangeDefaultPassword { get; }

    public string StoreName => _services.Settings.GetString("store.name");

    /// <summary>The product title is fixed; the shop name below it remains the store's own data.</summary>
    public string AppName => "Area11";

    public string UserName
    {
        get => _userName;
        set => SetProperty(ref _userName, value);
    }

    public string Secret { get; set; } = string.Empty;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public bool SignIn(string secret)
    {
        if (IsBusy)
        {
            return false;
        }

        IsBusy = true;
        StatusMessage = string.Empty;

        try
        {
            var result = _services.Auth.Login(UserName, secret);
            if (!result.Ok || result.Value is null)
            {
                StatusMessage = result.Error ?? "Sign-in failed.";
                return false;
            }

            _services.SignIn(result.Value);
            Secret = string.Empty;
            SignedIn?.Invoke(result.Value);
            return true;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
