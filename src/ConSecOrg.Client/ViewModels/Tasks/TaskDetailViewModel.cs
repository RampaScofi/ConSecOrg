using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Services;
using ConSecOrg.Client.Views.Tasks;
using ConSecOrg.Shared.DTOs.Tasks;
using ConSecOrg.Shared.Enums;
using System.Collections.ObjectModel;
using WpfApp = System.Windows.Application;

namespace ConSecOrg.Client.ViewModels.Tasks;

public partial class TaskDetailViewModel : ObservableObject
{
    private readonly TaskMetaService _metaService;
    private readonly SubTaskEditViewModel _subTaskEditVm;

    public TaskItemDto? Task { get; private set; }
    public KanbanColumnViewModel? Column { get; private set; }
    public string ColumnPath { get; private set; } = string.Empty;

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private TaskPriorityDto _priority;
    [ObservableProperty] private DateTime? _dueDate;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private ObservableCollection<string> _tags = [];
    [ObservableProperty] private bool _isCompleted;
    [ObservableProperty] private ObservableCollection<SubTaskItem> _subTasks = [];
    [ObservableProperty] private string _newSubTaskTitle = string.Empty;

    public string PriorityText => Priority switch
    {
        TaskPriorityDto.Low => "Не важно",
        TaskPriorityDto.Normal => "Нормально",
        TaskPriorityDto.High => "Важно",
        TaskPriorityDto.Critical => "Критично!",
        _ => "Обычный"
    };

    public string PriorityColor => Priority switch
    {
        TaskPriorityDto.Low => "#616161",
        TaskPriorityDto.Normal => "#2E7D32",
        TaskPriorityDto.High => "#E65100",
        TaskPriorityDto.Critical => "#C62828",
        _ => "#607D8B"
    };

    public string PriorityBackground => Priority switch
    {
        TaskPriorityDto.Low => "#1A9E9E9E",
        TaskPriorityDto.Normal => "#1A4CAF50",
        TaskPriorityDto.High => "#1AFF9800",
        TaskPriorityDto.Critical => "#1AF44336",
        _ => "#1A607D8B"
    };

    public bool HasDueDate => DueDate.HasValue;
    public bool IsDueDatePast => DueDate.HasValue && DueDate.Value.Date < DateTime.Today;
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    public bool HasTags => Tags.Count > 0;
    public bool HasSubTasks => SubTasks.Count > 0;
    public int CompletedSubTaskCount => SubTasks.Count(s => s.IsCompleted);
    public string SubTaskProgress => HasSubTasks ? $"{CompletedSubTaskCount}/{SubTasks.Count}" : string.Empty;

    public Action? CloseRequested { get; set; }
    public Action<TaskCardViewModel>? EditRequested { get; set; }

    private TaskCardViewModel? _card;

    public TaskDetailViewModel(TaskMetaService metaService, SubTaskEditViewModel subTaskEditVm)
    {
        _metaService = metaService;
        _subTaskEditVm = subTaskEditVm;
    }

    public void Load(TaskCardViewModel card, KanbanColumnViewModel? col, string columnPath)
    {
        _card = card;
        Task = card.Task;
        Column = col;
        ColumnPath = columnPath;

        Title = card.Task.Title;
        Priority = card.Task.Priority;
        DueDate = card.Task.DueDate;
        IsCompleted = card.IsCompleted;
        Tags = new ObservableCollection<string>(card.Tags);

        var meta = _metaService.GetMeta(card.Task.Id);
        Description = meta.Description;
        SubTasks = new ObservableCollection<SubTaskItem>(meta.SubTasks);
        NewSubTaskTitle = string.Empty;

        OnPropertyChanged(nameof(PriorityText));
        OnPropertyChanged(nameof(PriorityColor));
        OnPropertyChanged(nameof(PriorityBackground));
        OnPropertyChanged(nameof(HasDueDate));
        OnPropertyChanged(nameof(IsDueDatePast));
        OnPropertyChanged(nameof(HasDescription));
        OnPropertyChanged(nameof(HasTags));
        OnPropertyChanged(nameof(HasSubTasks));
        OnPropertyChanged(nameof(SubTaskProgress));
    }

