using Area11.WPF.Models;
using Area11.WPF.Services;
using Area11.WPF.ViewModels.Base;

namespace Area11.WPF.ViewModels;

public sealed class LoginViewModel : ObservableObject
{
    private readonly AuthService _auth;
    private string _userName = string.Empty;
    private string _errorMessage = string.Empty;

    public LoginViewModel(AppServices services)
    {
        _auth = services.Auth;
        StoreName = services.Settings.Get("store.name");
        AppName = services.Settings.Get("brand.appName");
    }

    public string AppName { get; }

    public string StoreName { get; }

    public string UserName
    {
        get => _userName;
        set => SetProperty(ref _userName, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool TryLogin(string userName, string secret, out User? user)
    {
        ErrorMessage = string.Empty;
        var result = _auth.Login(userName, secret);

        if (result.Ok && result.Value is not null)
        {
            user = result.Value;
            return true;
        }

        user = null;
        ErrorMessage = result.Error ?? "Sign in failed.";
        return false;
    }
}
