using ConSecOrg.Client.Infrastructure.Api;
using ConSecOrg.Client.Infrastructure.Local;
using ConSecOrg.Client.Services;
using ConSecOrg.Client.Themes;
using ConSecOrg.Client.ViewModels;
using ConSecOrg.Client.ViewModels.Audit;
using ConSecOrg.Client.ViewModels.Calendar;
using ConSecOrg.Client.ViewModels.Contacts;
using ConSecOrg.Client.ViewModels.Dashboard;
using ConSecOrg.Client.ViewModels.Company;
using ConSecOrg.Client.ViewModels.Notes;
using ConSecOrg.Client.ViewModels.Reports;
using ConSecOrg.Client.ViewModels.Settings;
using ConSecOrg.Client.ViewModels.Shell;
using ConSecOrg.Client.ViewModels.Startup;
using ConSecOrg.Client.ViewModels.Tasks;
using ConSecOrg.Client.ViewModels.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Windows;
using WpfApp = System.Windows.Application;

namespace ConSecOrg.Client;

public partial class App : WpfApp
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, ex) =>
        {
            LogCrash(ex.Exception);
            MessageBox.Show($"Ошибка приложения:\n{ex.Exception.Message}\n\nПодробности в %AppData%\\ConSecOrg\\crash.log",
                "ConSecOrg — Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
        {
            if (ex.ExceptionObject is Exception e2) LogCrash(e2);
        };

        TaskScheduler.UnobservedTaskException += (_, ex) =>
        {
            LogCrash(ex.Exception);
            ex.SetObserved();
        };

        _host = Host.CreateDefaultBuilder(e.Args)
            .ConfigureServices(ConfigureServices)
            .Build();

        await _host.StartAsync();

        ThemeEngine.Instance.Initialize(this);

        var appVm = _host.Services.GetRequiredService<AppViewModel>();
        appVm.ShowModeSelection();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.DataContext = appVm;
        mainWindow.Show();
        MainWindow = mainWindow;
    }

    private static void ConfigureServices(HostBuilderContext _, IServiceCollection services)
    {
        // Core services
        services.AddSingleton<ModeService>();
        services.AddSingleton<SessionService>();
        services.AddSingleton<NavigationService>();
        services.AddSingleton<NotificationService>();
        services.AddSingleton<ProjectService>();
        services.AddSingleton<NoteMetaService>();
        services.AddSingleton<TaskMetaService>();
        services.AddSingleton<UserSettingsService>();
        services.AddSingleton<ChatService>();
        services.AddSingleton<ChatPanelViewModel>();
        services.AddSingleton<SharedProjectsHubClient>();
        services.AddSingleton<BoardHubClient>();
        services.AddHostedService<DestructionTimerService>();

        // Personal mode infrastructure
        services.AddSingleton<LocalUserStore>();
        services.AddSingleton<PersonalDbContext>();
        services.AddSingleton<LocalNotesService>();
        services.AddSingleton<LocalTasksService>();

        // API client (corporate mode)
        services.AddSingleton<ApiClient>();
        services.AddSingleton<IAuthApiService>(sp => sp.GetRequiredService<ApiClient>());
        services.AddSingleton<IContactsApiService>(sp => sp.GetRequiredService<ApiClient>());
        services.AddSingleton<IAuditApiService>(sp => sp.GetRequiredService<ApiClient>());
        services.AddSingleton<IDashboardApiService>(sp => sp.GetRequiredService<ApiClient>());
        services.AddSingleton<IUserSearchApiService>(sp => sp.GetRequiredService<ApiClient>());
        services.AddSingleton<ISharedProjectsApiService>(sp => sp.GetRequiredService<ApiClient>());
        services.AddSingleton<IChatApiService>(sp => sp.GetRequiredService<ApiClient>());
        services.AddSingleton<IUsersManagementApiService>(sp => sp.GetRequiredService<ApiClient>());

        // Proxies: delegate to API or local depending on ModeService
        services.AddSingleton<INotesApiService>(sp => new NotesServiceProxy(
            sp.GetRequiredService<ModeService>(),
            sp.GetRequiredService<ApiClient>(),
            sp.GetRequiredService<LocalNotesService>()));

        services.AddSingleton<ITasksApiService>(sp => new TasksServiceProxy(
            sp.GetRequiredService<ModeService>(),
            sp.GetRequiredService<ApiClient>(),
            sp.GetRequiredService<LocalTasksService>()));

        // ViewModels
        services.AddSingleton<AppViewModel>();
        services.AddTransient<ModeSelectionViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<PinViewModel>();
        services.AddTransient<RegisterViewModel>();
        services.AddSingleton<ShellViewModel>();
        services.AddTransient<NotesListViewModel>();
        services.AddTransient<NoteEditorViewModel>();
        services.AddSingleton<TaskEditViewModel>();
        services.AddSingleton<SubTaskEditViewModel>();
        services.AddSingleton<TaskDetailViewModel>();
        services.AddTransient<KanbanBoardViewModel>();
        services.AddTransient<MyTasksViewModel>();
        services.AddTransient<CalendarViewModel>();
        services.AddTransient<ContactsViewModel>();
        services.AddTransient<SecurityDashboardViewModel>();
        services.AddTransient<AuditLogViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<ChangePasswordDialogViewModel>();
        services.AddTransient<ChangeEmailDialogViewModel>();
        services.AddTransient<SharedProjectsViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<CompanyViewModel>();
        services.AddTransient<UsersManagementViewModel>();
        services.AddSingleton<ConSecOrg.Client.ViewModels.Company.SharedChatPanelViewModel>();
        services.AddSingleton<ConSecOrg.Client.ViewModels.Company.SharedTaskEditViewModel>();
        services.AddTransient<ConSecOrg.Client.ViewModels.Company.SharedProjectBoardViewModel>();

        services.AddSingleton<MainWindow>();
    }

    public T? TryGetService<T>() where T : class =>
        _host?.Services.GetService<T>();

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(3));
            _host.Dispose();
        }
        base.OnExit(e);
    }

    private static void LogCrash(Exception ex)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ConSecOrg");
            Directory.CreateDirectory(dir);
            var log = Path.Combine(dir, "crash.log");
            File.AppendAllText(log, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
        }
        catch { }
    }
}
