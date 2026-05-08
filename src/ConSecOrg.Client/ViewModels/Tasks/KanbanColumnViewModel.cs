using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Services;
using System.Collections.ObjectModel;

namespace ConSecOrg.Client.ViewModels.Tasks;

public partial class KanbanColumnViewModel : ObservableObject
{
    public ColumnModel Model { get; }

    public Guid Id => Model.Id;
    public string? StatusKey => Model.StatusKey;

    // Called by the board VM after construction to wire up persistence.
    public Action<string>? PersistColor { get; set; }

    [ObservableProperty] private string _color;
    [ObservableProperty] private string _name;
    [ObservableProperty] private ObservableCollection<TaskCardViewModel> _tasks = [];
    [ObservableProperty] private string _newTaskTitle = string.Empty;
    [ObservableProperty] private bool _isRenaming;
    [ObservableProperty] private string _renameText = string.Empty;
    [ObservableProperty] private bool _showColorPicker;

    public KanbanColumnViewModel(ColumnModel model)
    {
        Model = model;
        _name = model.Name;
        _color = model.Color;
        _renameText = model.Name;
    }

    public int TaskCount => Tasks.Count;

    public void RefreshTaskCount() => OnPropertyChanged(nameof(TaskCount));

    [RelayCommand]
    private void ToggleColorPicker() => ShowColorPicker = !ShowColorPicker;

    [RelayCommand]
    private void SetColor(string hex) => Color = hex;

    partial void OnColorChanged(string value)
    {
        Model.Color = value;
        ShowColorPicker = false;
        PersistColor?.Invoke(value);
    }

    partial void OnNameChanged(string value) => Model.Name = value;

    partial void OnTasksChanged(ObservableCollection<TaskCardViewModel> value)
        => OnPropertyChanged(nameof(TaskCount));
}
