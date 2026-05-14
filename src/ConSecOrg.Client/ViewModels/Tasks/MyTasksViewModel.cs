using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Infrastructure.Api;
using ConSecOrg.Client.Services;
using ConSecOrg.Client.ViewModels.Base;
using ConSecOrg.Client.Views.Tasks;
using ConSecOrg.Shared.DTOs.Tasks;
using ConSecOrg.Shared.Enums;
using System.Collections.ObjectModel;

namespace ConSecOrg.Client.ViewModels.Tasks;

// ── Row types ────────────────────────────────────────────────────────────────

public partial class MyTaskRow : ObservableObject
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? DueDate { get; init; }
    public TaskPriorityDto Priority { get; init; }
    public List<string> Tags { get; init; } = [];
    [ObservableProperty] private bool _isCompleted;
    [ObservableProperty] private bool _isStarred;

    public bool HasDueDate => DueDate.HasValue;
    public bool IsDueDatePast => DueDate.HasValue && DueDate.Value.Date < DateTime.Today;
    public string DueDateText => DueDate?.ToString("dd MMM yyyy") ?? string.Empty;

    public string PriorityText => Priority switch
    {
        TaskPriorityDto.Low => "Не важно",
        TaskPriorityDto.Normal => "Нормально",
        TaskPriorityDto.High => "Важно",
        TaskPriorityDto.Critical => "Критично!",
        _ => string.Empty
    };
    public string PriorityColor => Priority switch
    {
        TaskPriorityDto.Low => "#616161",
        TaskPriorityDto.Normal => "#2E7D32",
        TaskPriorityDto.High => "#E65100",
        TaskPriorityDto.Critical => "#C62828",
        _ => "#607D8B"
    };
    public string PriorityBg => Priority switch
    {
        TaskPriorityDto.Low => "#1A9E9E9E",
        TaskPriorityDto.Normal => "#1A4CAF50",
        TaskPriorityDto.High => "#1AFF9800",
        TaskPriorityDto.Critical => "#1AF44336",
        _ => "#1A607D8B"
    };
    public string DueDateBg => IsDueDatePast ? "#1AF44336" : "#1A2196F3";
    public string DueDateFg => IsDueDatePast ? "#F44336" : "#2196F3";
}

// ── Private tab: notes + tasks with Confidential / Secret level ───────────────

public class PrivateItemRow
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string ItemType { get; init; } = string.Empty;   // "Заметка" | "Задача"
    public string SecurityLevelText { get; init; } = string.Empty;
    public string SecurityLevelColor { get; init; } = "#607D8B";
    public string SecurityLevelBg { get; init; } = "#1A607D8B";
    public DateTime CreatedAt { get; init; }
    public string Source { get; init; } = string.Empty;     // category or project path
    public bool IsNote { get; init; }
    public bool IsTask => !IsNote;
}

// ── Delegated tab: tasks assigned by current user to someone else ─────────────

public class DelegatedTaskRow
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string AssignedToUsername { get; init; } = string.Empty;
    public string ProjectPath { get; init; } = string.Empty;
    public TaskPriorityDto Priority { get; init; }
    public DateTime? DueDate { get; init; }
    public bool IsCompleted { get; init; }
    public List<string> Tags { get; init; } = [];

    public bool HasDueDate => DueDate.HasValue;
    public bool IsDueDatePast => DueDate.HasValue && DueDate.Value.Date < DateTime.Today;
    public string DueDateText => DueDate?.ToString("dd MMM yyyy") ?? string.Empty;
    public string DueDateBg => IsDueDatePast ? "#1AF44336" : "#1A2196F3";
    public string DueDateFg => IsDueDatePast ? "#F44336" : "#2196F3";
    public string PriorityText => Priority switch
    {
        TaskPriorityDto.Low => "Не важно",
        TaskPriorityDto.Normal => "Нормально",
        TaskPriorityDto.High => "Важно",
        TaskPriorityDto.Critical => "Критично!",
        _ => string.Empty
    };
    public string PriorityColor => Priority switch
    {
        TaskPriorityDto.Low => "#616161",
        TaskPriorityDto.Normal => "#2E7D32",
        TaskPriorityDto.High => "#E65100",
        TaskPriorityDto.Critical => "#C62828",
        _ => "#607D8B"
    };
    public string PriorityBg => Priority switch
    {
        TaskPriorityDto.Low => "#1A9E9E9E",
        TaskPriorityDto.Normal => "#1A4CAF50",
        TaskPriorityDto.High => "#1AFF9800",
        TaskPriorityDto.Critical => "#1AF44336",
        _ => "#1A607D8B"
    };
}

