using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Infrastructure.Api;
using ConSecOrg.Client.Services;
using ConSecOrg.Client.ViewModels.Base;
using ConSecOrg.Client.ViewModels.Shell;
using ConSecOrg.Client.Views.Dialogs;
using ConSecOrg.Shared.DTOs.Projects;
using System.Collections.ObjectModel;
using System.Windows;
using SysWin = System.Windows;

namespace ConSecOrg.Client.ViewModels.Company;

// ── SharedColumnViewModel ─────────────────────────────────────────────────────
public partial class SharedColumnViewModel : ObservableObject
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public int Order { get; set; }

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _color = "#607D8B";
    [ObservableProperty] private bool _isRenaming;
    [ObservableProperty] private string _renameText = string.Empty;
    [ObservableProperty] private bool _showColorPicker;
    [ObservableProperty] private string _newTaskTitle = string.Empty;

    public ObservableCollection<SharedTaskCardViewModel> Tasks { get; } = new();
    public int TaskCount => Tasks.Count;

    // Set by the board VM to persist color to the server
    public Action<string>? PersistColor { get; set; }

    public SharedColumnViewModel()
    {
        Tasks.CollectionChanged += (_, _) => OnPropertyChanged(nameof(TaskCount));
    }

    [RelayCommand]
    private void ToggleColorPicker() => ShowColorPicker = !ShowColorPicker;

    [RelayCommand]
    private void SetColor(string hex) => Color = hex;

    partial void OnColorChanged(string value)
    {
        ShowColorPicker = false;
        PersistColor?.Invoke(value);
    }

    public static SharedColumnViewModel FromDto(SharedProjectColumnDto dto)
    {
        var vm = new SharedColumnViewModel { Id = dto.Id, ProjectId = dto.ProjectId, Order = dto.Order };
        vm.Name = dto.Name;
        vm.Color = dto.Color;
        return vm;
    }
}

// ── SharedTaskCardViewModel ───────────────────────────────────────────────────
public partial class SharedTaskCardViewModel : ObservableObject
{
    private TaskMetaService? _metaService;

    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ColumnId { get; set; }

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string? _description;
    [ObservableProperty] private List<string> _tags = [];
    [ObservableProperty] private int _priority;
    [ObservableProperty] private DateTime? _dueDate;
    [ObservableProperty] private int _position;
    [ObservableProperty] private bool _isCompleted;
    [ObservableProperty] private Guid? _assignedUserId;
    [ObservableProperty] private string? _assignedUsername;

    // ── Subtasks ────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isSubTasksExpanded;
    [ObservableProperty] private ObservableCollection<SubTaskItem> _subTasks = [];
    [ObservableProperty] private string _newSubTaskTitle = string.Empty;

    public int SubTaskCount => SubTasks.Count;
    public int CompletedSubTaskCount => SubTasks.Count(s => s.IsCompleted);
    public bool HasSubTasks => SubTasks.Count > 0;
    public double SubTaskProgress => HasSubTasks ? (double)CompletedSubTaskCount / SubTaskCount : 0;
    public string SubTaskCountText => HasSubTasks ? $"{CompletedSubTaskCount}/{SubTasks.Count}" : string.Empty;

    // ── Assignee helpers ────────────────────────────────────────────────────
    public string AssigneeLetter =>
        !string.IsNullOrEmpty(AssignedUsername) ? AssignedUsername![0].ToString().ToUpper() : "?";
    public bool HasAssignee => AssignedUserId.HasValue;

    public string AssigneeColor
    {
        get
        {
            if (!AssignedUserId.HasValue) return "#9E9E9E";
            var bytes = AssignedUserId.Value.ToByteArray();
            string[] palette = { "#7C4DFF", "#43A047", "#E53935", "#FF9800", "#0097A7", "#1976D2", "#8E24AA", "#00897B" };
            return palette[Math.Abs(bytes[0]) % palette.Length];
        }
    }

    public string PriorityText => Priority switch { 3 => "Критично!", 2 => "Важно", 0 => "Не важно", _ => "Нормально" };
    public string PriorityColor => Priority switch { 3 => "#C62828", 2 => "#E65100", 0 => "#616161", _ => "#2E7D32" };
    public string PriorityBgColor => Priority switch { 3 => "#FFEBEE", 2 => "#FFF3E0", 0 => "#F5F5F5", _ => "#E8F5E9" };
    public bool ShowPriority => Priority != 1;

