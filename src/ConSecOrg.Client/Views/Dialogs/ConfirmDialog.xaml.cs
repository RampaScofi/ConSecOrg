using System.Windows;
using WpfApp = System.Windows.Application;

namespace ConSecOrg.Client.Views.Dialogs;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog(string title, string message, string confirmText = "Удалить", string cancelText = "Отмена")
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmBtn.Content = confirmText;
        CancelBtn.Content = cancelText;
    }

    private void ConfirmBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    public static bool Show(string title, string message,
        string confirmText = "Удалить", string cancelText = "Отмена")
    {
        var dlg = new ConfirmDialog(title, message, confirmText, cancelText)
        {
            Owner = WpfApp.Current.MainWindow
        };
        return dlg.ShowDialog() == true;
    }
}
