using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Infrastructure.Api;
using ConSecOrg.Client.Services;
using ConSecOrg.Client.ViewModels.Base;
using ConSecOrg.Shared.DTOs.Auth;

namespace ConSecOrg.Client.ViewModels.Startup;

public partial class LoginViewModel(
    IAuthApiService authService,
    SessionService session,
    AppViewModel appVm) : BasePageViewModel
{
    public override string Title => "Вход";

    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string? _errorText;

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorText = null;
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorText = "Введите логин и пароль.";
            return;
        }

        await ExecuteAsync(async () =>
        {
            var result = await authService.LoginAsync(new LoginRequestDto
            {
                Username = Username,
                Password = Password
            });

            session.SetSession(result, []);
            Password = string.Empty;
            appVm.ShowShell();
        });

        if (!IsBusy && ErrorMessage is not null)
            ErrorText = ErrorMessage;
    }

    [RelayCommand]
    private void NavigateRegister() => appVm.ShowRegister();

    [RelayCommand]
    private void GoBack() => appVm.ShowModeSelection();
}
