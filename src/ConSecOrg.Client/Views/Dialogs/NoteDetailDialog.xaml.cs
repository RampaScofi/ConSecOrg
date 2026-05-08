using ConSecOrg.Client.Infrastructure.Local;
using ConSecOrg.Client.Services;
using ConSecOrg.Shared.DTOs.Notes;
using ConSecOrg.Shared.Enums;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WpfApp = System.Windows.Application;

namespace ConSecOrg.Client.Views.Dialogs;

public partial class NoteDetailDialog : Window
{
    private readonly NoteDto _note;
    private readonly LocalUserStore _userStore;
    private readonly UserSettingsService _userSettings;
    private readonly ModeService _modeService;
    private bool _unlocked;

    public bool RequestedEdit { get; private set; }

    public NoteDetailDialog(NoteDto note, LocalUserStore userStore,
                            UserSettingsService userSettings, ModeService modeService)
    {
        InitializeComponent();
        _note = note;
        _userStore = userStore;
        _userSettings = userSettings;
        _modeService = modeService;
        Render();
    }

    private void Render()
    {
        TitleText.Text = _note.Title;
        CreatedText.Text = $"Изменена {_note.UpdatedAt.ToLocalTime():dd MMMM yyyy HH:mm}";

        var (color, label) = _note.SecurityLevel switch
        {
            SecurityLevelDto.Public       => (Color.FromRgb(0x4C, 0xAF, 0x50), "Публичный"),
            SecurityLevelDto.Internal     => (Color.FromRgb(0x21, 0x96, 0xF3), "Внутренний"),
            SecurityLevelDto.Confidential => (Color.FromRgb(0xFF, 0x98, 0x00), "Конфиденциальный"),
            SecurityLevelDto.Secret       => (Color.FromRgb(0xF4, 0x43, 0x36), "Секретный"),
            _ => (Color.FromRgb(0x80, 0x80, 0x80), _note.SecurityLevel.ToString())
        };
        var brush = new SolidColorBrush(color);
        LevelDot.Background = brush;
        LevelBadge.Background = brush;
        LevelBadgeText.Text = label;
        SubtitleText.Text = $"Уровень: {label.ToLower()}";

        if (_note.ExpiresAt.HasValue)
        {
            var expiresLocal = _note.ExpiresAt.Value.Kind == DateTimeKind.Utc
                ? _note.ExpiresAt.Value.ToLocalTime()
                : DateTime.SpecifyKind(_note.ExpiresAt.Value, DateTimeKind.Utc).ToLocalTime();
            TimerText.Text = $"Уничтожение: {expiresLocal:dd.MM.yyyy HH:mm}";
            TimerBadge.Visibility = Visibility.Visible;
        }

        bool needsPin = NeedsPin();
        if (needsPin && !_unlocked)
        {
            ContentView.Visibility = Visibility.Hidden;
            LockedOverlay.Visibility = Visibility.Visible;
            LockedReason.Text = _note.SecurityLevel == SecurityLevelDto.Secret
                ? "Эта заметка имеет уровень «Секретный». Для просмотра и редактирования содержимого требуется ввести PIN-код."
                : "Эта заметка имеет уровень «Конфиденциальный». Для просмотра и редактирования содержимого требуется ввести PIN-код.";
            EditBtn.IsEnabled = false;
            FooterHint.Text = "Содержимое скрыто до подтверждения PIN";
        }
        else
        {
            ContentView.Visibility = Visibility.Visible;
            LockedOverlay.Visibility = Visibility.Collapsed;
            ContentView.Markdown = _note.Content ?? string.Empty;
            EditBtn.IsEnabled = true;
            FooterHint.Text = string.Empty;
        }
    }

    private bool NeedsPin()
    {
        if (_note.SecurityLevel != SecurityLevelDto.Confidential
            && _note.SecurityLevel != SecurityLevelDto.Secret) return false;

        if (_modeService.IsPersonal) return _userStore.HasPin;
        return !string.IsNullOrEmpty(_userSettings.Current.NotesPinVerifier);
    }

    private void UnlockBtn_Click(object sender, RoutedEventArgs e)
    {
        var levelLabel = _note.SecurityLevel == SecurityLevelDto.Secret ? "секретной" : "конфиденциальной";
        if (PinPromptDialog.Prompt(_userStore, _userSettings, _modeService,
                "Подтвердите доступ",
                $"Введите PIN для просмотра {levelLabel} заметки"))
        {
            _unlocked = true;
            Render();
        }
    }

    private void EditBtn_Click(object sender, RoutedEventArgs e)
    {
        if (NeedsPin() && !_unlocked)
        {
            if (!PinPromptDialog.Prompt(_userStore, _userSettings, _modeService,
                    "Подтвердите доступ",
                    "Для редактирования заметки требуется PIN"))
                return;
            _unlocked = true;
        }

        RequestedEdit = true;
        DialogResult = true;
        Close();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
        base.OnKeyDown(e);
    }

    public static bool Show(NoteDto note, LocalUserStore userStore,
                            UserSettingsService userSettings, ModeService modeService)
    {
        var dlg = new NoteDetailDialog(note, userStore, userSettings, modeService)
        {
            Owner = WpfApp.Current.MainWindow
        };
        dlg.ShowDialog();
        return dlg.RequestedEdit;
    }
}
