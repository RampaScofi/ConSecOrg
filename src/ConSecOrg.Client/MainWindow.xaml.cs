using ConSecOrg.Client.Controls;
using ConSecOrg.Client.Services;
using ConSecOrg.Client.ViewModels;
using WpfApp = System.Windows.Application;
using System.Windows;

namespace ConSecOrg.Client;

public partial class MainWindow : Window
{
    private bool _forceClose;

    public MainWindow()
    {
        InitializeComponent();
        WireNotifications();
    }

    private void WireNotifications()
    {
        if (WpfApp.Current is not App app) return;
        var svc = app.TryGetService<NotificationService>();
        if (svc is null) return;
        svc.NotificationRequested += (title, msg, level) =>
            ToastNotificationHelper.Show(ToastOverlay, title, msg, level);
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_forceClose) return;
        e.Cancel = true;
        Hide();
    }

    private void TrayIcon_TrayLeftMouseDown(object sender, RoutedEventArgs e) => RestoreWindow();

    private void TrayMenu_Open(object sender, RoutedEventArgs e) => RestoreWindow();

    private void TrayMenu_Lock(object sender, RoutedEventArgs e)
    {
        if (DataContext is not AppViewModel appVm) return;
        if (WpfApp.Current is not App app) return;

        var session = app.TryGetService<SessionService>();
        var nav = app.TryGetService<NavigationService>();
        var mode = app.TryGetService<ModeService>();

        if (session is not null && nav is not null)
        {
            session.Clear();
            nav.Clear();
            if (mode?.IsCorporate == true)
                appVm.ShowLogin();
            else
                appVm.ShowPin();
        }

        RestoreWindow();
    }

    private void TrayMenu_Exit(object sender, RoutedEventArgs e)
    {
        _forceClose = true;
        TrayIcon.Dispose();
        WpfApp.Current.Shutdown();
    }

    private void RestoreWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }
}