// ── ViewModel ────────────────────────────────────────────────────────────────

public partial class MyTasksViewModel : BasePageViewModel
{
    private readonly ITasksApiService _tasksService;
    private readonly INotesApiService _notesService;
    private readonly ProjectService _projectService;
    private readonly TaskMetaService _taskMetaService;
    private readonly SessionService _sessionService;
    private readonly TaskDetailViewModel _taskDetailVm;
    private readonly TaskEditViewModel _taskEditVm;

    public MyTasksViewModel(
        ITasksApiService tasksService,
        INotesApiService notesService,
        ProjectService projectService,
        TaskMetaService taskMetaService,
        SessionService sessionService,
        TaskDetailViewModel taskDetailVm,
        TaskEditViewModel taskEditVm)
    {
        _tasksService = tasksService;
        _notesService = notesService;
        _projectService = projectService;
        _taskMetaService = taskMetaService;
        _sessionService = sessionService;
        _taskDetailVm = taskDetailVm;
        _taskEditVm = taskEditVm;
    }

    public override string Title => "Мои задачи";

    public bool IsManagerOrAdmin =>
        _sessionService.CurrentUser?.Role is UserRoleDto.Admin or UserRoleDto.Manager;

    // ── Collections ──────────────────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<MyTaskRow> _tasks = [];
    [ObservableProperty] private ObservableCollection<MyTaskRow> _filteredTasks = [];
    [ObservableProperty] private ObservableCollection<PrivateItemRow> _filteredPrivateItems = [];
    [ObservableProperty] private ObservableCollection<DelegatedTaskRow> _filteredDelegatedTasks = [];

    private List<PrivateItemRow> _allPrivateItems = [];
    private List<DelegatedTaskRow> _allDelegatedTasks = [];

    // ── State ─────────────────────────────────────────────────────────────────

    [ObservableProperty] private string _activeTab = "Mine";
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string? _sortColumn;
    [ObservableProperty] private bool _sortDescending;

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnActiveTabChanged(string value) => ApplyFilter();

    public override async Task OnNavigatedToAsync() => await LoadAsync();

    // ── Load ─────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            var all = await _tasksService.GetBoardAsync();
            var currentUserId = _sessionService.CurrentUser?.Id;

            // Build MyTaskRow list (Mine / Starred tabs)
            var rows = all.Select(t =>
            {
                var meta = _taskMetaService.GetMeta(t.Id);
                var colId = _projectService.GetTaskColumn(t.Id);
                var projId = _projectService.GetTaskProject(t.Id);
                var boardId = _projectService.GetTaskBoard(t.Id);
                var proj = projId.HasValue ? _projectService.Projects.FirstOrDefault(p => p.Id == projId.Value) : null;
                var board = boardId.HasValue ? proj?.Boards.FirstOrDefault(b => b.Id == boardId.Value) : null;
                var cols = _projectService.GetColumns(projId, boardId);
                var col = colId.HasValue ? cols.FirstOrDefault(c => c.Id == colId.Value) : null;
                var path = proj != null
                    ? (board != null
                        ? $"{proj.Name} / {board.Name}{(col != null ? " / " + col.Name : "")}"
                        : $"{proj.Name}{(col != null ? " / " + col.Name : "")}")
                    : col?.Name ?? "Все задачи";

                return new MyTaskRow
                {
                    Id = t.Id,
                    Title = t.Title,
                    Path = path,
                    CreatedAt = t.CreatedAt,
                    DueDate = t.DueDate,
                    Priority = t.Priority,
                    Tags = meta.Tags,
                    IsCompleted = meta.IsCompleted,
                };
            }).ToList();

            Tasks = new ObservableCollection<MyTaskRow>(rows);

            // Build Private tab: notes (Confidential/Secret) + tasks tagged "Конфиденциальный"/"Секретный"
            await LoadPrivateItemsAsync(all);

            // Build Delegated tab (manager/admin only)
            if (IsManagerOrAdmin)
                LoadDelegatedTasks(all, currentUserId);

