using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Services;
using ConSecOrg.Shared.Enums;

namespace ConSecOrg.Client.ViewModels.Tasks;

public partial class SubTaskEditViewModel : ObservableObject
{
    private SubTaskItem? _target;

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private TaskPriorityDto _priority = TaskPriorityDto.Normal;
    [ObservableProperty] private DateTime? _dueDate;

    public bool HasDueDate => DueDate.HasValue;

    public Action? CloseRequested { get; set; }
    public bool Saved { get; private set; }

    public void Load(SubTaskItem sub)
    {
        _target = sub;
        Title = sub.Title;
        Description = sub.Description;
        Priority = sub.Priority;
        DueDate = sub.DueDate;
        Saved = false;
    }

    partial void OnDueDateChanged(DateTime? value)
        => OnPropertyChanged(nameof(HasDueDate));

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Title) || _target == null) return;

        _target.Title = Title.Trim();
        _target.Description = Description;
        _target.Priority = Priority;
        _target.DueDate = DueDate;
        _target.RefreshDisplayProps();

        Saved = true;
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke();
}
