using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Infrastructure.Api;
using ConSecOrg.Client.Services;
using ConSecOrg.Client.ViewModels.Shell;
using ConSecOrg.Shared.DTOs.Projects;
using ConSecOrg.Shared.Enums;
using System.Collections.ObjectModel;

namespace ConSecOrg.Client.ViewModels.Company;

public partial class SharedTaskEditViewModel : ObservableObject
{
    private readonly ISharedProjectsApiService _api;
    private readonly SessionService _session;
    private readonly TaskMetaService _metaService;

    // ── Editing context ─────────────────────────────────────────────────────
    public Guid ProjectId { get; set; }
    public Guid? ColumnId { get; set; }
    private Guid? _taskId;
    public bool IsNew => !_taskId.HasValue;
    public string DialogTitle => IsNew ? "Новая задача" : "Редактировать задачу";
    public string SaveButtonText => IsNew ? "Создать" : "Сохранить";

    // ── Fields ───────────────────────────────────────────────────────────────
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private int _priority = 1;
    [ObservableProperty] private DateTime? _dueDate;
    [ObservableProperty] private string _tagsInput = string.Empty;
    [ObservableProperty] private ObservableCollection<string> _tags = [];
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private string? _errorMessage;

    // ── Assignment ───────────────────────────────────────────────────────────
    [ObservableProperty] private SharedMemberInfo? _selectedAssignee;
    public ObservableCollection<SharedMemberInfo> AvailableMembers { get; } = new();

    // Admin/Manager can pick assignee; User gets auto-assigned to self
    public bool CanAssign { get; private set; }

    partial void OnTagsInputChanged(string value)
    {
        Tags = new ObservableCollection<string>(
            value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .Where(t => !string.IsNullOrWhiteSpace(t)));
    }

    public static int[] PriorityValues { get; } = [0, 1, 2, 3];

    public static string PriorityLabel(int p) => p switch { 3 => "Критично!", 2 => "Важно", 0 => "Не важно", _ => "Нормально" };

    public Action? CloseRequested { get; set; }
    // Invoked after successful save with the resulting task DTO
    public Action<SharedProjectTaskDto, bool>? Saved { get; set; }

    public SharedTaskEditViewModel(
        ISharedProjectsApiService api,
        SessionService session,
        TaskMetaService metaService)
    {
        _api = api;
        _session = session;
        _metaService = metaService;
    }

    /// <summary>Prepare for creating a new task in the given column.</summary>
    public void PrepareNew(Guid projectId, Guid columnId, IEnumerable<SharedMemberInfo> members)
    {
        ProjectId = projectId;
        ColumnId = columnId;
        _taskId = null;
        Title = string.Empty;
        Priority = 1;
        DueDate = null;
        TagsInput = string.Empty;
        Description = string.Empty;
        ErrorMessage = null;
        SetupMembers(members);
    }

    /// <summary>Prepare for editing an existing task.</summary>
    public void PrepareEdit(Guid projectId, SharedTaskCardViewModel card, IEnumerable<SharedMemberInfo> members)
    {
        ProjectId = projectId;
        ColumnId = card.ColumnId;
        _taskId = card.Id;
        Title = card.Title;
        Priority = card.Priority;
        DueDate = card.DueDate;
        Description = card.Description ?? string.Empty;
        TagsInput = string.Join(", ", card.Tags);
        ErrorMessage = null;
        SetupMembers(members);
        SelectedAssignee = AvailableMembers.FirstOrDefault(m => m.UserId == card.AssignedUserId?.ToString());
    }

    private void SetupMembers(IEnumerable<SharedMemberInfo> members)
    {
        var role = _session.CurrentUser?.Role ?? UserRoleDto.User;
        CanAssign = role is UserRoleDto.Admin or UserRoleDto.Manager;
        OnPropertyChanged(nameof(CanAssign));

        AvailableMembers.Clear();
        foreach (var m in members)
            AvailableMembers.Add(m);

        SelectedAssignee = null;
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
            Guid? assignedId = null;
            if (CanAssign)
                assignedId = SelectedAssignee?.UserId is { } uid ? Guid.Parse(uid) : (Guid?)null;
            else
                assignedId = _session.CurrentUser?.Id;

            SharedProjectTaskDto result;
            if (IsNew)
            {
                result = await _api.CreateProjectTaskAsync(ProjectId, new CreateSharedProjectTaskDto
                {
                    Title = Title.Trim(),
                    Description = string.IsNullOrWhiteSpace(Description) ? null : Description,
                    ColumnId = ColumnId,
                    Priority = Priority,
                    DueDate = DueDate,
                    AssignedUserId = assignedId,
                    Tags = Tags.ToList()
                });
            }
            else
            {
                result = await _api.UpdateProjectTaskAsync(ProjectId, _taskId!.Value, new UpdateSharedProjectTaskDto
                {
                    Title = Title.Trim(),
                    Description = string.IsNullOrWhiteSpace(Description) ? null : Description,
                    ColumnId = ColumnId,
                    Priority = Priority,
                    DueDate = DueDate,
                    IsCompleted = false,
                    Position = 0,
                    AssignedUserId = assignedId,
                    Tags = Tags.ToList()
                });
            }

            Saved?.Invoke(result, IsNew);
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
