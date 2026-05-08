using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Infrastructure.Api;
using ConSecOrg.Shared.DTOs.Auth;

namespace ConSecOrg.Client.ViewModels.Settings;

public partial class ChangeEmailDialogViewModel(IAuthApiService authService) : ObservableObject
{
    [ObservableProperty] private string _newEmail = string.Empty;
    [ObservableProperty] private string? _errorText;
    [ObservableProperty] private bool _isBusy;

    public Action? CloseRequested { get; set; }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorText = null;
        if (string.IsNullOrWhiteSpace(NewEmail) || !NewEmail.Contains('@'))
        {
            ErrorText = "Введите корректный e-mail адрес.";
            return;
        }

        IsBusy = true;
        try
        {
            await authService.ChangeEmailAsync(new ChangeEmailRequestDto { NewEmail = NewEmail.Trim() });
            NewEmail = string.Empty;
            CloseRequested?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message.Contains("409") || ex.Message.Contains("Conflict")
                ? "Этот e-mail уже используется."
                : "Ошибка при смене e-mail.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        NewEmail = string.Empty;
        ErrorText = null;
        CloseRequested?.Invoke();
    }
}
