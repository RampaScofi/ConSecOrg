using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Infrastructure.Api;
using ConSecOrg.Client.Services;
using ConSecOrg.Client.ViewModels.Base;
using ConSecOrg.Client.Views.Tasks;
using ConSecOrg.Shared.DTOs.Tasks;
using ConSecOrg.Shared.Enums;
using System.Collections.ObjectModel;
using System.Windows;

namespace ConSecOrg.Client.ViewModels.Tasks;

public partial class MyTaskRow : ObservableObject
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty; // "Project / Board / Column"
    public DateTime CreatedAt { get; init; }
    public DateTime? DueDate { get; init; }
    public TaskPriorityDto Priority { get; init; }
    public List<string> Tags { get; init; } = [];
    [ObservableProperty] private bool _isCompleted;
    [ObservableProperty] private bool _isStarred;

    public bool HasDueDate => DueDate.HasValue;
    public bool IsDueDatePast => DueDate.HasValue && DueDate.Value.Date < DateTime.Today;

    public string DueDateText => DueDate.HasValue
        ? DueDate.Value.ToString("dd MMM yyyy")
        : string.Empty;

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

public partial class MyTasksViewModel(
    ITasksApiService tasksService,
    ProjectService projectService,
    TaskMetaService taskMetaService,
    TaskDetailViewModel taskDetailVm,
    TaskEditViewModel taskEditVm) : BasePageViewModel
{
    public override string Title => "Мои задачи";

    [ObservableProperty] private ObservableCollection<MyTaskRow> _tasks = [];
    [ObservableProperty] private ObservableCollection<MyTaskRow> _filteredTasks = [];
    [ObservableProperty] private string _activeTab = "Mine";   // Mine | Delegated | Private | Starred
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string? _sortColumn;          // Title | DueDate | Priority
    [ObservableProperty] private bool _sortDescending;

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnActiveTabChanged(string value) => ApplyFilter();

    public override async Task OnNavigatedToAsync() => await LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            var all = await tasksService.GetBoardAsync();
            var rows = all.Select(t =>
            {
                var meta = taskMetaService.GetMeta(t.Id);
                var colId = projectService.GetTaskColumn(t.Id);
                var projId = projectService.GetTaskProject(t.Id);
                var boardId = projectService.GetTaskBoard(t.Id);

                var proj = projId.HasValue
                    ? projectService.Projects.FirstOrDefault(p => p.Id == projId.Value)
                    : null;
                var board = boardId.HasValue
                    ? proj?.Boards.FirstOrDefault(b => b.Id == boardId.Value)
                    : null;
                var cols = projectService.GetColumns(projId, boardId);
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
            ApplyFilter();
        });
    }

    private void ApplyFilter()
    {
        var src = Tasks.AsEnumerable();

        src = ActiveTab switch
        {
            "Starred" => src.Where(t => t.IsStarred),
            _ => src
        };

        if (!string.IsNullOrWhiteSpace(SearchText))
            src = src.Where(t => t.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                               || t.Path.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        if (SortColumn != null)
        {
            src = (SortColumn, SortDescending) switch
            {
                ("Title", false) => src.OrderBy(t => t.Title),
                ("Title", true)  => src.OrderByDescending(t => t.Title),
                ("DueDate", false) => src.OrderBy(t => t.DueDate ?? DateTime.MaxValue),
                ("DueDate", true)  => src.OrderByDescending(t => t.DueDate ?? DateTime.MinValue),
                ("Priority", false) => src.OrderByDescending(t => (int)t.Priority),
                ("Priority", true)  => src.OrderBy(t => (int)t.Priority),
                _ => src
            };
        }

        FilteredTasks = new ObservableCollection<MyTaskRow>(src);
    }

    [RelayCommand]
    private void SetTab(string tab)
    {
        ActiveTab = tab;
    }

    [RelayCommand]
    private void SortBy(string col)
    {
        if (SortColumn == col)
            SortDescending = !SortDescending;
        else
        {
            SortColumn = col;
            SortDescending = false;
        }
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
        taskMetaService.SetCompleted(row.Id, row.IsCompleted);
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
            taskMetaService);

        taskDetailVm.Load(card, null, row.Path);
        taskDetailVm.EditRequested = c =>
        {
            taskEditVm.TaskId = c.Task.Id;
            taskEditVm.Title = c.Task.Title;
            taskEditVm.Priority = c.Task.Priority;
            taskEditVm.DueDate = c.Task.DueDate;
            var meta = taskMetaService.GetMeta(c.Task.Id);
            taskEditVm.TagsInput = string.Join(", ", meta.Tags);
            taskEditVm.Description = meta.Description;
            var dlg = new TaskEditDialog(taskEditVm);
            dlg.Owner = System.Windows.Application.Current.MainWindow;
            dlg.ShowDialog();
            _ = LoadAsync();
        };

        var detailDlg = new TaskDetailDialog(taskDetailVm);
        detailDlg.Owner = System.Windows.Application.Current.MainWindow;
        detailDlg.ShowDialog();
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteTask(MyTaskRow row)
    {
        if (!Views.Dialogs.ConfirmDialog.Show("Удалить задачу", $"Удалить «{row.Title}»?\nДействие необратимо.")) return;
        taskMetaService.Remove(row.Id);
        await tasksService.DeleteTaskAsync(row.Id);
        Tasks.Remove(row);
        FilteredTasks.Remove(row);
    }
}