            ApplyFilter();
        });
    }

    private async Task LoadPrivateItemsAsync(IReadOnlyList<TaskItemDto> allTasks)
    {
        var items = new List<PrivateItemRow>();

        // Private notes: Confidential (2) or Secret (3)
        try
        {
            var page1 = await _notesService.GetNotesAsync(page: 1, pageSize: 100);
            var privateNotes = page1.Items
                .Where(n => n.SecurityLevel >= SecurityLevelDto.Confidential);

            foreach (var note in privateNotes)
            {
                var (levelText, levelColor, levelBg) = note.SecurityLevel == SecurityLevelDto.Secret
                    ? ("Секретный", "#F44336", "#1AF44336")
                    : ("Конфиденциальный", "#FF9800", "#1AFF9800");

                items.Add(new PrivateItemRow
                {
                    Id = note.Id,
                    Title = note.Title,
                    ItemType = "Заметка",
                    SecurityLevelText = levelText,
                    SecurityLevelColor = levelColor,
                    SecurityLevelBg = levelBg,
                    CreatedAt = note.CreatedAt,
                    Source = note.CategoryName ?? "Без категории",
                    IsNote = true,
                });
            }
        }
        catch { /* network/local error — skip notes */ }

        // Private tasks: those with tag "Конфиденциальный" or "Секретный"
        foreach (var task in allTasks)
        {
            var meta = _taskMetaService.GetMeta(task.Id);
            var hasSecret = meta.Tags.Any(t =>
                t.Equals("Секретный", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("Конфиденциальный", StringComparison.OrdinalIgnoreCase));
            if (!hasSecret) continue;

            var isSecret = meta.Tags.Any(t => t.Equals("Секретный", StringComparison.OrdinalIgnoreCase));
            var (levelText, levelColor, levelBg) = isSecret
                ? ("Секретный", "#F44336", "#1AF44336")
                : ("Конфиденциальный", "#FF9800", "#1AFF9800");

            var colId = _projectService.GetTaskColumn(task.Id);
            var projId = _projectService.GetTaskProject(task.Id);
            var boardId = _projectService.GetTaskBoard(task.Id);
            var proj = projId.HasValue ? _projectService.Projects.FirstOrDefault(p => p.Id == projId.Value) : null;
            var board = boardId.HasValue ? proj?.Boards.FirstOrDefault(b => b.Id == boardId.Value) : null;
            var source = proj != null ? $"{proj.Name}{(board != null ? " / " + board.Name : "")}" : "Задачи";

            items.Add(new PrivateItemRow
            {
                Id = task.Id,
                Title = task.Title,
                ItemType = "Задача",
                SecurityLevelText = levelText,
                SecurityLevelColor = levelColor,
                SecurityLevelBg = levelBg,
                CreatedAt = task.CreatedAt,
                Source = source,
                IsNote = false,
            });
        }

        _allPrivateItems = items.OrderByDescending(i => i.SecurityLevelText == "Секретный")
                                .ThenBy(i => i.Title)
                                .ToList();
    }

    private void LoadDelegatedTasks(IReadOnlyList<TaskItemDto> allTasks, Guid? currentUserId)
    {
        var delegated = new List<DelegatedTaskRow>();

        var currentUserIdStr = currentUserId?.ToString();

        foreach (var task in allTasks)
        {
            var assignee = _taskMetaService.GetAssignee(task.Id);
            if (string.IsNullOrEmpty(assignee.UserId)) continue;
            if (assignee.UserId == currentUserIdStr) continue; // assigned to self = not delegated

            var meta = _taskMetaService.GetMeta(task.Id);
            var colId = _projectService.GetTaskColumn(task.Id);
            var projId = _projectService.GetTaskProject(task.Id);
            var boardId = _projectService.GetTaskBoard(task.Id);
            var proj = projId.HasValue ? _projectService.Projects.FirstOrDefault(p => p.Id == projId.Value) : null;
            var board = boardId.HasValue ? proj?.Boards.FirstOrDefault(b => b.Id == boardId.Value) : null;
            var cols = _projectService.GetColumns(projId, boardId);
            var col = colId.HasValue ? cols.FirstOrDefault(c => c.Id == colId.Value) : null;
            var path = proj != null
                ? (board != null
                    ? $"{proj.Name} / {board.Name}{(col != null ? " / " + col.Name : "")}"
                    : proj.Name)
                : "Задачи";

            delegated.Add(new DelegatedTaskRow
            {
                Id = task.Id,
                Title = task.Title,
                AssignedToUsername = assignee.Username ?? "Неизвестный",
                ProjectPath = path,
                Priority = task.Priority,
                DueDate = task.DueDate,
                IsCompleted = meta.IsCompleted,
                Tags = meta.Tags,
            });
        }

        _allDelegatedTasks = delegated;
    }

    // ── Filter & sort ─────────────────────────────────────────────────────────

    private void ApplyFilter()
    {
        switch (ActiveTab)
        {
            case "Private":
                ApplyPrivateFilter();
                return;
            case "Delegated":
                ApplyDelegatedFilter();
                return;
        }

        var src = Tasks.AsEnumerable();

        if (ActiveTab == "Starred")
            src = src.Where(t => t.IsStarred);

        if (!string.IsNullOrWhiteSpace(SearchText))
            src = src.Where(t =>
                t.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                t.Path.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        if (SortColumn != null)
        {
            src = (SortColumn, SortDescending) switch
            {
                ("Title", false) => src.OrderBy(t => t.Title),
                ("Title", true) => src.OrderByDescending(t => t.Title),
                ("DueDate", false) => src.OrderBy(t => t.DueDate ?? DateTime.MaxValue),
                ("DueDate", true) => src.OrderByDescending(t => t.DueDate ?? DateTime.MinValue),
                ("Priority", false) => src.OrderByDescending(t => (int)t.Priority),
                ("Priority", true) => src.OrderBy(t => (int)t.Priority),
                _ => src
            };
        }

        FilteredTasks = new ObservableCollection<MyTaskRow>(src);
    }

    private void ApplyPrivateFilter()
    {
        var src = _allPrivateItems.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(SearchText))
            src = src.Where(i => i.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                 i.Source.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        FilteredPrivateItems = new ObservableCollection<PrivateItemRow>(src);
    }

    private void ApplyDelegatedFilter()
    {
        var src = _allDelegatedTasks.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(SearchText))
            src = src.Where(r =>
                r.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                r.AssignedToUsername.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                r.ProjectPath.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        FilteredDelegatedTasks = new ObservableCollection<DelegatedTaskRow>(src);
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private void SetTab(string tab) => ActiveTab = tab;

    [RelayCommand]
    private void SortBy(string col)
    {
        if (SortColumn == col) SortDescending = !SortDescending;
        else { SortColumn = col; SortDescending = false; }
        ApplyFilter();
    }

    [RelayCommand]
    private void ToggleStar(MyTaskRow row)
    {
        row.IsStarred = !row.IsStarred;
        if (ActiveTab == "Starred") ApplyFilter();
    }

    [RelayCommand]
    private void ToggleComplete(MyTaskRow row)
    {
        row.IsCompleted = !row.IsCompleted;
        _taskMetaService.SetCompleted(row.Id, row.IsCompleted);
    }

    [RelayCommand]
    private void OpenTask(MyTaskRow row)
    {
        var card = new TaskCardViewModel(
            new TaskItemDto
            {
                Id = row.Id,
                Title = row.Title,
                Priority = row.Priority,
                DueDate = row.DueDate,
                CreatedAt = row.CreatedAt
            },
            _taskMetaService);

        _taskDetailVm.Load(card, null, row.Path);
        _taskDetailVm.EditRequested = c =>
        {
            _taskEditVm.TaskId = c.Task.Id;
            _taskEditVm.Title = c.Task.Title;
            _taskEditVm.Priority = c.Task.Priority;
            _taskEditVm.DueDate = c.Task.DueDate;
            var meta = _taskMetaService.GetMeta(c.Task.Id);
            _taskEditVm.TagsInput = string.Join(", ", meta.Tags);
            _taskEditVm.Description = meta.Description;
            var dlg = new TaskEditDialog(_taskEditVm) { Owner = System.Windows.Application.Current.MainWindow };
            dlg.ShowDialog();
            _ = LoadAsync();
        };

        var detailDlg = new TaskDetailDialog(_taskDetailVm) { Owner = System.Windows.Application.Current.MainWindow };
        detailDlg.ShowDialog();
        _ = LoadAsync();
    }

    [RelayCommand]
    private void OpenDelegatedTask(DelegatedTaskRow row)
    {
        var card = new TaskCardViewModel(
            new TaskItemDto { Id = row.Id, Title = row.Title, Priority = row.Priority, DueDate = row.DueDate },
            _taskMetaService);

        _taskDetailVm.Load(card, null, row.ProjectPath);
        var dlg = new TaskDetailDialog(_taskDetailVm) { Owner = System.Windows.Application.Current.MainWindow };
        dlg.ShowDialog();
    }

    [RelayCommand]
    private async Task DeleteTask(MyTaskRow row)
    {
        if (!Views.Dialogs.ConfirmDialog.Show("Удалить задачу", $"Удалить «{row.Title}»?\nДействие необратимо.")) return;
        _taskMetaService.Remove(row.Id);
        await _tasksService.DeleteTaskAsync(row.Id);
        Tasks.Remove(row);
        FilteredTasks.Remove(row);
    }
}
