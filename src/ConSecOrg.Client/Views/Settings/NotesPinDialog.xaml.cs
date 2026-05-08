using System.Windows;

namespace ConSecOrg.Client.Views.Settings;

public partial class NotesPinDialog : Window
{
    private readonly bool _isCorporate;
    private readonly bool _hasExisting;

    /// <summary>Введённое значение для подтверждения (мастер-пароль или текущий PIN).</summary>
    public string ConfirmationValue => ConfirmationBox.Password;

    /// <summary>Новый PIN-код, заданный пользователем.</summary>
    public string NewPin => NewPinBox.Password;

    public NotesPinDialog(bool isCorporate, bool hasExisting)
    {
        InitializeComponent();
        _isCorporate = isCorporate;
        _hasExisting = hasExisting;

        TitleText.Text = hasExisting ? "Сменить PIN заметок" : "Установить PIN заметок";

        if (isCorporate)
        {
            ConfirmationLabel.Text = "Подтвердите ваш мастер-пароль";
            ConfirmationBox.Tag = "Мастер-пароль";
        }
        else if (hasExisting)
        {
            ConfirmationLabel.Text = "Введите текущий PIN";
        }
        else
        {
            // Персональный режим без PIN — confirmation не нужно (но в персональном PIN всегда есть)
            ConfirmationLabel.Visibility = Visibility.Collapsed;
            ConfirmationBox.Visibility = Visibility.Collapsed;
        }

        Loaded += (_, _) =>
        {
            if (ConfirmationBox.Visibility == Visibility.Visible)
                ConfirmationBox.Focus();
            else
                NewPinBox.Focus();
        };
    }

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        if (ConfirmationBox.Visibility == Visibility.Visible
            && string.IsNullOrEmpty(ConfirmationBox.Password))
        {
            ShowError(_isCorporate
                ? "Введите мастер-пароль для подтверждения."
                : "Введите текущий PIN для подтверждения.");
            return;
        }

        if (NewPinBox.Password.Length < 4)
        {
            ShowError("PIN-код должен содержать не менее 4 символов.");
            return;
        }

        if (NewPinBox.Password != ConfirmNewPinBox.Password)
        {
            ShowError("PIN-коды не совпадают.");
            return;
        }

        DialogResult = true;
        Close();
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowError(string text)
    {
        ErrorText.Text = text;
        ErrorText.Visibility = Visibility.Visible;
    }
}