    partial void OnAssignedUsernameChanged(string? value)
    {
        OnPropertyChanged(nameof(AssigneeLetter));
        OnPropertyChanged(nameof(HasAssignee));
    }
    partial void OnAssignedUserIdChanged(Guid? value)
    {
        OnPropertyChanged(nameof(HasAssignee));
        OnPropertyChanged(nameof(AssigneeColor));
    }

    // ── Subtask commands ────────────────────────────────────────────────────

    [RelayCommand]
    private void ToggleSubTasksExpanded() => IsSubTasksExpanded = !IsSubTasksExpanded;

    [RelayCommand]
    private void ToggleSubTask(SubTaskItem sub)
    {
        _metaService?.ToggleSubTask(Id, sub.Id);
        FindParentOf(SubTasks, sub)?.RefreshChildStats();
        RefreshSubTaskCounts();
    }

    [RelayCommand]
    private void ToggleSubExpanded(SubTaskItem sub) => sub.IsExpanded = !sub.IsExpanded;

    [RelayCommand]
    private void AddTopSubTask()
    {
        if (_metaService is null || string.IsNullOrWhiteSpace(NewSubTaskTitle)) return;
        var sub = _metaService.AddSubTask(Id, NewSubTaskTitle.Trim());
        SubTasks.Add(sub);
        IsSubTasksExpanded = true;
        NewSubTaskTitle = string.Empty;
        RefreshSubTaskCounts();
    }

    [RelayCommand]
    private void AddChildSub(SubTaskItem parent)
    {
        if (_metaService is null || parent == null || string.IsNullOrWhiteSpace(parent.NewChildTitle)) return;
        _metaService.AddChildSubTask(Id, parent.Id, parent.NewChildTitle.Trim());
        parent.NewChildTitle = string.Empty;
        parent.IsExpanded = true;
        parent.RefreshChildStats();
    }

    [RelayCommand]
    private void RemoveSubTask(SubTaskItem sub)
    {
        if (_metaService is null || sub == null) return;
        _metaService.RemoveSubTask(Id, sub.Id);
        if (!SubTasks.Remove(sub))
        {
            var parentItem = FindParentOf(SubTasks, sub);
            parentItem?.Children.Remove(sub);
            parentItem?.RefreshChildStats();
        }
        RefreshSubTaskCounts();
    }

    [RelayCommand]
    private void SaveSubTitle(SubTaskItem sub)
    {
        if (sub == null) return;
        _metaService?.UpdateSubTask(Id, sub);
    }

    private static SubTaskItem? FindParentOf(IEnumerable<SubTaskItem> list, SubTaskItem target)
    {
        foreach (var item in list)
        {
            if (item.Children.Contains(target)) return item;
            var found = FindParentOf(item.Children, target);
            if (found != null) return found;
        }
        return null;
    }

    private void RefreshSubTaskCounts()
    {
        OnPropertyChanged(nameof(SubTaskCount));
        OnPropertyChanged(nameof(CompletedSubTaskCount));
        OnPropertyChanged(nameof(HasSubTasks));
        OnPropertyChanged(nameof(SubTaskCountText));
        OnPropertyChanged(nameof(SubTaskProgress));
    }

    // ── Factory ─────────────────────────────────────────────────────────────

    public static SharedTaskCardViewModel FromDto(SharedProjectTaskDto d, TaskMetaService? metaService = null)
    {
        var vm = new SharedTaskCardViewModel { Id = d.Id, ProjectId = d.ProjectId, ColumnId = d.ColumnId };
        vm.Title = d.Title;
        vm.Description = d.Description;
        vm.Tags = d.Tags ?? [];
        vm.Priority = d.Priority;
        vm.DueDate = d.DueDate;
        vm.IsCompleted = d.IsCompleted;
        vm.Position = d.Position;
        vm.AssignedUserId = d.AssignedUserId;
        vm.AssignedUsername = d.AssignedUsername;
        if (metaService is not null)
            vm.InitMeta(metaService);
        return vm;
    }

    public void InitMeta(TaskMetaService metaService)
    {
        _metaService = metaService;
        var meta = metaService.GetMeta(Id);
        SubTasks = new ObservableCollection<SubTaskItem>(meta.SubTasks);
        RefreshSubTaskCounts();
    }

