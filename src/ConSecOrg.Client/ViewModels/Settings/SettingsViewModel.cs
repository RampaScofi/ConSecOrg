using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Infrastructure.Api;
using ConSecOrg.Client.Infrastructure.Local;
using ConSecOrg.Client.Services;
using ConSecOrg.Client.Themes;
using ConSecOrg.Client.ViewModels.Base;
using ConSecOrg.Client.Views.Settings;
using ConSecOrg.Infrastructure.Crypto;
using ConSecOrg.Shared.DTOs.Auth;
using Microsoft.Win32;
using System.Security.Cryptography;
using System.Text;
using WpfApp = System.Windows.Application;

namespace ConSecOrg.Client.ViewModels.Settings;

public partial class SettingsViewModel : BasePageViewModel
{
    private readonly SessionService _sessionService;
    private readonly ModeService _modeService;
    private readonly AppViewModel _appViewModel;
    private readonly IAuthApiService _authService;
    private readonly LocalUserStore _localUserStore;
    private readonly UserSettingsService _userSettings;
    private readonly ChangePasswordDialogViewModel _changePwdVm;
    private readonly ChangeEmailDialogViewModel _changeEmailVm;
    private readonly AvatarCacheService _avatarCache;
    private readonly IUserSearchApiService _userSearchApi;
    private readonly KdfService _kdf = new();

    public SettingsViewModel(SessionService sessionService, ModeService modeService, AppViewModel appViewModel,
        IAuthApiService authService, LocalUserStore localUserStore, UserSettingsService userSettings,
        ChangePasswordDialogViewModel changePwdVm, ChangeEmailDialogViewModel changeEmailVm,
        AvatarCacheService avatarCache, IUserSearchApiService userSearchApi)
    {
        _sessionService = sessionService;
        _modeService = modeService;
        _appViewModel = appViewModel;
        _authService = authService;
        _localUserStore = localUserStore;
        _userSettings = userSettings;
        _changePwdVm = changePwdVm;
        _changeEmailVm = changeEmailVm;
        _avatarCache = avatarCache;
        _userSearchApi = userSearchApi;

        var s = _userSettings.Current;
        _selectedTheme = string.IsNullOrEmpty(s.Theme) ? ThemeEngine.Instance.CurrentTheme : s.Theme;
        _accentHex = string.IsNullOrEmpty(s.AccentHex) ? ColorToHex(ThemeEngine.Instance.AccentColor) : s.AccentHex;
        _secondaryAccentHex = string.IsNullOrEmpty(s.SecondaryAccentHex) ? ColorToHex(ThemeEngine.Instance.SecondaryAccentColor) : s.SecondaryAccentHex;
        _fontSize = s.FontSize > 0 ? s.FontSize : ThemeEngine.Instance.FontSize;
        _backgroundImagePath = s.BackgroundImagePath ?? string.Empty;
        _backgroundOverlayOpacity = s.BackgroundOverlayOpacity > 0 ? s.BackgroundOverlayOpacity : ThemeEngine.Instance.BackgroundOverlayOpacity;
        _buttonForegroundHex = s.ButtonForegroundHex ?? string.Empty;
        _fullName = s.FullName ?? string.Empty;
        _avatarImagePath = s.AvatarImagePath ?? string.Empty;
        _showFloatingChatButton = s.ShowFloatingChatButton;
    }

    public override string Title => "Настройки";

    [ObservableProperty] private string _selectedTheme;
    [ObservableProperty] private string _accentHex;
    [ObservableProperty] private string _secondaryAccentHex;
    [ObservableProperty] private int _fontSize;
    [ObservableProperty] private string _backgroundImagePath;
    [ObservableProperty] private double _backgroundOverlayOpacity;
    [ObservableProperty] private string _buttonForegroundHex;
    [ObservableProperty] private string _fullName;
    [ObservableProperty] private string _avatarImagePath;
    [ObservableProperty] private bool _showFloatingChatButton;

    public bool IsButtonForegroundAuto => string.IsNullOrWhiteSpace(ButtonForegroundHex);
    public bool IsButtonForegroundWhite => ButtonForegroundHex == "#FFFFFF";
    public bool IsButtonForegroundBlack => ButtonForegroundHex == "#1A1C30";

    public bool HasAvatar => !string.IsNullOrEmpty(AvatarImagePath) && System.IO.File.Exists(AvatarImagePath);

    public string DisplayName => !string.IsNullOrWhiteSpace(_userSettings.Current.FullName)
        ? _userSettings.Current.FullName
        : _sessionService.CurrentUser?.Username ?? "Пользователь";

    public string Username => _sessionService.CurrentUser?.Username ?? string.Empty;
    public string ModeName => _modeService.IsCorporate ? "Корпоративный режим" : "Персональный режим";
    public bool IsCorporate => _modeService.IsCorporate;
    public bool IsPersonal => _modeService.IsPersonal;

