using Area11.Core.Models;
using Area11.Core.Utils;
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

    /// <summary>Portion 16: the sign-in window needs the startup policy for its own ×.</summary>
    public AppServices Services => _services;

    public event Action<User>? SignedIn;

    public RelayCommand SignInCommand { get; }

    public bool CanChangeDefaultPassword { get; }

    public string StoreName => _services.Settings.GetString("store.name");

    /// <summary>Owner's word (Nov 2026, reverses D-77): the sign-in screen carries his app name.</summary>
    public string AppName
    {
        get
        {
            var name = _services.Settings.GetString("brand.appName").Trim();
            return name.Length == 0 ? "Area11" : name;
        }
    }

    public string WindowTitle => AppName + " — " + BuildInfo.Label;

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
