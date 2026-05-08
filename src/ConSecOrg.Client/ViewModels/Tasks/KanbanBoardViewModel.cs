using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Infrastructure.Api;
using ConSecOrg.Client.Services;
using ConSecOrg.Client.ViewModels.Base;
using ConSecOrg.Client.ViewModels.Shell;
using ConSecOrg.Client.Views.Dialogs;
using ConSecOrg.Client.Views.Tasks;
using ConSecOrg.Shared.DTOs.Tasks;
using ConSecOrg.Shared.Enums;
using System.Collections.ObjectModel;
using System.Windows;

namespace ConSecOrg.Client.ViewModels.Tasks;

public class BoardTabItem
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#607D8B";
    public bool IsAll => Id == null;
}

public partial class KanbanBoardViewModel(
    ITasksApiService tasksService,
    ProjectService projectService,
    TaskMetaService taskMetaService,
    TaskEditViewModel taskEditVm,
    TaskDetailViewModel taskDetailVm,
    NotificationService notifications,
    BoardHubClient boardHub,
    ModeService modeService) : BasePageViewModel
{
    public override string Title => "Задачи";

    [ObservableProperty] private ObservableCollection<KanbanColumnViewModel> _columns = [];
    [ObservableProperty] private ObservableCollection<BoardTabItem> _boardTabs = [];
    [ObservableProperty] private BoardTabItem? _selectedBoardTab;

    [ObservableProperty] private Guid? _selectedProjectId;
    [ObservableProperty] private Guid? _selectedBoardId;
    [ObservableProperty] private string _activeProjectName = "Все задачи";

    // Shared project mode
    [ObservableProperty] private Guid? _sharedProjectId;
    [ObservableProperty] private string _sharedProjectName = string.Empty;
    public List<SharedMemberInfo> SharedProjectMembers { get; set; } = [];
    public bool IsSharedMode => SharedProjectId.HasValue;

    [ObservableProperty] private string _newProjectName = string.Empty;
    [ObservableProperty] private string _newBoardName = string.Empty;
    [ObservableProperty] private string _newColumnName = string.Empty;
    [ObservableProperty] private bool _showProjectDialog;
    [ObservableProperty] private bool _showBoardDialog;
    [ObservableProperty] private bool _showAddColumnDialog;

    [ObservableProperty] private string? _sortMode; // null | "Deadline" | "Priority"

    public IEnumerable<ProjectItem?> ProjectFilter =>
        new ProjectItem?[] { null }.Concat(projectService.Projects.Cast<ProjectItem?>());

    partial void OnSelectedProjectIdChanged(Guid? value)
    {
        UpdateBoardTabs();
        ActiveProjectName = projectService.Projects.FirstOrDefault(p => p.Id == value)?.Name ?? "Все задачи";
    }

    partial void OnSharedProjectIdChanged(Guid? value)
    {
        if (value.HasValue)
            ActiveProjectName = SharedProjectName.Length > 0 ? SharedProjectName : "Совместный проект";
        BoardTabs = [new BoardTabItem { Id = null, Name = "Все", Color = "#607D8B" }];
        SelectedBoardTab = BoardTabs[0];
        OnPropertyChanged(nameof(IsSharedMode));
    }

    partial void OnSelectedBoardTabChanged(BoardTabItem? value)
    {
        SelectedBoardId = value?.Id;
    }

    private void UpdateBoardTabs()
    {
        var tabs = new ObservableCollection<BoardTabItem>
        {
            new() { Id = null, Name = "Все", Color = "#607D8B" }
        };
        if (SelectedProjectId.HasValue)
        {
            var project = projectService.Projects.FirstOrDefault(p => p.Id == SelectedProjectId.Value);
            if (project != null)
                foreach (var b in project.Boards)
                    tabs.Add(new BoardTabItem { Id = b.Id, Name = b.Name, Color = b.Color });
        }
        BoardTabs = tabs;
        SelectedBoardTab = tabs[0];
    }

    [RelayCommand] private void SelectBoardTab(BoardTabItem tab) => SelectedBoardTab = tab;
    [RelayCommand] private void ToggleBoardDialog() => ShowBoardDialog = !ShowBoardDialog;
    [RelayCommand] private void ToggleAddColumnDialog() => ShowAddColumnDialog = !ShowAddColumnDialog;

    [RelayCommand]
    private void CreateProject()
    {
        if (string.IsNullOrWhiteSpace(NewProjectName)) return;
        projectService.CreateProject(NewProjectName, "#" + Random.Shared.Next(0x1000000).ToString("X6"));
        NewProjectName = string.Empty;
        ShowProjectDialog = false;
        OnPropertyChanged(nameof(ProjectFilter));
        UpdateBoardTabs();
    }

    [RelayCommand]
    private void CreateBoard()
    {
        if (string.IsNullOrWhiteSpace(NewBoardName) || !SelectedProjectId.HasValue) return;
        projectService.CreateBoard(SelectedProjectId.Value, NewBoardName);
        NewBoardName = string.Empty;
        ShowBoardDialog = false;
        UpdateBoardTabs();
    }

    // ── Column management ────────────────────────────────────────────────────────

    [RelayCommand]
    private void AddNewColumn()
    {
        if (string.IsNullOrWhiteSpace(NewColumnName)) return;
        var colModel = projectService.AddColumn(SelectedProjectId, SelectedBoardId, NewColumnName);
        NewColumnName = string.Empty;
        ShowAddColumnDialog = false;
        var colVm = new KanbanColumnViewModel(colModel);
        colVm.PersistColor = hex => projectService.SetColumnColor(colModel.Id, hex, SelectedProjectId, SelectedBoardId);
        Columns.Add(colVm);
    }

    [RelayCommand]
    private void StartRenameColumn(KanbanColumnViewModel col)
    {
        col.RenameText = col.Name;
        col.IsRenaming = true;
    }

    [RelayCommand]
    private void ConfirmRenameColumn(KanbanColumnViewModel col)
    {
        if (string.IsNullOrWhiteSpace(col.RenameText)) { col.IsRenaming = false; return; }
        projectService.RenameColumn(col.Model.Id, col.RenameText, SelectedProjectId, SelectedBoardId);
        col.Name = col.RenameText;
        col.IsRenaming = false;
    }

    [RelayCommand]
    private void CancelRenameColumn(KanbanColumnViewModel col) => col.IsRenaming = false;

    [RelayCommand]
    private void DeleteColumn(KanbanColumnViewModel col)
    {
        if (!ConfirmDialog.Show("Удалить колонку", $"Удалить колонку «{col.Name}»?\nЗадачи в ней потеряют привязку к колонке.")) return;
        projectService.DeleteColumn(col.Model.Id, SelectedProjectId, SelectedBoardId);
        Columns.Remove(col);
    }

    // ── Load ─────────────────────────────────────────────────────────────────────

    public override async Task OnNavigatedToAsync()
    {
        UpdateBoardTabs();
        await LoadAsync();
        await ConnectHubAsync();
    }

    private async Task ConnectHubAsync()
    {
        if (!modeService.IsCorporate || IsSharedMode) return;
        try
        {
            boardHub.TaskMoved -= OnRemoteTaskMoved;
            boardHub.TaskMoved += OnRemoteTaskMoved;
            await boardHub.EnsureConnectedAsync();
        }
        catch { }
    }

    private void OnRemoteTaskMoved(Guid taskId, int newColumn, int newStatus)
    {
        // Reload the board when another user moves a task
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(async () =>
        {
            await LoadAsync();
        });
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            var tasks = await tasksService.GetBoardAsync();
            var all = tasks.ToList();

            if (SelectedProjectId.HasValue)
            {
                var pid = SelectedProjectId.Value;
                all = all.Where(t => projectService.GetTaskProject(t.Id) == pid).ToList();
            }
            if (SelectedBoardId.HasValue)
            {
                var bid = SelectedBoardId.Value;
                all = all.Where(t => projectService.GetTaskBoard(t.Id) == bid).ToList();
            }

            var colModels = projectService.GetColumns(SelectedProjectId, SelectedBoardId);
            var colVms = new List<KanbanColumnViewModel>();
            foreach (var c in colModels)
            {
                var colVm = new KanbanColumnViewModel(c);
                colVm.PersistColor = hex => projectService.SetColumnColor(c.Id, hex, SelectedProjectId, SelectedBoardId);
                colVms.Add(colVm);
            }

            // Apply sort
            all = SortMode switch
            {
                "Deadline" => all.OrderBy(t => t.DueDate ?? DateTime.MaxValue).ToList(),
                "Priority" => all.OrderByDescending(t => (int)t.Priority).ToList(),
                _ => all
            };

            foreach (var task in all)
            {
                var card = new TaskCardViewModel(task, taskMetaService);
                var assignedColId = projectService.GetTaskColumn(task.Id);
                KanbanColumnViewModel? target = null;

                if (assignedColId.HasValue)
                    target = colVms.FirstOrDefault(c => c.Id == assignedColId.Value);

                if (target == null)
                    target = colVms.FirstOrDefault(c => c.StatusKey == StatusToKey(task.Status));

                target ??= colVms.FirstOrDefault();
                target?.Tasks.Add(card);
            }

            Columns = new ObservableCollection<KanbanColumnViewModel>(colVms);
        });
    }

    // ── Add / Edit tasks ─────────────────────────────────────────────────────────

    [RelayCommand]
    private void OpenAddTaskDialog(KanbanColumnViewModel col)
    {
        taskEditVm.TaskId = null;
        taskEditVm.Title = string.Empty;
        taskEditVm.Priority = TaskPriorityDto.Normal;
        taskEditVm.DueDate = null;
        taskEditVm.TagsInput = string.Empty;
        taskEditVm.Description = string.Empty;
        taskEditVm.TargetColumn = col;
        taskEditVm.ProjectId = SelectedProjectId;
        taskEditVm.BoardId = SelectedBoardId;
        taskEditVm.AssignedToUserId = null;
        taskEditVm.AssignedToUsername = null;
        taskEditVm.AvailableMembers = SharedProjectMembers;

        var dialog = new TaskEditDialog(taskEditVm);
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        dialog.ShowDialog();
        _ = LoadAsync();
    }

    [RelayCommand]
    private void EditTask(TaskCardViewModel card)
    {
        taskEditVm.LoadTask(card.Task);
        taskEditVm.TargetColumn = FindTaskColumn(card).col;
        taskEditVm.ProjectId = SelectedProjectId;
        taskEditVm.BoardId = SelectedBoardId;
        taskEditVm.AvailableMembers = SharedProjectMembers;

        var dialog = new TaskEditDialog(taskEditVm);
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        dialog.ShowDialog();
        card.ReloadMeta();
        _ = LoadAsync();
    }

    [RelayCommand]
    private void AddSubTask(TaskCardViewModel card)
    {
        var (col, _) = FindTaskColumn(card);
        var project = SelectedProjectId.HasValue
            ? projectService.Projects.FirstOrDefault(p => p.Id == SelectedProjectId.Value)
            : null;
        var board = SelectedBoardId.HasValue
            ? project?.Boards.FirstOrDefault(b => b.Id == SelectedBoardId.Value)
            : null;
        var path = project != null
            ? (board != null ? $"{project.Name} / {board.Name} / {col?.Name}" : $"{project.Name} / {col?.Name}")
            : col?.Name ?? "Задачи";

        taskDetailVm.Load(card, col, path);
        taskDetailVm.EditRequested = c => EditTask(c);

        var dialog = new TaskDetailDialog(taskDetailVm);
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        dialog.ShowDialog();
        card.ReloadMeta();
        _ = LoadAsync();
    }

    [RelayCommand]
    private void OpenDetailTask(TaskCardViewModel card)
    {
        var (col, _) = FindTaskColumn(card);
        var project = SelectedProjectId.HasValue
            ? projectService.Projects.FirstOrDefault(p => p.Id == SelectedProjectId.Value)
            : null;
        var board = SelectedBoardId.HasValue
            ? project?.Boards.FirstOrDefault(b => b.Id == SelectedBoardId.Value)
            : null;
        var path = project != null
            ? (board != null ? $"{project.Name} / {board.Name} / {col?.Name}" : $"{project.Name} / {col?.Name}")
            : col?.Name ?? "Задачи";

        taskDetailVm.Load(card, col, path);
        taskDetailVm.EditRequested = c => EditTask(c);

        var dialog = new TaskDetailDialog(taskDetailVm);
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        dialog.ShowDialog();
        card.ReloadMeta();
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task AddTaskToColumn(KanbanColumnViewModel col)
    {
        if (string.IsNullOrWhiteSpace(col.NewTaskTitle)) return;
        var title = col.NewTaskTitle;
        var status = col.StatusKey != null ? KeyToStatus(col.StatusKey) : TaskStatusDto.ToDo;

        await ExecuteAsync(async () =>
        {
            var id = await tasksService.CreateTaskAsync(new CreateTaskRequestDto
            {
                Title = title,
                Priority = TaskPriorityDto.Normal
            });

            if (SelectedProjectId.HasValue)
                projectService.AssignTask(id, SelectedProjectId.Value, SelectedBoardId, col.Model.Id);
            else
                projectService.AssignTaskToColumn(id, col.Model.Id);

            if (col.StatusKey != null && status != TaskStatusDto.ToDo)
                await tasksService.MoveTaskAsync(id, new MoveTaskRequestDto
                {
                    NewStatus = status, NewColumn = (int)status, NewPosition = 0
                });

            col.NewTaskTitle = string.Empty;
            await LoadAsync();
        });

        if (ErrorMessage is not null)
            notifications.Error("Ошибка создания задачи", ErrorMessage);
    }

    // ── Complete / Delete ─────────────────────────────────────────────────────────

    [RelayCommand]
    private void ToggleTaskCompleted(TaskCardViewModel card)
    {
        card.IsCompleted = !card.IsCompleted;
        taskMetaService.SetCompleted(card.Task.Id, card.IsCompleted);
    }

    [RelayCommand]
    private async Task DeleteTask(TaskCardViewModel card)
    {
        if (!ConfirmDialog.Show("Удалить задачу", $"Удалить задачу «{card.Task.Title}»?\nДействие необратимо.")) return;
        var (col, _) = FindTaskColumn(card);
        col?.Tasks.Remove(card);
        col?.RefreshTaskCount();
        taskMetaService.Remove(card.Task.Id);
        await tasksService.DeleteTaskAsync(card.Task.Id);
    }

    [RelayCommand]
    private void PersistColumnOrder()
    {
        var orderedIds = Columns.Select(c => c.Model.Id).ToList();
        projectService.ReorderColumns(SelectedProjectId, SelectedBoardId, orderedIds);
    }

    // ── Sort ─────────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SortByDeadline()
    {
        SortMode = SortMode == "Deadline" ? null : "Deadline";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task SortByPriority()
    {
        SortMode = SortMode == "Priority" ? null : "Priority";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ClearSort()
    {
        SortMode = null;
        await LoadAsync();
    }

    // ── Move left / right ────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task MoveTaskLeft(TaskCardViewModel card)
    {
        var (current, idx) = FindTaskColumn(card);
        if (current == null || idx <= 0) return;
        await MoveTaskToColumn(card, current, Columns[idx - 1]);
    }

    [RelayCommand]
    private async Task MoveTaskRight(TaskCardViewModel card)
    {
        var (current, idx) = FindTaskColumn(card);
        if (current == null || idx >= Columns.Count - 1) return;
        await MoveTaskToColumn(card, current, Columns[idx + 1]);
    }

    /// <summary>Используется из code-behind при drag-drop.</summary>
    internal async Task MoveTaskByDragAsync(TaskCardViewModel card, KanbanColumnViewModel toColumn)
    {
        var (from, _) = FindTaskColumn(card);
        if (from == null || from == toColumn) return;
        await MoveTaskToColumn(card, from, toColumn);
    }

    private async Task MoveTaskToColumn(TaskCardViewModel card, KanbanColumnViewModel from, KanbanColumnViewModel to)
    {
        from.Tasks.Remove(card);
        to.Tasks.Add(card);
        from.RefreshTaskCount();
        to.RefreshTaskCount();

        projectService.AssignTaskToColumn(card.Task.Id, to.Model.Id);

        if (to.StatusKey != null)
        {
            var newStatus = KeyToStatus(to.StatusKey);
            card.Task.Status = newStatus;
            await tasksService.MoveTaskAsync(card.Task.Id, new MoveTaskRequestDto
            {
                NewStatus = newStatus, NewColumn = (int)newStatus, NewPosition = 0
            });

            // Broadcast move to other connected corporate users
            if (modeService.IsCorporate && !IsSharedMode)
                _ = boardHub.NotifyTaskMovedAsync(card.Task.Id, (int)newStatus, (int)newStatus);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private (KanbanColumnViewModel? col, int index) FindTaskColumn(TaskCardViewModel card)
    {
        for (int i = 0; i < Columns.Count; i++)
            if (Columns[i].Tasks.Contains(card))
                return (Columns[i], i);
        return (null, -1);
    }

    private static string StatusToKey(TaskStatusDto s) => s switch
    {
        TaskStatusDto.ToDo => "todo",
        TaskStatusDto.InProgress => "inprogress",
        TaskStatusDto.Review => "review",
        TaskStatusDto.Done => "done",
        _ => "todo"
    };

    private static TaskStatusDto KeyToStatus(string key) => key switch
    {
        "todo" => TaskStatusDto.ToDo,
        "inprogress" => TaskStatusDto.InProgress,
        "review" => TaskStatusDto.Review,
        "done" => TaskStatusDto.Done,
        _ => TaskStatusDto.ToDo
    };
}