    /// <summary>True если у пользователя уже задан PIN для защиты заметок.</summary>
    public bool HasNotesPin
    {
        get
        {
            if (_modeService.IsPersonal) return _localUserStore.HasPin;
            return !string.IsNullOrEmpty(_userSettings.Current.NotesPinVerifier);
        }
    }

    public string NotesPinStatus => HasNotesPin
        ? "PIN установлен. Используется для защиты заметок уровня «Конфиденциальный» и «Секретный»."
        : "PIN не задан. Установите его, чтобы защитить конфиденциальные заметки от просмотра.";

    private static string ColorToHex(System.Windows.Media.Color c)
        => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    public string[] Themes { get; } = ["Dark", "Light"];

    public string[] MaterialAccents { get; } =
    [
        "#F44336", "#E91E63", "#9C27B0", "#673AB7",
        "#3F51B5", "#2196F3", "#03A9F4", "#00BCD4",
        "#009688", "#4CAF50", "#8BC34A", "#CDDC39",
        "#FFC107", "#FF9800", "#FF5722", "#607D8B"
    ];

    public bool HasBackground => !string.IsNullOrEmpty(BackgroundImagePath);

    [RelayCommand]
    private void SetTheme(string theme)
    {
        SelectedTheme = theme;
        ApplyAndSaveTheme();
    }

    [RelayCommand]
    private void ApplyTheme() => ApplyAndSaveTheme();

    private void ApplyAndSaveTheme()
    {
        ThemeEngine.Instance.ApplyFromSettings(
            SelectedTheme, AccentHex, FontSize,
            HasBackground ? BackgroundImagePath : null,
            BackgroundOverlayOpacity,
            SecondaryAccentHex,
            ButtonForegroundHex);

        _userSettings.UpdateTheme(SelectedTheme, AccentHex, FontSize,
                                   BackgroundImagePath, BackgroundOverlayOpacity,
                                   SecondaryAccentHex, ButtonForegroundHex);
    }

    [RelayCommand]
    private void SelectAccent(string hex)
    {
        AccentHex = hex;
        ApplyAndSaveTheme();
    }

    [RelayCommand]
    private void SelectSecondaryAccent(string hex)
    {
        SecondaryAccentHex = hex;
        ApplyAndSaveTheme();
    }

    [RelayCommand]
    private void SetButtonForeground(string preset)
    {
        ButtonForegroundHex = preset switch
        {
            "auto"  => string.Empty,
            "white" => "#FFFFFF",
            "black" => "#1A1C30",
            _       => preset  // custom hex passed directly
        };
        NotifyButtonForegroundPresets();
        ApplyAndSaveTheme();
    }

    [RelayCommand]
    private void ApplyButtonForeground()
    {
        NotifyButtonForegroundPresets();
        ApplyAndSaveTheme();
    }

    private void NotifyButtonForegroundPresets()
    {
        OnPropertyChanged(nameof(IsButtonForegroundAuto));
        OnPropertyChanged(nameof(IsButtonForegroundWhite));
        OnPropertyChanged(nameof(IsButtonForegroundBlack));
    }

    partial void OnButtonForegroundHexChanged(string value) => NotifyButtonForegroundPresets();

