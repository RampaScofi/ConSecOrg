using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Infrastructure.Api;
using ConSecOrg.Client.Services;
using ConSecOrg.Client.ViewModels.Base;
using ConSecOrg.Client.ViewModels.Notes;
using ConSecOrg.Client.ViewModels.Tasks;
using ConSecOrg.Shared.DTOs.Tasks;
using ConSecOrg.Shared.Enums;
using System.Collections.ObjectModel;

namespace ConSecOrg.Client.ViewModels.Calendar;

public class CalendarDayTask
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Color { get; init; } = "#607D8B";
    public bool IsCompleted { get; init; }
    public bool IsNote { get; init; }
}

public partial class CalendarDay : ObservableObject
{
    public DateTime Date { get; init; }
    public int DayNumber => Date.Day;
    public bool IsCurrentMonth { get; init; }
    public bool IsToday => Date.Date == DateTime.Today;
    public bool IsWeekend => Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    [ObservableProperty] private int _noteCount;
    public bool HasNotes => NoteCount > 0;
    partial void OnNoteCountChanged(int value) => OnPropertyChanged(nameof(HasNotes));

    public ObservableCollection<CalendarDayTask> Tasks { get; } = [];
    public ObservableCollection<CalendarDayTask> VisibleTasks { get; } = [];

    public int ExtraTaskCount => Math.Max(0, Tasks.Count - 3);
    public bool HasExtraTasks => ExtraTaskCount > 0;
    public bool HasAnyTasks => Tasks.Count > 0;

    public void RefreshTasks()
    {
        // Rebuild VisibleTasks in-place so the existing binding stays alive
        VisibleTasks.Clear();
        foreach (var t in Tasks.Take(4))
            VisibleTasks.Add(t);

        OnPropertyChanged(nameof(ExtraTaskCount));
        OnPropertyChanged(nameof(HasExtraTasks));
        OnPropertyChanged(nameof(HasAnyTasks));
    }
}

public partial class WeekDayViewModel : ObservableObject
{
    public DateTime Date { get; init; }
    public string ShortName { get; init; } = string.Empty;
    public bool IsToday => Date.Date == DateTime.Today;
    public bool IsWeekend => Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    public string DayNumber => Date.Day.ToString();
    public ObservableCollection<WeekTaskItem> Tasks { get; } = [];
}

public class WeekTaskItem
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string PriorityColor { get; init; } = "#607D8B";
    public bool IsCompleted { get; init; }
}

