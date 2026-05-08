using ConSecOrg.Client.Infrastructure.Local;
using ConSecOrg.Client.Services;
using ConSecOrg.Infrastructure.Crypto;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Input;
using WpfApp = System.Windows.Application;

namespace ConSecOrg.Client.Views.Dialogs;

public partial class PinPromptDialog : Window
{
    private readonly LocalUserStore _userStore;
    private readonly UserSettingsService _userSettings;
    private readonly ModeService _modeService;
    private readonly KdfService _kdf = new();

    public PinPromptDialog(LocalUserStore userStore, UserSettingsService userSettings, ModeService modeService,
                           string title, string subtitle)
    {
        InitializeComponent();
        _userStore = userStore;
        _userSettings = userSettings;
        _modeService = modeService;
        TitleText.Text = title;
        SubtitleText.Text = subtitle;
        Loaded += (_, _) => PinBox.Focus();
    }

    private void ConfirmBtn_Click(object sender, RoutedEventArgs e) => TryConfirm();

    private void PinBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { TryConfirm(); e.Handled = true; }
    }

    private void TryConfirm()
    {
        var pin = PinBox.Password;
        if (string.IsNullOrEmpty(pin))
        {
            ErrorText.Text = "Введите PIN-код.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        bool ok;
        if (_modeService.IsPersonal)
        {
            ok = _userStore.VerifyPin(pin);
        }
        else
        {
            // Корпоративный — PIN хранится в UserSettings
            ok = VerifyCorporatePin(pin);
        }

        if (!ok)
        {
            ErrorText.Text = "Неверный PIN-код. Попробуйте ещё раз.";
            ErrorText.Visibility = Visibility.Visible;
            PinBox.Clear();
            PinBox.Focus();
            return;
        }

        DialogResult = true;
        Close();
    }

    private bool VerifyCorporatePin(string pin)
    {
        var s = _userSettings.Current;
        if (string.IsNullOrEmpty(s.NotesPinSalt) || string.IsNullOrEmpty(s.NotesPinVerifier))
            return false;

        var salt = Convert.FromBase64String(s.NotesPinSalt);
        var derived = _kdf.Derive(pin, salt);
        var verifier = new byte[32];
        Buffer.BlockCopy(derived, 0, verifier, 0, 32);
        Array.Clear(derived, 0, derived.Length);

        var stored = Convert.FromBase64String(s.NotesPinVerifier);
        return CryptographicOperations.FixedTimeEquals(verifier, stored);
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    /// <summary>Запрос PIN. Возвращает true если PIN верный.</summary>
    public static bool Prompt(LocalUserStore userStore, UserSettingsService userSettings, ModeService modeService,
                               string title, string subtitle)
    {
        var dlg = new PinPromptDialog(userStore, userSettings, modeService, title, subtitle)
        {
            Owner = WpfApp.Current.MainWindow
        };
        return dlg.ShowDialog() == true;
    }
}
