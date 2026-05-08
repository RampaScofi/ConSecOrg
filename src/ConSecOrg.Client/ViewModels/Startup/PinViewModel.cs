using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Infrastructure.Local;
using ConSecOrg.Client.Services;
using ConSecOrg.Client.ViewModels.Base;
using ConSecOrg.Shared.DTOs.Auth;
using ConSecOrg.Shared.Enums;

namespace ConSecOrg.Client.ViewModels.Startup;

public partial class PinViewModel : BasePageViewModel
{
    private readonly LocalUserStore _userStore;
    private readonly PersonalDbContext _db;
    private readonly SessionService _session;
    private readonly AppViewModel _appVm;

    public override string Title => IsSettingPin ? "Создание PIN-кода" : "Вход в личный органайзер";

    public bool IsSettingPin => !_userStore.HasPin;

    [ObservableProperty] private string _pin = string.Empty;
    [ObservableProperty] private string _confirmPin = string.Empty;
    [ObservableProperty] private string? _errorText;

    public PinViewModel(
        LocalUserStore userStore,
        PersonalDbContext db,
        SessionService session,
        AppViewModel appVm)
    {
        _userStore = userStore;
        _db = db;
        _session = session;
        _appVm = appVm;
    }

    [RelayCommand]
    private void GoBack() => _appVm.ShowModeSelection();

    [RelayCommand]
    private async Task SubmitAsync()
    {
        ErrorText = null;

        if (string.IsNullOrWhiteSpace(Pin) || Pin.Length < 4)
        {
            ErrorText = "PIN должен содержать не менее 4 символов.";
            return;
        }

        await ExecuteAsync(async () =>
        {
            if (IsSettingPin)
            {
                if (Pin != ConfirmPin)
                {
                    ErrorText = "PIN-коды не совпадают.";
                    return;
                }

                _userStore.CreatePin(Pin);
            }
            else
            {
                if (!_userStore.VerifyPin(Pin))
                {
                    ErrorText = "Неверный PIN-код. Попробуйте ещё раз.";
                    Pin = string.Empty;
                    ConfirmPin = string.Empty;
                    return;
                }
            }

            var key = _userStore.DeriveKey(Pin);
            Pin = string.Empty;
            ConfirmPin = string.Empty;

            // Ensure LocalDB schema exists
            await _db.EnsureSchemaAsync();

            // Set up personal session
            _session.SetPersonalSession(_userStore.UserId, key);

            _appVm.ShowShell();
        });

        if (!IsBusy && ErrorMessage is not null && ErrorText is null)
            ErrorText = ErrorMessage;
    }

    [RelayCommand]
    private void ResetPin()
    {
        _userStore.ResetPin();
        OnPropertyChanged(nameof(IsSettingPin));
        OnPropertyChanged(nameof(Title));
        Pin = string.Empty;
        ConfirmPin = string.Empty;
        ErrorText = null;
    }
}