    [RelayCommand]
    private void BrowseBackground()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Выберите фоновое изображение",
            Filter = "Изображения|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp|Все файлы|*.*",
            CheckFileExists = true
        };
        if (dlg.ShowDialog() == true)
        {
            // Копируем выбранное изображение в папку пользователя для надёжного хранения
            BackgroundImagePath = _userSettings.CopyBackgroundImage(dlg.FileName);
            OnPropertyChanged(nameof(HasBackground));
            ApplyAndSaveTheme();
        }
    }

    [RelayCommand]
    private void ClearBackground()
    {
        BackgroundImagePath = string.Empty;
        OnPropertyChanged(nameof(HasBackground));
        ApplyAndSaveTheme();
    }

    [RelayCommand]
    private async Task BrowseAvatarAsync()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Выберите фото профиля",
            Filter = "Изображения|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp|Все файлы|*.*",
            CheckFileExists = true
        };
        if (dlg.ShowDialog() != true) return;

        AvatarImagePath = _userSettings.CopyAvatarImage(dlg.FileName);
        OnPropertyChanged(nameof(HasAvatar));
        _userSettings.UpdateProfile(FullName, AvatarImagePath);
        NotifyProfileChanged();
        await UploadAvatarToServerAsync(AvatarImagePath);
    }

    [RelayCommand]
    private async Task ClearAvatarAsync()
    {
        AvatarImagePath = string.Empty;
        OnPropertyChanged(nameof(HasAvatar));
        _userSettings.UpdateProfile(FullName, AvatarImagePath);
        NotifyProfileChanged();
        await UploadAvatarToServerAsync(null);
    }

    [RelayCommand]
    private void SaveProfile()
    {
        _userSettings.UpdateProfile(FullName, AvatarImagePath);
        NotifyProfileChanged();
    }

    public override async Task OnNavigatedToAsync()
    {
        // Если у пользователя есть локальная аватарка, но на сервере её ещё нет — загружаем автоматически
        if (_modeService.IsCorporate && HasAvatar)
        {
            var userId = _sessionService.CurrentUser?.Id;
            if (userId.HasValue)
            {
                var serverAvatar = await _avatarCache.GetAsync(userId.Value);
                if (string.IsNullOrEmpty(serverAvatar))
                    await UploadAvatarToServerAsync(AvatarImagePath);
            }
        }
    }

    private async Task UploadAvatarToServerAsync(string? imagePath)
    {
        if (_modeService.IsPersonal) return;
        var userId = _sessionService.CurrentUser?.Id;
        if (userId is null) return;
        try
        {
            string? base64 = null;
            if (!string.IsNullOrEmpty(imagePath) && System.IO.File.Exists(imagePath))
            {
                var bytes = await System.IO.File.ReadAllBytesAsync(imagePath);
                // Ограничение 512 КБ — достаточно для аватарок 128x128
                if (bytes.Length <= 512 * 1024)
                    base64 = Convert.ToBase64String(bytes);
            }
            await _userSearchApi.UploadAvatarAsync(userId.Value, base64);
            _avatarCache.Update(userId.Value, base64);
        }
        catch { /* не критично — аватарка сохранена локально */ }
    }

    private void NotifyProfileChanged()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(HasAvatar));
    }

    [RelayCommand]
    private void ChangePassword()
    {
        var dlg = new ChangePasswordDialog(_changePwdVm) { Owner = WpfApp.Current.MainWindow };
        dlg.ShowDialog();
    }

    [RelayCommand]
    private void ChangeEmail()
    {
        var dlg = new ChangeEmailDialog(_changeEmailVm) { Owner = WpfApp.Current.MainWindow };
        dlg.ShowDialog();
    }

    /// <summary>Установить или сменить PIN для защиты заметок.
    /// Корпоративный режим — требует мастер-пароль. Персональный — текущий PIN.</summary>
    [RelayCommand]
    private async Task ChangeNotesPin()
    {
        var dlg = new Views.Settings.NotesPinDialog(_modeService.IsCorporate, HasNotesPin)
        {
            Owner = WpfApp.Current.MainWindow
        };
        if (dlg.ShowDialog() != true) return;

        // Подтверждение
        try
        {
            if (_modeService.IsCorporate)
            {
                var username = _sessionService.CurrentUser?.Username ?? string.Empty;
                await _authService.LoginAsync(new LoginRequestDto
                {
                    Username = username,
                    Password = dlg.ConfirmationValue
                });
            }
            else
            {
                if (HasNotesPin && !_localUserStore.VerifyPin(dlg.ConfirmationValue))
                {
                    System.Windows.MessageBox.Show("Неверный текущий PIN-код.",
                        "Ошибка", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
            }
        }
        catch
        {
            System.Windows.MessageBox.Show("Не удалось подтвердить личность. Проверьте ввод.",
                "Ошибка", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        // Сохраняем новый PIN
        if (_modeService.IsCorporate)
        {
            // В корпоративном режиме храним PIN в UserSettings (раздельно для каждого пользователя)
            var salt = new byte[32];
            RandomNumberGenerator.Fill(salt);
            var derived = _kdf.Derive(dlg.NewPin, salt);
            var verifier = new byte[32];
            Buffer.BlockCopy(derived, 0, verifier, 0, 32);
            Array.Clear(derived, 0, derived.Length);

            _userSettings.Current.NotesPinSalt = Convert.ToBase64String(salt);
            _userSettings.Current.NotesPinVerifier = Convert.ToBase64String(verifier);
            _userSettings.Save();
        }
        else
        {
            _localUserStore.CreatePin(dlg.NewPin);
        }

        OnPropertyChanged(nameof(HasNotesPin));
        OnPropertyChanged(nameof(NotesPinStatus));
        System.Windows.MessageBox.Show("PIN-код успешно " + (HasNotesPin ? "обновлён" : "установлен") + ".",
            "Готово", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    [RelayCommand]
    private void Logout()
    {
        _userSettings.Clear();
        _sessionService.Clear();
        if (_modeService.IsCorporate)
            _appViewModel.ShowLogin();
        else
            _appViewModel.ShowPin();
    }

    partial void OnFontSizeChanged(int value) => ApplyAndSaveTheme();

    partial void OnBackgroundOverlayOpacityChanged(double value) => ApplyAndSaveTheme();

    partial void OnShowFloatingChatButtonChanged(bool value)
    {
        _userSettings.Current.ShowFloatingChatButton = value;
        _userSettings.Save();
    }
}