    public void UpdateFromDto(SharedProjectTaskDto d)
    {
        ColumnId = d.ColumnId;
        Title = d.Title;
        Description = d.Description;
        Tags = d.Tags ?? [];
        Priority = d.Priority;
        DueDate = d.DueDate;
        IsCompleted = d.IsCompleted;
        Position = d.Position;
        AssignedUserId = d.AssignedUserId;
        AssignedUsername = d.AssignedUsername;
    }
}

// ── SharedProjectBoardViewModel ───────────────────────────────────────────────
public partial class SharedProjectBoardViewModel : BasePageViewModel
{
    private readonly ISharedProjectsApiService _api;
    private readonly SharedProjectsHubClient _hub;
    private readonly SharedChatPanelViewModel _chatPanel;
    private readonly SessionService _session;
    private readonly NotificationService _notifications;
    private readonly TaskMetaService _metaService;
    private readonly SharedTaskEditViewModel _taskEditVm;

    [ObservableProperty] private SharedProjectDto? _project;
    [ObservableProperty] private string? _statusText;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _newColumnName = string.Empty;
    [ObservableProperty] private bool _showAddColumnDialog;

    public ObservableCollection<SharedColumnViewModel> Columns { get; } = new();
    public SharedChatPanelViewModel ChatPanel => _chatPanel;

    public string MembersCountText =>
        Project is null ? string.Empty : $"Участников: {Project.Members.Count}";

    private IEnumerable<SharedMemberInfo> CurrentMembers =>
        Project?.Members.Select(m => new SharedMemberInfo { UserId = m.UserId.ToString(), Username = m.Username })
        ?? Enumerable.Empty<SharedMemberInfo>();

    public SharedProjectBoardViewModel(
        ISharedProjectsApiService api,
        SharedProjectsHubClient hub,
        SharedChatPanelViewModel chatPanel,
        SessionService session,
        NotificationService notifications,
        TaskMetaService metaService,
        SharedTaskEditViewModel taskEditVm)
    {
        _api = api;
        _hub = hub;
        _chatPanel = chatPanel;
        _session = session;
        _notifications = notifications;
        _metaService = metaService;
        _taskEditVm = taskEditVm;

        _hub.TaskCreated += OnHubTaskCreated;
        _hub.TaskUpdated += OnHubTaskUpdated;
        _hub.TaskDeleted += OnHubTaskDeleted;
        _hub.TaskMoved += OnHubTaskMoved;
        _hub.ColumnCreated += OnHubColumnCreated;
        _hub.ColumnUpdated += OnHubColumnUpdated;
        _hub.ColumnDeleted += OnHubColumnDeleted;
    }

    public async Task OpenAsync(SharedProjectDto project)
    {
        Project = project;
        OnPropertyChanged(nameof(MembersCountText));
        await LoadAsync();
        try
        {
            await _hub.SubscribeAsync(project.Id);
            IsConnected = _hub.IsConnected;
            StatusText = IsConnected ? null : "Real-time недоступен";
        }
        catch (Exception ex)
        {
            StatusText = $"Real-time недоступен: {ex.Message}";
            IsConnected = false;
        }
    }

    public override async Task OnNavigatedFromAsync()
    {
        await _hub.UnsubscribeAsync();
        IsConnected = false;
    }

