using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Infrastructure.Api;
using ConSecOrg.Client.Services;
using ConSecOrg.Client.ViewModels.Shell;
using ConSecOrg.Shared.DTOs.Tasks;
using ConSecOrg.Shared.Enums;
using System.Collections.ObjectModel;

namespace ConSecOrg.Client.ViewModels.Tasks;

public partial class TaskEditViewModel : ObservableObject
{
    private readonly ITasksApiService _tasksService;
    private readonly ProjectService _projectService;
    private readonly TaskMetaService _taskMetaService;

    [ObservableProperty] private Guid? _taskId;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private TaskPriorityDto _priority = TaskPriorityDto.Normal;
    [ObservableProperty] private DateTime? _dueDate;
    [ObservableProperty] private string _tagsInput = string.Empty;
    [ObservableProperty] private ObservableCollection<string> _tags = [];
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private string? _errorMessage;

    // Set by the board before opening dialog
    public KanbanColumnViewModel? TargetColumn { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? BoardId { get; set; }

    // Assignee (for shared projects)
    [ObservableProperty] private string? _assignedToUserId;
    [ObservableProperty] private string? _assignedToUsername;
    [ObservableProperty] private SharedMemberInfo? _selectedAssignee;
    public List<SharedMemberInfo> AvailableMembers { get; set; } = [];
    public bool HasMembers => AvailableMembers.Count > 0;

    partial void OnSelectedAssigneeChanged(SharedMemberInfo? value)
    {
        AssignedToUserId = value?.UserId;
        AssignedToUsername = value?.Username;
    }

    public bool IsNew => !TaskId.HasValue;
    public string DialogTitle => IsNew ? "Новая задача" : "Редактировать задачу";
    public string SaveButtonText => IsNew ? "Создать" : "Сохранить";

    public Array Priorities => Enum.GetValues(typeof(TaskPriorityDto));

    public Action? CloseRequested { get; set; }

    public TaskEditViewModel(
        ITasksApiService tasksService,
        ProjectService projectService,
        TaskMetaService taskMetaService)
    {
        _tasksService = tasksService;
        _projectService = projectService;
        _taskMetaService = taskMetaService;
    }

    public void LoadTask(TaskItemDto task)
    {
        TaskId = task.Id;
        Title = task.Title;
        Priority = task.Priority;
        DueDate = task.DueDate;

        var meta = _taskMetaService.GetMeta(task.Id);
        Description = meta.Description;
        TagsInput = string.Join(", ", meta.Tags);
        AssignedToUserId = meta.AssignedToUserId;
        AssignedToUsername = meta.AssignedToUsername;
        SelectedAssignee = AvailableMembers.FirstOrDefault(m => m.UserId == meta.AssignedToUserId);
    }

    partial void OnTagsInputChanged(string value)
    {
        Tags = new ObservableCollection<string>(
            value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .Where(t => !string.IsNullOrWhiteSpace(t)));
    }

    [RelayCommand]
    private void RemoveTag(string tag)
    {
        var list = Tags.Where(t => t != tag).ToList();
        Tags = new ObservableCollection<string>(list);
        TagsInput = string.Join(", ", list);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            ErrorMessage = "Введите название задачи.";
            return;
        }

        IsSaving = true;
        ErrorMessage = null;
        try
        {
            Guid savedId;
            if (IsNew)
            {
                savedId = await _tasksService.CreateTaskAsync(new CreateTaskRequestDto
                {
                    Title = Title,
                    Priority = Priority,
                    DueDate = DueDate
                });

                if (ProjectId.HasValue)
                    _projectService.AssignTask(savedId, ProjectId.Value, BoardId, TargetColumn?.Model.Id);
                else if (TargetColumn != null)
                    _projectService.AssignTaskToColumn(savedId, TargetColumn.Model.Id);

                // Move to correct status if column has StatusKey
                if (TargetColumn?.StatusKey is { } key && key != "todo")
                {
                    var status = key switch
                    {
                        "inprogress" => TaskStatusDto.InProgress,
                        "review" => TaskStatusDto.Review,
                        "done" => TaskStatusDto.Done,
                        _ => TaskStatusDto.ToDo
                    };
                    await _tasksService.MoveTaskAsync(savedId, new MoveTaskRequestDto
                    {
                        NewStatus = status, NewColumn = (int)status, NewPosition = 0
                    });
                }
            }
            else
            {
                savedId = TaskId!.Value;
                await _tasksService.UpdateTaskAsync(savedId, new UpdateTaskRequestDto
                {
                    Title = Title,
                    Priority = Priority,
                    DueDate = DueDate
                });
            }

            var existingMeta = _taskMetaService.GetMeta(savedId);
            _taskMetaService.SetMeta(savedId, new TaskMetaEntry
            {
                Description = Description,
                Tags = Tags.ToList(),
                IsCompleted = existingMeta.IsCompleted,
                SubTasks = existingMeta.SubTasks,
                AssignedToUserId = AssignedToUserId,
                AssignedToUsername = AssignedToUsername
            });

            CloseRequested?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();
}