public partial class CalendarViewModel(
    INotesApiService notesService,
    ITasksApiService tasksService,   // used only for tasks with explicit DueDate
    NavigationService navigation,
    TaskEditViewModel taskEditVm) : BasePageViewModel
{
    public override string Title => "Календарь";

    // ── Month view ────────────────────────────────────────────────────────────
    [ObservableProperty] private DateTime _displayMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private ObservableCollection<CalendarDay> _days = [];
    [ObservableProperty] private string _monthTitle = string.Empty;
    [ObservableProperty] private CalendarDay? _selectedDay;

    // ── Week view ─────────────────────────────────────────────────────────────
    [ObservableProperty] private DateTime _weekStart = GetWeekStart(DateTime.Today);
    [ObservableProperty] private ObservableCollection<WeekDayViewModel> _weekDays = [];
    [ObservableProperty] private string _weekTitle = string.Empty;

    // ── View mode ─────────────────────────────────────────────────────────────
    [ObservableProperty] private string _viewMode = "Month";
    public bool IsMonthView => ViewMode == "Month";
    public bool IsWeekView => ViewMode == "Week";
    partial void OnViewModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsMonthView));
        OnPropertyChanged(nameof(IsWeekView));
    }

    // ── Day detail panel ──────────────────────────────────────────────────────
    [ObservableProperty] private bool _showDayPanel;
    [ObservableProperty] private string _dayPanelTitle = string.Empty;
    [ObservableProperty] private ObservableCollection<CalendarDayTask> _dayPanelTasks = [];
    private DateTime _dayPanelDate;

    public string[] DayNames { get; } = ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"];

    private static readonly string[] _shortDayNames = ["Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс"];
    private static readonly string[] _monthNames =
    [
        "Январь","Февраль","Март","Апрель","Май","Июнь",
        "Июль","Август","Сентябрь","Октябрь","Ноябрь","Декабрь"
    ];
    private static readonly string[] _monthNamesGen =
    [
        "января","февраля","марта","апреля","мая","июня",
        "июля","августа","сентября","октября","ноября","декабря"
    ];

    // ── Navigation ────────────────────────────────────────────────────────────

    public override async Task OnNavigatedToAsync()
    {
        BuildCalendar();
        BuildWeekDays();
        await LoadAsync();
    }

    [RelayCommand]
    private void SwitchView(string mode)
    {
        ViewMode = mode;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task PrevMonth()
    {
        DisplayMonth = DisplayMonth.AddMonths(-1);
        BuildCalendar();
        await LoadAsync();
    }

    [RelayCommand]
    private async Task NextMonth()
    {
        DisplayMonth = DisplayMonth.AddMonths(1);
        BuildCalendar();
        await LoadAsync();
    }

    [RelayCommand]
    private async Task PrevWeek()
    {
        WeekStart = WeekStart.AddDays(-7);
        BuildWeekDays();
        await LoadWeekTasksAsync();
    }

    [RelayCommand]
    private async Task NextWeek()
    {
        WeekStart = WeekStart.AddDays(7);
        BuildWeekDays();
        await LoadWeekTasksAsync();
    }

    [RelayCommand]
    private async Task GoToToday()
    {
        DisplayMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        WeekStart = GetWeekStart(DateTime.Today);
        BuildCalendar();
        BuildWeekDays();
        await LoadAsync();
    }

    [RelayCommand]
    private void SelectDay(CalendarDay day)
    {
        SelectedDay = day;
        _dayPanelDate = day.Date;
        DayPanelTitle = $"{day.Date.Day} {_monthNamesGen[day.Date.Month - 1]} {day.Date.Year}";
        DayPanelTasks = new ObservableCollection<CalendarDayTask>(day.Tasks);
        ShowDayPanel = true;
    }

    [RelayCommand]
    private void CloseDayPanel() => ShowDayPanel = false;

    [RelayCommand]
    private void CreateTaskForDay()
    {
        taskEditVm.TaskId = null;
        taskEditVm.Title = string.Empty;
        taskEditVm.Priority = TaskPriorityDto.Normal;
        taskEditVm.DueDate = _dayPanelDate;
        taskEditVm.TagsInput = string.Empty;
        taskEditVm.Description = string.Empty;
        taskEditVm.TargetColumn = null;
        taskEditVm.ProjectId = null;
        taskEditVm.BoardId = null;

        var dialog = new Views.Tasks.TaskEditDialog(taskEditVm);
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        dialog.ShowDialog();
        _ = LoadAsync();
    }

    [RelayCommand]
    private void CreateNoteForDayPanel()
    {
        ShowDayPanel = false;
        var title = $"Заметка на {_dayPanelDate.Day} {_monthNamesGen[_dayPanelDate.Month - 1]}";
        navigation.NavigateTo<NoteEditorViewModel>(vm => vm.NoteTitle = title);
    }

    [RelayCommand]
    private void CreateNoteForWeekDay(WeekDayViewModel day)
    {
        var title = $"Заметка на {day.Date.Day} {_monthNamesGen[day.Date.Month - 1]}";
        navigation.NavigateTo<NoteEditorViewModel>(vm => vm.NoteTitle = title);
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    private async Task LoadAsync()
    {
        await LoadMonthTasksAsync();
        await LoadWeekTasksAsync();
    }

    // Month view: tasks with DueDate + notes in day cells
    private async Task LoadMonthTasksAsync()
    {
        var daySet = Days.ToDictionary(d => d.Date.Date);
        foreach (var day in Days) { day.Tasks.Clear(); day.NoteCount = 0; }

        // Only tasks that have an explicit DueDate
        try
        {
            var allTasks = await tasksService.GetBoardAsync();
            foreach (var t in allTasks.Where(t => t.DueDate.HasValue))
            {
                if (!daySet.TryGetValue(t.DueDate!.Value.Date, out var cell)) continue;
                cell.Tasks.Add(new CalendarDayTask
                {
                    Id = t.Id,
                    Title = t.Title,
                    Color = PriorityToColor(t.Priority),
                    IsCompleted = t.Status == TaskStatusDto.Done,
                    IsNote = false
                });
            }
        }
        catch { /* non-critical */ }

        // Notes placed on their UpdatedAt date
        try
        {
            var result = await notesService.GetNotesAsync(pageSize: 500);
            foreach (var n in result.Items)
            {
                if (!daySet.TryGetValue(n.UpdatedAt.Date, out var cell)) continue;
                cell.Tasks.Add(new CalendarDayTask
                {
                    Id = n.Id,
                    Title = n.Title,
                    Color = "#1E88E5",
                    IsCompleted = false,
                    IsNote = true
                });
                cell.NoteCount++;
            }
        }
        catch { /* non-critical */ }

        foreach (var day in Days) day.RefreshTasks();
    }

    // Week view: same logic
    private async Task LoadWeekTasksAsync()
    {
        var weekDaySet = WeekDays.ToDictionary(d => d.Date.Date);
        foreach (var wd in WeekDays) wd.Tasks.Clear();

        try
        {
            var allTasks = await tasksService.GetBoardAsync();
            foreach (var t in allTasks.Where(t => t.DueDate.HasValue))
            {
                if (!weekDaySet.TryGetValue(t.DueDate!.Value.Date, out var wd)) continue;
                wd.Tasks.Add(new WeekTaskItem
                {
                    Id = t.Id,
                    Title = t.Title,
                    PriorityColor = PriorityToColor(t.Priority),
                    IsCompleted = t.Status == TaskStatusDto.Done
                });
            }
        }
        catch { /* non-critical */ }

        try
        {
            var result = await notesService.GetNotesAsync(pageSize: 500);
            foreach (var n in result.Items)
            {
                if (!weekDaySet.TryGetValue(n.UpdatedAt.Date, out var wd)) continue;
                wd.Tasks.Add(new WeekTaskItem
                {
                    Id = n.Id,
                    Title = n.Title,
                    PriorityColor = "#1E88E5",
                    IsCompleted = false
                });
            }
        }
        catch { /* non-critical */ }
    }

    // ── Build helpers ─────────────────────────────────────────────────────────

    private void BuildCalendar()
    {
        MonthTitle = $"{_monthNames[DisplayMonth.Month - 1]} {DisplayMonth.Year}";

        var list = new List<CalendarDay>();
        var startDow = (int)DisplayMonth.DayOfWeek;
        if (startDow == 0) startDow = 7;
        var start = DisplayMonth.AddDays(-(startDow - 1));

        for (int i = 0; i < 42; i++)
        {
            var d = start.AddDays(i);
            list.Add(new CalendarDay { Date = d, IsCurrentMonth = d.Month == DisplayMonth.Month });
        }

        Days = new ObservableCollection<CalendarDay>(list);
        SelectedDay = list.FirstOrDefault(d => d.IsToday)
                   ?? list.FirstOrDefault(d => d.IsCurrentMonth);
    }

    private void BuildWeekDays()
    {
        var end = WeekStart.AddDays(6);
        var startMonth = _monthNamesGen[WeekStart.Month - 1];
        var endMonth = _monthNamesGen[end.Month - 1];
        WeekTitle = WeekStart.Month == end.Month
            ? $"{WeekStart.Day}–{end.Day} {startMonth} {WeekStart.Year}"
            : $"{WeekStart.Day} {startMonth} – {end.Day} {endMonth} {WeekStart.Year}";

        var days = new ObservableCollection<WeekDayViewModel>();
        for (int i = 0; i < 7; i++)
        {
            var d = WeekStart.AddDays(i);
            days.Add(new WeekDayViewModel
            {
                Date = d,
                ShortName = _shortDayNames[i]
            });
        }
        WeekDays = days;
    }

    private static DateTime GetWeekStart(DateTime date)
    {
        int dow = (int)date.DayOfWeek;
        if (dow == 0) dow = 7;
        return date.AddDays(-(dow - 1)).Date;
    }

    private static string PriorityToColor(TaskPriorityDto p) => p switch
    {
        TaskPriorityDto.Critical => "#E53935",
        TaskPriorityDto.High => "#FF9800",
        TaskPriorityDto.Normal => "#4CAF50",
        _ => "#607D8B"
    };

}
