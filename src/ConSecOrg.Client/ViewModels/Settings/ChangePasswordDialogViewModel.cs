using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Infrastructure.Api;
using ConSecOrg.Shared.DTOs.Auth;

namespace ConSecOrg.Client.ViewModels.Settings;

public partial class ChangePasswordDialogViewModel(IAuthApiService authService) : ObservableObject
{
    [ObservableProperty] private string _currentPassword = string.Empty;
    [ObservableProperty] private string _newPassword = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;
    [ObservableProperty] private string? _errorText;
    [ObservableProperty] private bool _isBusy;

    public Action? CloseRequested { get; set; }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorText = null;
        if (string.IsNullOrWhiteSpace(CurrentPassword) || string.IsNullOrWhiteSpace(NewPassword))
        {
            ErrorText = "Заполните все поля.";
            return;
        }
        if (NewPassword != ConfirmPassword)
        {
            ErrorText = "Новые пароли не совпадают.";
            return;
        }
        if (NewPassword.Length < 12)
        {
            ErrorText = "Пароль должен содержать не менее 12 символов.";
            return;
        }

        IsBusy = true;
        try
        {
            await authService.ChangePasswordAsync(new ChangePasswordRequestDto
            {
                CurrentPassword = CurrentPassword,
                NewPassword = NewPassword
            });
            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;
            CloseRequested?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message.Contains("400") || ex.Message.Contains("Invalid")
                ? "Неверный текущий пароль."
                : "Ошибка при смене пароля.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        CurrentPassword = string.Empty;
        NewPassword = string.Empty;
        ConfirmPassword = string.Empty;
        ErrorText = null;
        CloseRequested?.Invoke();
    }
}
