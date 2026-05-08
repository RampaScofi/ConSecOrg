using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Infrastructure.Api;
using ConSecOrg.Client.Services;
using ConSecOrg.Client.ViewModels.Base;
using ConSecOrg.Shared.DTOs.Auth;

namespace ConSecOrg.Client.ViewModels.Startup;

public partial class RegisterViewModel(
    IAuthApiService authService,
    SessionService session,
    AppViewModel appVm) : BasePageViewModel
{
    public override string Title => "Регистрация";

    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;
    [ObservableProperty] private string? _errorText;
    [ObservableProperty] private bool _success;

    [RelayCommand]
    private async Task RegisterAsync()
    {
        ErrorText = null;
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Email) ||
            string.IsNullOrWhiteSpace(Password))
        {
            ErrorText = "Заполните все поля.";
            return;
        }
        if (Password != ConfirmPassword)
        {
            ErrorText = "Пароли не совпадают.";
            return;
        }
        if (Password.Length < 12)
        {
            ErrorText = "Пароль должен содержать не менее 12 символов.";
            return;
        }

        await ExecuteAsync(async () =>
        {
            var dto = new RegisterRequestDto
            {
                Username = Username,
                Email = Email,
                Password = Password,
                RoleId = string.Empty
            };
            // Try self-registration first; if no users exist yet, fall back to bootstrap
            try
            {
                await authService.RegisterSelfAsync(dto);
            }
            catch
            {
                await authService.RegisterAsync(dto);
            }

            // Auto-login after registration
            var loginResult = await authService.LoginAsync(new LoginRequestDto
            {
                Username = Username,
                Password = Password
            });
            Password = string.Empty;
            ConfirmPassword = string.Empty;
            session.SetSession(loginResult, []);
            appVm.ShowShell();
        });

        if (!IsBusy && ErrorMessage is not null && ErrorText is null)
        {
            ErrorText = ErrorMessage;
        }
    }

    [RelayCommand]
    private void BackToLogin() => appVm.ShowLogin();
}
