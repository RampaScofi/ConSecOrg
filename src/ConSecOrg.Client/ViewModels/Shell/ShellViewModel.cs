using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Infrastructure.Api;
using ConSecOrg.Client.Services;
using ConSecOrg.Client.ViewModels.Audit;

using ConSecOrg.Client.ViewModels.Chats;
using ConSecOrg.Client.ViewModels.Company;
using SharedChatPanelVm = ConSecOrg.Client.ViewModels.Company.SharedChatPanelViewModel;
using ConSecOrg.Client.ViewModels.Users;
using ConSecOrg.Client.Views.Dialogs;
using ConSecOrg.Client.ViewModels.Base;
using ConSecOrg.Client.ViewModels.Calendar;
using ConSecOrg.Client.ViewModels.Contacts;
using ConSecOrg.Client.ViewModels.Dashboard;
using ConSecOrg.Client.ViewModels.Notes;
using ConSecOrg.Client.ViewModels.Reports;
using ConSecOrg.Client.ViewModels.Settings;
using ConSecOrg.Client.ViewModels.Tasks;
using ConSecOrg.Shared.DTOs.Projects;
using ConSecOrg.Shared.Enums;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace ConSecOrg.Client.ViewModels.Shell;

public partial class ShellViewModel(
    NavigationService navigation,
    SessionService session,
    AppViewModel appVm,
    ModeService modeService,
    ProjectService projectService,
    ISharedProjectsApiService sharedProjectsApi,
    IUserSearchApiService userSearchApi,
    ChatPanelViewModel chatPanel,
    UserSettingsService userSettings,
    SharedProjectsHubClient sharedHub,
    SharedChatPanelVm sharedChatPanel) : BaseViewModel
{
    private DispatcherTimer? _pendingContactsTimer;
    [ObservableProperty] private BasePageViewModel? _currentPage;
    [ObservableProperty] private bool _isSidebarExpanded = true;
    [ObservableProperty] private string _currentSection = "notes";
    [ObservableProperty] private int _pendingContactsCount;
    [ObservableProperty] private Guid? _selectedProjectId;
    [ObservableProperty] private bool _isProjectsExpanded = true;
    [ObservableProperty] private bool _showInlineProjectCreate;
    [ObservableProperty] private string _newProjectNameInline = string.Empty;
    [ObservableProperty] private ObservableCollection<SharedProjectDto> _sharedProjects = [];
    [ObservableProperty] private Guid? _selectedSharedProjectId;
    [ObservableProperty] private bool _isSharedProjectsExpanded = true;
    [ObservableProperty] private int _unreadChatsCount;
    [ObservableProperty] private bool _hasUnreadChats;

    public SessionService Session => session;
    public ModeService ModeService => modeService;
    public ProjectService ProjectService => projectService;
    public ChatPanelViewModel ChatPanel => chatPanel;
    public SharedChatPanelVm SharedChatPanel => sharedChatPanel;
    public ObservableCollection<ProjectItem> Projects => projectService.Projects;
    public bool IsCorporate => modeService.IsCorporate;

    public bool IsAdminOrAuditor =>
        session.CurrentUser?.Role is UserRoleDto.Admin or UserRoleDto.Auditor;

    public bool IsAdmin =>
        session.CurrentUser?.Role is UserRoleDto.Admin;

    public string SidebarDisplayName => !string.IsNullOrWhiteSpace(userSettings.Current.FullName)
        ? userSettings.Current.FullName
        : session.CurrentUser?.Username ?? "Пользователь";

    public string SidebarAvatarPath => userSettings.Current.AvatarImagePath ?? string.Empty;

    public bool SidebarHasAvatar =>
        !string.IsNullOrEmpty(userSettings.Current.AvatarImagePath) &&
        System.IO.File.Exists(userSettings.Current.AvatarImagePath);

    public bool CanSeeReports => modeService.IsCorporate;
    public bool CanSeeCompany => modeService.IsCorporate;
    public bool CanSeeUsers => modeService.IsCorporate && IsAdmin;

    // Hide FAB when panel is open or we're already on the full Chats page
    public bool ShowFloatingChatButton =>
        modeService.IsCorporate
        && userSettings.Current.ShowFloatingChatButton
        && !sharedChatPanel.IsOpen
        && CurrentSection != "chats";

    public void Initialize()
    {
        // When profile changes (avatar/FullName saved), refresh sidebar
        userSettings.Changed += () =>
        {
            OnPropertyChanged(nameof(SidebarDisplayName));
            OnPropertyChanged(nameof(SidebarAvatarPath));
            OnPropertyChanged(nameof(SidebarHasAvatar));
        };

        if (session.CurrentUser is not null)
        {
            projectService.LoadForUser(session.CurrentUser.Id);
            // Загружаем сохранённые настройки темы и фона для текущего пользователя
            var s = userSettings.LoadFor(session.CurrentUser.Id);
            Themes.ThemeEngine.Instance.ApplyFromSettings(
                s.Theme, s.AccentHex, s.FontSize,
                string.IsNullOrEmpty(s.BackgroundImagePath) ? null : s.BackgroundImagePath,
                s.BackgroundOverlayOpacity);
        }

        navigation.NavigationChanged += () =>
        {
            CurrentPage = navigation.CurrentPage;
            CurrentSection = CurrentPage switch
            {
                NotesListViewModel or NoteEditorViewModel => "notes",
                KanbanBoardViewModel => "tasks",
                MyTasksViewModel => "mytasks",
                CalendarViewModel => "calendar",
                ContactsViewModel => "contacts",
                ChatsViewModel => "chats",
                SecurityDashboardViewModel => "dashboard",
                AuditLogViewModel => "audit",
                ReportsViewModel => "reports",
                CompanyViewModel => "company",
                UsersManagementViewModel => "users",
                SettingsViewModel => "settings",
                _ => CurrentSection
            };
        };

        if (modeService.IsCorporate)
        {
            _ = LoadSharedProjectsAsync();
            _ = LoadPendingContactsCountAsync();
            _pendingContactsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _pendingContactsTimer.Tick += async (_, _) => await LoadPendingContactsCountAsync();
            _pendingContactsTimer.Start();

            sharedHub.ChatMessageReceived += OnHubChatMessageReceived;
            // Refresh FAB visibility when sliding chat panel opens/closes
            sharedChatPanel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SharedChatPanelVm.IsOpen))
                    OnPropertyChanged(nameof(ShowFloatingChatButton));
            };
        }

        userSettings.Changed += () => OnPropertyChanged(nameof(ShowFloatingChatButton));

        NavigateNotes();
    }

    private async Task LoadSharedProjectsAsync()
    {
        try
        {
            var list = await sharedProjectsApi.GetMyProjectsAsync();
            SharedProjects = new ObservableCollection<SharedProjectDto>(list);
        }
        catch { }
    }

    private async Task LoadPendingContactsCountAsync()
    {
        try
        {
            var pending = await userSearchApi.GetPendingRequestsAsync();
            PendingContactsCount = pending.Count;
        }
        catch { }
    }

    public bool HasPendingContacts => PendingContactsCount > 0;

    partial void OnPendingContactsCountChanged(int value)
        => OnPropertyChanged(nameof(HasPendingContacts));

    partial void OnUnreadChatsCountChanged(int value)
        => HasUnreadChats = value > 0;

    partial void OnCurrentSectionChanged(string value)
        => OnPropertyChanged(nameof(ShowFloatingChatButton));

    private void OnHubChatMessageReceived(ConSecOrg.Shared.DTOs.Projects.ChatMessageDto msg)
    {
        // Only count as unread if we're not currently on the chats page
        if (CurrentSection == "chats") return;
        var myId = session.CurrentUser?.Id;
        if (msg.SenderUserId == myId) return; // own message echo
        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            UnreadChatsCount++;
        });
    }

    [RelayCommand]
    private void OpenChatFab()
    {
        sharedChatPanel.OpenChatList();
        UnreadChatsCount = 0;
    }

    [RelayCommand]
    private void NavigateChats()
    {
        navigation.NavigateTo<ChatsViewModel>();
        UnreadChatsCount = 0;
    }
    [RelayCommand] private void NavigateNotes() => navigation.NavigateTo<NotesListViewModel>();
    [RelayCommand] private void NavigateTasks() => navigation.NavigateTo<KanbanBoardViewModel>();
    [RelayCommand] private void NavigateMyTasks() => navigation.NavigateTo<MyTasksViewModel>();
    [RelayCommand] private void NavigateCalendar() => navigation.NavigateTo<CalendarViewModel>();
    [RelayCommand] private void NavigateContacts() => navigation.NavigateTo<ContactsViewModel>();
    [RelayCommand] private void NavigateDashboard() => navigation.NavigateTo<SecurityDashboardViewModel>();
    [RelayCommand] private void NavigateAudit() => navigation.NavigateTo<AuditLogViewModel>();
    [RelayCommand] private void NavigateSettings() => navigation.NavigateTo<SettingsViewModel>();
    [RelayCommand] private void NavigateReports() => navigation.NavigateTo<ReportsViewModel>();
    [RelayCommand] private void NavigateCompany()
    {
        _ = LoadSharedProjectsAsync();
        navigation.NavigateTo<CompanyViewModel>();
    }
    [RelayCommand] private void NavigateUsers() => navigation.NavigateTo<UsersManagementViewModel>();

    [RelayCommand]
    private void NavigateProject(ProjectItem? project)
    {
        SelectedProjectId = project?.Id;
        SelectedSharedProjectId = null;
        navigation.NavigateTo<KanbanBoardViewModel>(vm =>
        {
            vm.SelectedProjectId = project?.Id;
            vm.SharedProjectId = null;
        });
    }

    [RelayCommand]
    private void NavigateSharedProject(SharedProjectDto? project)
    {
        if (project is null) return;
        SelectedSharedProjectId = project.Id;
        SelectedProjectId = null;
        navigation.NavigateTo<ConSecOrg.Client.ViewModels.Company.SharedProjectBoardViewModel>(vm =>
        {
            _ = vm.OpenAsync(project);
        });
    }

    [RelayCommand]
    private void OpenProjectChat(SharedProjectDto project)
    {
        chatPanel.OpenForProject(project.Id.ToString(), project.Name);
    }

    [RelayCommand]
    private void CreateProjectFromSidebar()
        => ShowInlineProjectCreate = !ShowInlineProjectCreate;

    [RelayCommand]
    private void ConfirmCreateProject()
    {
        if (string.IsNullOrWhiteSpace(NewProjectNameInline)) return;
        projectService.CreateProject(NewProjectNameInline.Trim(),
            "#" + System.Random.Shared.Next(0x1000000).ToString("X6"));
        NewProjectNameInline = string.Empty;
        ShowInlineProjectCreate = false;
    }

    [RelayCommand]
    private void CancelCreateProject()
    {
        NewProjectNameInline = string.Empty;
        ShowInlineProjectCreate = false;
    }

    [RelayCommand]
    private void DeleteProject(ProjectItem project)
    {
        if (!ConfirmDialog.Show("Удалить проект", $"Удалить проект «{project.Name}»?\nВсе задачи проекта потеряют привязку.")) return;

        projectService.DeleteProject(project.Id);
        if (SelectedProjectId == project.Id)
        {
            SelectedProjectId = null;
            NavigateTasks();
        }
    }

    [RelayCommand]
    private void DeleteBoard(BoardItem board)
    {
        if (!ConfirmDialog.Show("Удалить доску", $"Удалить доску «{board.Name}»?\nЗадачи доски потеряют привязку.")) return;

        projectService.DeleteBoard(board.Id);
        navigation.NavigateTo<KanbanBoardViewModel>(vm =>
        {
            vm.SelectedProjectId = SelectedProjectId;
            vm.SelectedBoardId = null;
        });
    }

    [RelayCommand]
    private void NavigateBoard(BoardItem board)
    {
        var projectId = projectService.GetProjectForBoard(board.Id);
        SelectedProjectId = projectId;
        SelectedSharedProjectId = null;
        navigation.NavigateTo<KanbanBoardViewModel>(vm =>
        {
            vm.SelectedProjectId = projectId;
            vm.SelectedBoardId = board.Id;
            vm.SharedProjectId = null;
        });
    }

    [RelayCommand]
    private void Logout()
    {
        session.Clear();
        navigation.Clear();
        if (modeService.IsCorporate)
            appVm.ShowLogin();
        else
            appVm.ShowPin();
    }

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarExpanded = !IsSidebarExpanded;

    [RelayCommand]
    private void ToggleProjects() => IsProjectsExpanded = !IsProjectsExpanded;

    [RelayCommand]
    private void ToggleSharedProjects() => IsSharedProjectsExpanded = !IsSharedProjectsExpanded;
}
