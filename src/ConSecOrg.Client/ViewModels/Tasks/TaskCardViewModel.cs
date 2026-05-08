using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Services;
using ConSecOrg.Shared.DTOs.Tasks;
using System.Collections.ObjectModel;

namespace ConSecOrg.Client.ViewModels.Tasks;

public partial class TaskCardViewModel : ObservableObject
{
    private readonly TaskMetaService _metaService;

    public TaskItemDto Task { get; }

    [ObservableProperty] private bool _isCompleted;
    [ObservableProperty] private List<string> _tags = [];
    [ObservableProperty] private bool _isSubTasksExpanded;
    [ObservableProperty] private ObservableCollection<SubTaskItem> _subTasks = [];
    [ObservableProperty] private string _newSubTaskTitle = string.Empty;

    public Guid Id => Task.Id;

    public int SubTaskCount => SubTasks.Count;
    public int CompletedSubTaskCount => SubTasks.Count(s => s.IsCompleted);
    public bool HasSubTasks => SubTasks.Count > 0;
    public double SubTaskProgress => HasSubTasks ? (double)CompletedSubTaskCount / SubTaskCount : 0;

    public string SubTaskCountText => HasSubTasks
        ? $"{CompletedSubTaskCount}/{SubTasks.Count}"
        : string.Empty;

    [ObservableProperty] private string? _assignedToUsername;
    [ObservableProperty] private string? _assignedToUserId;
    public string AssignedToLetter => AssignedToUsername?.Length > 0
        ? AssignedToUsername[0].ToString().ToUpper() : string.Empty;
    public bool HasAssignee => !string.IsNullOrEmpty(AssignedToUsername);

    public TaskCardViewModel(TaskItemDto task, TaskMetaService metaService)
    {
        Task = task;
        _metaService = metaService;
        var meta = metaService.GetMeta(task.Id);
        _isCompleted = meta.IsCompleted;
        _tags = meta.Tags;
        _subTasks = new ObservableCollection<SubTaskItem>(meta.SubTasks);
        _assignedToUsername = meta.AssignedToUsername;
        _assignedToUserId = meta.AssignedToUserId;
    }

    [RelayCommand]
    private void ToggleSubTasksExpanded() => IsSubTasksExpanded = !IsSubTasksExpanded;

    [RelayCommand]
    private void ToggleSubTask(SubTaskItem sub)
    {
        _metaService.ToggleSubTask(Task.Id, sub.Id);
        // Обновить счётчик "x/y" у прямого родителя подзадачи
        FindParentOf(SubTasks, sub)?.RefreshChildStats();
        RefreshSubTaskCounts();
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

    [RelayCommand]
    private void ToggleSubExpanded(SubTaskItem sub) => sub.IsExpanded = !sub.IsExpanded;

    /// <summary>Добавить подзадачу верхнего уровня (через NewSubTaskTitle).</summary>
    [RelayCommand]
    private void AddTopSubTask()
    {
        if (string.IsNullOrWhiteSpace(NewSubTaskTitle)) return;
        var sub = _metaService.AddSubTask(Task.Id, NewSubTaskTitle.Trim());
        SubTasks.Add(sub);
        IsSubTasksExpanded = true;
        NewSubTaskTitle = string.Empty;
        RefreshSubTaskCounts();
    }

    /// <summary>Добавить дочернюю подзадачу к указанному родителю (через parent.NewChildTitle).</summary>
    [RelayCommand]
    private void AddChildSub(SubTaskItem parent)
    {
        if (parent == null || string.IsNullOrWhiteSpace(parent.NewChildTitle)) return;
        _metaService.AddChildSubTask(Task.Id, parent.Id, parent.NewChildTitle.Trim());
        parent.NewChildTitle = string.Empty;
        parent.IsExpanded = true;
        parent.RefreshChildStats();
    }

    [RelayCommand]
    private void SaveSubTitle(SubTaskItem sub)
    {
        if (sub == null) return;
        _metaService.UpdateSubTask(Task.Id, sub);
    }

    [RelayCommand]
    private void RemoveSubTask(SubTaskItem sub)
    {
        if (sub == null) return;
        _metaService.RemoveSubTask(Task.Id, sub.Id);
        if (!SubTasks.Remove(sub))
        {
            // Найти прямого родителя и обновить его счётчик
            var parent = FindParentOf(SubTasks, sub);
            parent?.Children.Remove(sub);
            parent?.RefreshChildStats();
        }
        RefreshSubTaskCounts();
    }

    public void AddSubTaskItem(SubTaskItem sub)
    {
        SubTasks.Add(sub);
        IsSubTasksExpanded = true;
        RefreshSubTaskCounts();
    }

    private void RefreshSubTaskCounts()
    {
        OnPropertyChanged(nameof(SubTaskCount));
        OnPropertyChanged(nameof(CompletedSubTaskCount));
        OnPropertyChanged(nameof(HasSubTasks));
        OnPropertyChanged(nameof(SubTaskCountText));
        OnPropertyChanged(nameof(SubTaskProgress));
    }

    public void ReloadMeta()
    {
        var meta = _metaService.GetMeta(Task.Id);
        IsCompleted = meta.IsCompleted;
        Tags = meta.Tags;
        SubTasks = new ObservableCollection<SubTaskItem>(meta.SubTasks);
        AssignedToUsername = meta.AssignedToUsername;
        AssignedToUserId = meta.AssignedToUserId;
        OnPropertyChanged(nameof(AssignedToLetter));
        OnPropertyChanged(nameof(HasAssignee));
        RefreshSubTaskCounts();
    }
}