    [RelayCommand]
    private void AddSubTask()
    {
        if (string.IsNullOrWhiteSpace(NewSubTaskTitle) || Task == null) return;
        var sub = _metaService.AddSubTask(Task.Id, NewSubTaskTitle.Trim());
        SubTasks.Add(sub);
        NewSubTaskTitle = string.Empty;
        OnPropertyChanged(nameof(HasSubTasks));
        OnPropertyChanged(nameof(SubTaskProgress));
    }

    [RelayCommand]
    private void AddChildSubTask(SubTaskItem parent)
    {
        if (Task == null || string.IsNullOrWhiteSpace(parent.NewChildTitle)) return;
        // Service adds child to parent.Children (ObservableCollection) — UI updates automatically
        _metaService.AddChildSubTask(Task.Id, parent.Id, parent.NewChildTitle.Trim());
        parent.NewChildTitle = string.Empty;
        parent.IsExpanded = true;
        parent.RefreshChildStats();
        OnPropertyChanged(nameof(SubTaskProgress));
    }

    /// <summary>Сохранить отредактированный inline заголовок подзадачи.</summary>
    [RelayCommand]
    private void SaveSubTaskTitle(SubTaskItem sub)
    {
        if (Task == null || sub == null) return;
        _metaService.UpdateSubTask(Task.Id, sub);
    }

    [RelayCommand]
    private void EditSubTask(SubTaskItem sub)
    {
        if (Task == null) return;
        _subTaskEditVm.Load(sub);
        var dlg = new SubTaskEditDialog(_subTaskEditVm)
        {
            Owner = WpfApp.Current.MainWindow
        };
        dlg.ShowDialog();
        if (_subTaskEditVm.Saved)
            _metaService.UpdateSubTask(Task.Id, sub);
    }

    [RelayCommand]
    private void ToggleExpanded(SubTaskItem sub) => sub.IsExpanded = !sub.IsExpanded;

    [RelayCommand]
    private void ToggleSubTask(SubTaskItem sub)
    {
        if (Task == null) return;
        _metaService.ToggleSubTask(Task.Id, sub.Id);
        OnPropertyChanged(nameof(SubTaskProgress));
        OnPropertyChanged(nameof(CompletedSubTaskCount));
    }

    [RelayCommand]
    private void RemoveSubTask(SubTaskItem sub)
    {
        if (Task == null) return;
        _metaService.RemoveSubTask(Task.Id, sub.Id);
        // Remove from top-level or from any parent's children
        if (!SubTasks.Remove(sub))
        {
            foreach (var top in SubTasks)
            {
                if (top.Children.Remove(sub))
                {
                    top.RefreshChildStats();
                    break;
                }
            }
        }
        OnPropertyChanged(nameof(HasSubTasks));
        OnPropertyChanged(nameof(SubTaskProgress));
    }

    [RelayCommand]
    private void Edit()
    {
        if (_card == null) return;
        CloseRequested?.Invoke();
        EditRequested?.Invoke(_card);
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();

    partial void OnPriorityChanged(TaskPriorityDto value)
    {
        OnPropertyChanged(nameof(PriorityText));
        OnPropertyChanged(nameof(PriorityColor));
        OnPropertyChanged(nameof(PriorityBackground));
    }

    partial void OnDueDateChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(HasDueDate));
        OnPropertyChanged(nameof(IsDueDatePast));
    }

    partial void OnDescriptionChanged(string value)
        => OnPropertyChanged(nameof(HasDescription));

    partial void OnTagsChanged(ObservableCollection<string> value)
        => OnPropertyChanged(nameof(HasTags));

    partial void OnSubTasksChanged(ObservableCollection<SubTaskItem> value)
    {
        OnPropertyChanged(nameof(HasSubTasks));
        OnPropertyChanged(nameof(SubTaskProgress));
    }
}
