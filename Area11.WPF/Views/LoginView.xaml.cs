using System.Windows;
using Area11.WPF.Models;
using Area11.WPF.ViewModels;

namespace Area11.WPF.Views;

public partial class LoginView : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginView(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += (_, _) => UserInput.Focus();
    }

    public User? CurrentUser { get; private set; }

    private void OnSignInClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.TryLogin(UserInput.Text, PasswordInput.Password, out var user))
        {
            CurrentUser = user;
            DialogResult = true;
            return;
        }

        PasswordInput.Clear();
        PasswordInput.Focus();
    }
}