    // ── Load ─────────────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (Project is null) return;
        IsLoading = true;
        try
        {
            var columns = await _api.GetColumnsAsync(Project.Id);
            var tasks = await _api.GetProjectTasksAsync(Project.Id);

            Columns.Clear();
            foreach (var col in columns.OrderBy(c => c.Order))
            {
                var colVm = SharedColumnViewModel.FromDto(col);
                WireColumnPersist(colVm);
                Columns.Add(colVm);
            }

            foreach (var t in tasks.OrderBy(x => x.Position))
            {
                var card = SharedTaskCardViewModel.FromDto(t, _metaService);
                var col = Columns.FirstOrDefault(c => c.Id == t.ColumnId) ?? Columns.FirstOrDefault();
                col?.Tasks.Add(card);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка загрузки: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    // ── Column CRUD ──────────────────────────────────────────────────────────

    [RelayCommand]
    private void ToggleAddColumnDialog() => ShowAddColumnDialog = !ShowAddColumnDialog;

    [RelayCommand]
    private async Task AddColumnAsync()
    {
        if (Project is null || string.IsNullOrWhiteSpace(NewColumnName)) return;
        var name = NewColumnName.Trim();
        NewColumnName = string.Empty;
        ShowAddColumnDialog = false;
        try
        {
            var dto = await _api.CreateColumnAsync(Project.Id, new CreateSharedProjectColumnDto
            {
                Name = name,
                Color = "#607D8B"
            });
            // Add immediately (SignalR may also add it — check for duplicate in handler)
            if (!Columns.Any(c => c.Id == dto.Id))
            {
                var colVm = SharedColumnViewModel.FromDto(dto);
                WireColumnPersist(colVm);
                Columns.Add(colVm);
            }
        }
        catch (Exception ex)
        {
            _notifications.Error("Ошибка", $"Не удалось создать колонку: {ex.Message}");
        }
    }

    [RelayCommand]
    private void StartRenameColumn(SharedColumnViewModel col)
    {
        col.RenameText = col.Name;
        col.IsRenaming = true;
    }

    [RelayCommand]
    private async Task ConfirmRenameColumnAsync(SharedColumnViewModel col)
    {
        if (Project is null || string.IsNullOrWhiteSpace(col.RenameText))
        {
            col.IsRenaming = false;
            return;
        }
        var newName = col.RenameText.Trim();
        try
        {
            await _api.UpdateColumnAsync(Project.Id, col.Id, new UpdateSharedProjectColumnDto
            {
                Name = newName,
                Color = col.Color,
                Order = col.Order
            });
            col.Name = newName;
            col.IsRenaming = false;
        }
        catch (Exception ex)
        {
            _notifications.Error("Ошибка", $"Не удалось переименовать: {ex.Message}");
            col.IsRenaming = false;
        }
    }

    [RelayCommand]
    private void CancelRenameColumn(SharedColumnViewModel col) => col.IsRenaming = false;

    [RelayCommand]
    private async Task DeleteColumnAsync(SharedColumnViewModel col)
    {
        if (Project is null) return;
        if (!ConfirmDialog.Show("Удалить колонку", $"Удалить колонку «{col.Name}»?\nЗадачи в ней потеряют привязку к колонке.")) return;
        try
        {
            await _api.DeleteColumnAsync(Project.Id, col.Id);
            Columns.Remove(col);
        }
        catch (Exception ex)
        {
            _notifications.Error("Ошибка", $"Не удалось удалить: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task PersistColumnOrderAsync()
    {
        if (Project is null) return;
        var orderedIds = Columns.Select(c => c.Id).ToList();
        try
        {
            await _api.ReorderColumnsAsync(Project.Id, orderedIds);
        }
        catch { /* non-critical */ }
    }

    private void WireColumnPersist(SharedColumnViewModel col)
    {
        col.PersistColor = async hex =>
        {
            if (Project is null) return;
            try
            {
                await _api.UpdateColumnAsync(Project.Id, col.Id, new UpdateSharedProjectColumnDto
                {
                    Name = col.Name,
                    Color = hex,
                    Order = col.Order
                });
            }
            catch { /* non-critical */ }
        };
    }

    // ── Task CRUD ────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task AddTaskToColumnAsync(SharedColumnViewModel? col)
    {
        if (col is null || Project is null) return;
        if (string.IsNullOrWhiteSpace(col.NewTaskTitle)) return;
        var title = col.NewTaskTitle.Trim();
        col.NewTaskTitle = string.Empty;
        try
        {
            var assignedId = _session.CurrentUser?.Id;
            var dto = await _api.CreateProjectTaskAsync(Project.Id, new CreateSharedProjectTaskDto
            {
                Title = title,
                ColumnId = col.Id,
                Priority = 1,
                AssignedUserId = assignedId
            });
            if (!col.Tasks.Any(t => t.Id == dto.Id))
                col.Tasks.Add(SharedTaskCardViewModel.FromDto(dto, _metaService));
        }
        catch (Exception ex)
        {
            _notifications.Error("Ошибка создания задачи", ex.Message);
        }
    }

    [RelayCommand]
    private void OpenAddTaskDialog(SharedColumnViewModel? col)
    {
        if (col is null || Project is null) return;
        _taskEditVm.PrepareNew(Project.Id, col.Id, CurrentMembers);
        _taskEditVm.Saved = (dto, isNew) =>
        {
            var card = SharedTaskCardViewModel.FromDto(dto, _metaService);
            var targetCol = Columns.FirstOrDefault(c => c.Id == dto.ColumnId) ?? col;
            if (!targetCol.Tasks.Any(t => t.Id == dto.Id))
                targetCol.Tasks.Add(card);
        };
        var dialog = new Views.Company.SharedTaskEditDialog(_taskEditVm);
        dialog.Owner = SysWin.Application.Current.MainWindow;
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void OpenEditTaskDialog(SharedTaskCardViewModel? task)
    {
        if (task is null || Project is null) return;
        _taskEditVm.PrepareEdit(Project.Id, task, CurrentMembers);
        _taskEditVm.Saved = (dto, _) => task.UpdateFromDto(dto);
        var dialog = new Views.Company.SharedTaskEditDialog(_taskEditVm);
        dialog.Owner = SysWin.Application.Current.MainWindow;
        dialog.ShowDialog();
    }

    [RelayCommand]
    private async Task DeleteTaskAsync(SharedTaskCardViewModel? task)
    {
        if (task is null || Project is null) return;
        if (!ConfirmDialog.Show("Удалить задачу", $"Удалить задачу «{task.Title}»?")) return;
        try
        {
            await _api.DeleteProjectTaskAsync(Project.Id, task.Id);
            RemoveTaskFromAllColumns(task.Id);
        }
        catch (Exception ex)
        {
            _notifications.Error("Ошибка", $"Не удалось удалить: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ToggleCompleteAsync(SharedTaskCardViewModel? task)
    {
        if (task is null || Project is null) return;
        try
        {
            var newCompleted = !task.IsCompleted;
            await _api.UpdateProjectTaskAsync(Project.Id, task.Id, new UpdateSharedProjectTaskDto
            {
                Title = task.Title,
                Description = task.Description,
                ColumnId = task.ColumnId,
                Priority = task.Priority,
                DueDate = task.DueDate,
                IsCompleted = newCompleted,
                Position = task.Position,
                AssignedUserId = task.AssignedUserId,
                Tags = task.Tags
            });
            task.IsCompleted = newCompleted;
        }
        catch (Exception ex)
        {
            _notifications.Error("Ошибка", $"Не удалось обновить: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task AssignToMeAsync(SharedTaskCardViewModel? task)
    {
        if (task is null || Project is null) return;
        var myId = _session.CurrentUser?.Id;
        if (myId is null) return;
        try
        {
            await _api.UpdateProjectTaskAsync(Project.Id, task.Id, new UpdateSharedProjectTaskDto
            {
                Title = task.Title,
                Description = task.Description,
                ColumnId = task.ColumnId,
                Priority = task.Priority,
                DueDate = task.DueDate,
                IsCompleted = task.IsCompleted,
                Position = task.Position,
                AssignedUserId = myId.Value,
                Tags = task.Tags
            });
            task.AssignedUserId = myId.Value;
            task.AssignedUsername = _session.CurrentUser?.Username;
        }
        catch (Exception ex)
        {
            _notifications.Error("Ошибка", $"Не удалось назначить: {ex.Message}");
        }
    }

    // Called from code-behind for drag-drop
    public async Task MoveTaskByDragAsync(SharedTaskCardViewModel srcTask, SharedColumnViewModel targetCol)
    {
        if (Project is null || srcTask.ColumnId == targetCol.Id) return;
        var srcCol = Columns.FirstOrDefault(c => c.Id == srcTask.ColumnId);
        if (srcCol is null) return;

        // Optimistic UI update
        srcCol.Tasks.Remove(srcTask);
        srcTask.ColumnId = targetCol.Id;
        srcTask.Position = targetCol.Tasks.Count;
        targetCol.Tasks.Add(srcTask);

        try
        {
            await _api.MoveProjectTaskAsync(Project.Id, srcTask.Id, new MoveSharedTaskDto
            {
                ColumnId = targetCol.Id,
                Position = srcTask.Position
            });
        }
        catch (Exception ex)
        {
            // Rollback
            targetCol.Tasks.Remove(srcTask);
            srcTask.ColumnId = srcCol.Id;
            srcCol.Tasks.Add(srcTask);
            _notifications.Error("Ошибка", $"Не удалось переместить задачу: {ex.Message}");
        }
    }

    // ── Chat ─────────────────────────────────────────────────────────────────

    [RelayCommand]
    private void OpenAssigneeChat(SharedTaskCardViewModel? task)
    {
        if (task?.AssignedUserId is null || string.IsNullOrEmpty(task.AssignedUsername)) return;
        var myId = _session.CurrentUser?.Id;
        if (myId == task.AssignedUserId) return;
        _chatPanel.OpenDirect(task.AssignedUserId.Value, task.AssignedUsername!);
    }

    [RelayCommand]
    private void OpenProjectChat()
    {
        if (Project is null) return;
        _chatPanel.OpenProject(Project.Id, Project.Name);
    }

    [RelayCommand]
    private void OpenChatList() => _chatPanel.OpenChatList();

    [RelayCommand]
    private void CloseChat() => _chatPanel.Close();

    // ── SignalR handlers ──────────────────────────────────────────────────────

    private void OnHubColumnCreated(SharedProjectColumnDto dto)
    {
        SysWin.Application.Current.Dispatcher.Invoke(() =>
        {
            if (Project is null || dto.ProjectId != Project.Id) return;
            if (Columns.Any(c => c.Id == dto.Id)) return;
            var colVm = SharedColumnViewModel.FromDto(dto);
            WireColumnPersist(colVm);
            var idx = Columns.Count(c => c.Order <= dto.Order);
            Columns.Insert(Math.Min(idx, Columns.Count), colVm);
        });
    }

    private void OnHubColumnUpdated(SharedProjectColumnDto dto)
    {
        SysWin.Application.Current.Dispatcher.Invoke(() =>
        {
            var col = Columns.FirstOrDefault(c => c.Id == dto.Id);
            if (col is null) return;
            col.Name = dto.Name;
            col.Color = dto.Color;
        });
    }

    private void OnHubColumnDeleted(Guid id)
    {
        SysWin.Application.Current.Dispatcher.Invoke(() =>
        {
            var col = Columns.FirstOrDefault(c => c.Id == id);
            if (col is not null) Columns.Remove(col);
        });
    }

    private void OnHubTaskCreated(SharedProjectTaskDto dto)
    {
        SysWin.Application.Current.Dispatcher.Invoke(() =>
        {
            if (Project is null || dto.ProjectId != Project.Id) return;
            if (FindTask(dto.Id) is not null) return;
            var col = Columns.FirstOrDefault(c => c.Id == dto.ColumnId) ?? Columns.FirstOrDefault();
            col?.Tasks.Add(SharedTaskCardViewModel.FromDto(dto, _metaService));
        });
    }

    private void OnHubTaskUpdated(SharedProjectTaskDto dto)
    {
        SysWin.Application.Current.Dispatcher.Invoke(() =>
        {
            if (Project is null || dto.ProjectId != Project.Id) return;
            var existing = FindTask(dto.Id);
            if (existing is null) { OnHubTaskCreated(dto); return; }
            if (existing.ColumnId != dto.ColumnId)
            {
                var oldCol = Columns.FirstOrDefault(c => c.Id == existing.ColumnId);
                var newCol = Columns.FirstOrDefault(c => c.Id == dto.ColumnId) ?? Columns.FirstOrDefault();
                oldCol?.Tasks.Remove(existing);
                existing.UpdateFromDto(dto);
                newCol?.Tasks.Add(existing);
            }
            else { existing.UpdateFromDto(dto); }
        });
    }

    private void OnHubTaskMoved((Guid Id, Guid ColumnId, int Position) args)
    {
        SysWin.Application.Current.Dispatcher.Invoke(() =>
        {
            var existing = FindTask(args.Id);
            if (existing is null) return;
            if (existing.ColumnId != args.ColumnId)
            {
                var oldCol = Columns.FirstOrDefault(c => c.Id == existing.ColumnId);
                var newCol = Columns.FirstOrDefault(c => c.Id == args.ColumnId);
                oldCol?.Tasks.Remove(existing);
                existing.ColumnId = args.ColumnId;
                existing.Position = args.Position;
                newCol?.Tasks.Add(existing);
            }
            else { existing.Position = args.Position; }
        });
    }

    private void OnHubTaskDeleted(Guid id)
    {
        SysWin.Application.Current.Dispatcher.Invoke(() => RemoveTaskFromAllColumns(id));
    }

    private void RemoveTaskFromAllColumns(Guid id)
    {
        foreach (var col in Columns)
        {
            var t = col.Tasks.FirstOrDefault(x => x.Id == id);
            if (t is not null) { col.Tasks.Remove(t); return; }
        }
    }

    private SharedTaskCardViewModel? FindTask(Guid id)
    {
        foreach (var col in Columns)
        {
            var found = col.Tasks.FirstOrDefault(t => t.Id == id);
            if (found is not null) return found;
        }
        return null;
    }
}
