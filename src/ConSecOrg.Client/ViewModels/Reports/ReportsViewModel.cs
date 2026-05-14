using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Infrastructure.Api;
using ConSecOrg.Client.Services;
using ConSecOrg.Client.ViewModels.Base;
using ConSecOrg.Shared.DTOs.Tasks;
using ConSecOrg.Shared.Enums;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;

namespace ConSecOrg.Client.ViewModels.Reports;

// ── Data models ──────────────────────────────────────────────────────────────

public enum ReportColumnType
{
    AssignedTo, LastUpdated, DueDate, Status, Priority, TaskCount, CreatedAt
}

public class ReportColumnTypeItem
{
    public ReportColumnType Type { get; set; }
    public string Label { get; set; } = string.Empty;
}

public class CustomReportColumn
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public ReportColumnType DataType { get; set; }
}

public partial class AddColumnViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private string _columnName = string.Empty;
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private ReportColumnTypeItem? _selectedType;

    public ObservableCollection<ReportColumnTypeItem> TypeOptions { get; } =
    [
        new() { Type = ReportColumnType.AssignedTo, Label = "Исполнитель" },
        new() { Type = ReportColumnType.LastUpdated, Label = "Обновлён" },
        new() { Type = ReportColumnType.DueDate, Label = "Ближ. дедлайн" },
        new() { Type = ReportColumnType.Status, Label = "Статус задач" },
        new() { Type = ReportColumnType.Priority, Label = "Приоритет" },
        new() { Type = ReportColumnType.TaskCount, Label = "Всего задач" },
        new() { Type = ReportColumnType.CreatedAt, Label = "Дата создания" },
    ];

    public bool IsValid => !string.IsNullOrWhiteSpace(ColumnName) && SelectedType is not null;
}

public class GeneralReportRow
{
    public string Name { get; set; } = string.Empty;
    public int OpenTasks { get; set; }
    public int CompletedTasks { get; set; }
    public string? AssignedTo { get; set; }
    public string? LastUpdated { get; set; }
    public string? DueDate { get; set; }
    // Dynamic extra columns (same order as ReportsViewModel.CustomColumns)
    public List<string> CustomValues { get; set; } = [];
}

public class SavedReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public int OpenTasks { get; set; }
    public int CompletedTasks { get; set; }
    public string Filter { get; set; } = "all";
}

public class GanttTask
{
    public string Title { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string Color { get; set; } = "#4CAF50";
    public bool IsCompleted { get; set; }
    public string Initials { get; set; } = "";
    public double Left { get; set; }
    public double BarWidth { get; set; }
    public double Top { get; set; }
}

public class GanttDateHeader
{
    public string Label { get; set; } = string.Empty;
    public bool IsToday { get; set; }
    public bool IsWeekend { get; set; }
}

public class TimeColumnHeader
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#607D8B";
    public string AverageTimeText { get; set; } = "0:00";
    public double BarHeightPixels { get; set; }
}

public class TimeCell
{
    public string Text { get; set; } = string.Empty;
    public bool IsHighlighted { get; set; }
}

public class TimeColumnRow
{
    public string TaskTitle { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public ObservableCollection<TimeCell> Cells { get; set; } = [];
}

public class EmployeeTaskRow
{
    public string Title { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
    public string AssignedToUsername { get; set; } = "—";
    public bool HasAssignee => AssignedToUsername != "—" && !string.IsNullOrEmpty(AssignedToUsername);
    public string StatusText { get; set; } = string.Empty;
    public string StatusColor { get; set; } = "#607D8B";
    public string PriorityText { get; set; } = string.Empty;
    public string PriorityColor { get; set; } = "#607D8B";
    public DateTime CreatedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsCompleted { get; set; }
}

// Report configuration dialog state
public partial class ReportConfigViewModel : ObservableObject
{
    [ObservableProperty] private DateTime? _dateFrom;
    [ObservableProperty] private DateTime? _dateTo;
    [ObservableProperty] private bool _showCompleted = true;
    [ObservableProperty] private bool _showOpen = true;
    [ObservableProperty] private bool _groupByProject = true;
}

// ── ViewModel ────────────────────────────────────────────────────────────────

public partial class ReportsViewModel : BasePageViewModel
{
    private readonly ITasksApiService _tasksService;
    private readonly ProjectService _projectService;
    private readonly ISharedProjectsApiService _sharedService;
    private readonly TaskMetaService _taskMeta;
    private readonly ModeService _modeService;

    public override string Title => "Отчёты";

    private const double DayWidthPx = 42.0;
    private const int TimelineDays = 28;
    private const double MaxBarHeightPx = 130.0;

    private DateTime _timelineStart;
    private IReadOnlyList<TaskItemDto> _allTasks = [];
    private IReadOnlyList<TaskItemDto> _sharedTasks = [];

    public ReportsViewModel(
        ITasksApiService tasksService,
        ProjectService projectService,
        TaskMetaService taskMeta,
        ModeService modeService,
        ISharedProjectsApiService sharedService)
    {
        _tasksService = tasksService;
        _projectService = projectService;
        _taskMeta = taskMeta;
        _modeService = modeService;
        _sharedService = sharedService;
    }

    // ── Tabs ─────────────────────────────────────────────────────────────────

    [ObservableProperty] private string _selectedTab = "general";

    [RelayCommand]
    private void SetTab(string tab) => SelectedTab = tab;

    // ── General tab ──────────────────────────────────────────────────────────

    [ObservableProperty] private string _generalGroupBy = "projects";
    [ObservableProperty] private ObservableCollection<GeneralReportRow> _generalRows = [];

    // Dynamic custom columns
    public ObservableCollection<CustomReportColumn> CustomColumns { get; } = new();
    public AddColumnViewModel AddColumnVm { get; } = new();

    // ── Tables tab ───────────────────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<SavedReport> _savedReports = [];
    [ObservableProperty] private SavedReport? _selectedReport;
    [ObservableProperty] private ObservableCollection<GanttDateHeader> _ganttDateHeaders = [];
    [ObservableProperty] private ObservableCollection<GanttTask> _ganttTasks = [];
    [ObservableProperty] private double _ganttTotalWidth = TimelineDays * DayWidthPx;
    [ObservableProperty] private double _ganttCanvasHeight = 400.0;
    [ObservableProperty] private double _todayLeft;

    // Report configure state
    [ObservableProperty] private ReportConfigViewModel _reportConfig = new();

    // ── Employees tab ────────────────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<EmployeeTaskRow> _employeeRows = [];

    // ── Time-in-columns tab ──────────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<TimeColumnHeader> _timeHeaders = [];
    [ObservableProperty] private ObservableCollection<TimeColumnRow> _timeRows = [];
    [ObservableProperty] private ProjectItem? _selectedTimeProject;
    [ObservableProperty] private BoardItem? _selectedTimeBoard;

    public ObservableCollection<ProjectItem> Projects => _projectService.Projects;

    public ObservableCollection<BoardItem> TimeBoards { get; } = new();

    partial void OnSelectedTimeProjectChanged(ProjectItem? value)
    {
        TimeBoards.Clear();
        if (value is not null)
            foreach (var b in value.Boards)
                TimeBoards.Add(b);
        SelectedTimeBoard = null;
        BuildTimeTab();
    }

    partial void OnSelectedTimeBoardChanged(BoardItem? value) => BuildTimeTab();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public async Task LoadAsync()
    {
        _timelineStart = DateTime.Today.AddDays(-14);

        await ExecuteAsync(async () =>
        {
            _allTasks = await _tasksService.GetBoardAsync();

            // Also load shared project tasks in corporate mode
            if (_modeService.IsCorporate)
            {
                var sharedList = new List<TaskItemDto>();
                foreach (var project in _projectService.Projects)
                {
                    try
                    {
                        var sTasks = await _sharedService.GetProjectTasksAsync(project.Id);
                        foreach (var st in sTasks)
                        {
                            sharedList.Add(new TaskItemDto
                            {
                                Id = st.Id,
                                Title = st.Title,
                                Description = st.Description,
                                Status = st.IsCompleted ? TaskStatusDto.Done :
                                         st.Status == "inprogress" ? TaskStatusDto.InProgress :
                                         st.Status == "review" ? TaskStatusDto.Review : TaskStatusDto.ToDo,
                                Priority = (TaskPriorityDto)st.Priority,
                                DueDate = st.DueDate,
                                CreatedAt = st.CreatedAt,
                                UpdatedAt = st.UpdatedAt
                            });
                        }
                    }
                    catch { }
                }
                _sharedTasks = sharedList;
            }
            else
            {
                _sharedTasks = [];
            }

            BuildGeneralTab();
            BuildTablesTab();
            BuildEmployeesTab();
            BuildTimeTab();
        });
    }

    private IReadOnlyList<TaskItemDto> AllTasksCombined =>
        _allTasks.Concat(_sharedTasks).ToList();

    // ── General tab ──────────────────────────────────────────────────────────

    private List<string> BuildCustomValues(IList<TaskItemDto> tasks)
    {
        return CustomColumns.Select<CustomReportColumn, string>(col => col.DataType switch
        {
            ReportColumnType.AssignedTo =>
                string.Join(", ", tasks.Select(t => _taskMeta.GetAssignee(t.Id).Username)
                                       .Where(u => !string.IsNullOrEmpty(u)).Distinct()) is { Length: > 0 } s ? s : "—",
            ReportColumnType.LastUpdated =>
                tasks.MaxBy(t => t.UpdatedAt)?.UpdatedAt.ToString("dd.MM.yyyy") ?? "—",
            ReportColumnType.DueDate =>
                tasks.Where(t => t.DueDate.HasValue && t.Status != TaskStatusDto.Done)
                     .MinBy(t => t.DueDate)?.DueDate?.ToString("dd.MM.yyyy") ?? "—",
            ReportColumnType.Status =>
                $"{tasks.Count(t => t.Status == TaskStatusDto.Done)}/{tasks.Count} вып.",
            ReportColumnType.Priority =>
                tasks.Any() ? tasks.GroupBy(t => t.Priority).OrderByDescending(g => g.Count())
                     .First().Key switch
                     {
                         TaskPriorityDto.Critical => "Критично!",
                         TaskPriorityDto.High => "Важно",
                         TaskPriorityDto.Normal => "Нормально",
                         TaskPriorityDto.Low => "Не важно",
                         _ => "—"
                     } : "—",
            ReportColumnType.TaskCount => tasks.Count.ToString(),
            ReportColumnType.CreatedAt =>
                tasks.MinBy(t => t.CreatedAt)?.CreatedAt.ToString("dd.MM.yyyy") ?? "—",
            _ => "—"
        }).ToList();
    }

    private void BuildGeneralTab()
    {
        var allTasks = AllTasksCombined;
        var rows = new List<GeneralReportRow>();

        if (GeneralGroupBy == "projects")
        {
            foreach (var project in _projectService.Projects)
            {
                var projectTasks = allTasks
                    .Where(t => _projectService.GetTaskProject(t.Id) == project.Id)
                    .ToList();

                rows.Add(new GeneralReportRow
                {
                    Name = project.Name,
                    OpenTasks = projectTasks.Count(t => t.Status != TaskStatusDto.Done),
                    CompletedTasks = projectTasks.Count(t => t.Status == TaskStatusDto.Done),
                    AssignedTo = string.Join(", ", projectTasks.Select(t => _taskMeta.GetAssignee(t.Id).Username)
                                   .Where(u => !string.IsNullOrEmpty(u)).Distinct()),
                    LastUpdated = projectTasks.MaxBy(t => t.UpdatedAt)?.UpdatedAt.ToString("dd.MM.yyyy"),
                    DueDate = projectTasks.Where(t => t.DueDate.HasValue && t.Status != TaskStatusDto.Done)
                                          .MinBy(t => t.DueDate)?.DueDate?.ToString("dd.MM.yyyy"),
                    CustomValues = BuildCustomValues(projectTasks)
                });
            }
        }
        else if (GeneralGroupBy == "people")
        {
            var grouped = allTasks.GroupBy(t => _taskMeta.GetAssignee(t.Id).Username ?? "Не назначен");
            foreach (var g in grouped)
            {
                var gl = g.ToList();
                rows.Add(new GeneralReportRow
                {
                    Name = g.Key,
                    OpenTasks = gl.Count(t => t.Status != TaskStatusDto.Done),
                    CompletedTasks = gl.Count(t => t.Status == TaskStatusDto.Done),
                    AssignedTo = g.Key,
                    LastUpdated = gl.MaxBy(t => t.UpdatedAt)?.UpdatedAt.ToString("dd.MM.yyyy"),
                    DueDate = gl.Where(t => t.DueDate.HasValue).MinBy(t => t.DueDate)?.DueDate?.ToString("dd.MM.yyyy"),
                    CustomValues = BuildCustomValues(gl)
                });
            }
        }
        else
        {
            var l = allTasks.ToList();
            rows.Add(new GeneralReportRow
            {
                Name = "Все задачи",
                OpenTasks = l.Count(t => t.Status != TaskStatusDto.Done),
                CompletedTasks = l.Count(t => t.Status == TaskStatusDto.Done),
                LastUpdated = l.MaxBy(t => t.UpdatedAt)?.UpdatedAt.ToString("dd.MM.yyyy"),
                DueDate = l.Where(t => t.DueDate.HasValue).MinBy(t => t.DueDate)?.DueDate?.ToString("dd.MM.yyyy"),
                CustomValues = BuildCustomValues(l)
            });
        }

        if (!rows.Any())
            rows.Add(new GeneralReportRow
            {
                Name = "Нет данных",
                CustomValues = BuildCustomValues([])
            });

        GeneralRows = new ObservableCollection<GeneralReportRow>(rows);
    }

    [RelayCommand]
    private void SetGeneralGroupBy(string groupBy)
    {
        GeneralGroupBy = groupBy;
        BuildGeneralTab();
    }

    [RelayCommand]
    private void AddCustomColumn()
    {
        AddColumnVm.ColumnName = string.Empty;
        AddColumnVm.SelectedType = null;
        var dlg = new Views.Reports.AddColumnDialog
        {
            DataContext = AddColumnVm,
            Owner = System.Windows.Application.Current.MainWindow
        };
        if (dlg.ShowDialog() == true && AddColumnVm.IsValid)
        {
            CustomColumns.Add(new CustomReportColumn
            {
                Name = AddColumnVm.ColumnName.Trim(),
                DataType = AddColumnVm.SelectedType!.Type
            });
            BuildGeneralTab();
        }
    }

    [RelayCommand]
    private void RemoveCustomColumn(CustomReportColumn col)
    {
        CustomColumns.Remove(col);
        BuildGeneralTab();
    }

    // ── Tables tab ───────────────────────────────────────────────────────────

    private void BuildTablesTab()
    {
        var all = AllTasksCombined;
        var cfg = ReportConfig;

        IEnumerable<TaskItemDto> Filtered(IEnumerable<TaskItemDto> src)
        {
            var result = src;
            if (cfg.DateFrom.HasValue) result = result.Where(t => t.CreatedAt >= cfg.DateFrom.Value);
            if (cfg.DateTo.HasValue)   result = result.Where(t => t.CreatedAt <= cfg.DateTo.Value.AddDays(1));
            if (!cfg.ShowCompleted)    result = result.Where(t => t.Status != TaskStatusDto.Done);
            if (!cfg.ShowOpen)         result = result.Where(t => t.Status == TaskStatusDto.Done);
            return result;
        }

        var filtered = Filtered(all).ToList();

        var reports = new List<SavedReport>
        {
            new() {
                Name = "Новые за 30 дней", Filter = "new30",
                OpenTasks = filtered.Count(t => t.CreatedAt >= DateTime.Today.AddDays(-30) && t.Status != TaskStatusDto.Done),
                CompletedTasks = filtered.Count(t => t.CreatedAt >= DateTime.Today.AddDays(-30) && t.Status == TaskStatusDto.Done)
            },
            new() {
                Name = "Выполненные за 30 дней", Filter = "done30",
                OpenTasks = 0,
                CompletedTasks = filtered.Count(t => t.Status == TaskStatusDto.Done && t.UpdatedAt >= DateTime.Today.AddDays(-30))
            },
            new() {
                Name = "По проектам", Filter = "byprojects",
                OpenTasks = filtered.Count(t => _projectService.GetTaskProject(t.Id).HasValue && t.Status != TaskStatusDto.Done),
                CompletedTasks = filtered.Count(t => _projectService.GetTaskProject(t.Id).HasValue && t.Status == TaskStatusDto.Done)
            },
            new() {
                Name = "С дедлайном", Filter = "withdue",
                OpenTasks = filtered.Count(t => t.DueDate.HasValue && t.Status != TaskStatusDto.Done),
                CompletedTasks = filtered.Count(t => t.DueDate.HasValue && t.Status == TaskStatusDto.Done)
            }
        };
        SavedReports = new ObservableCollection<SavedReport>(reports);
    }

    [RelayCommand]
    private void OpenReport(SavedReport report)
    {
        SelectedReport = report;
        BuildGantt(report);
    }

    [RelayCommand]
    private void CloseReport() => SelectedReport = null;

    [RelayCommand]
    private void ConfigureReport()
    {
        var dlg = new Views.Reports.ReportConfigDialog { DataContext = ReportConfig };
        if (dlg.ShowDialog() == true)
        {
            BuildTablesTab();
            if (SelectedReport is not null) BuildGantt(SelectedReport);
        }
    }

    private void BuildGantt(SavedReport report)
    {
        _timelineStart = DateTime.Today.AddDays(-7);
        var timelineEnd = _timelineStart.AddDays(TimelineDays);

        var headers = Enumerable.Range(0, TimelineDays).Select(i =>
        {
            var d = _timelineStart.AddDays(i);
            return new GanttDateHeader
            {
                Label = d.Day.ToString(),
                IsToday = d.Date == DateTime.Today,
                IsWeekend = d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
            };
        }).ToList();
        GanttDateHeaders = new ObservableCollection<GanttDateHeader>(headers);

        TodayLeft = (DateTime.Today - _timelineStart).TotalDays * DayWidthPx;
        GanttTotalWidth = TimelineDays * DayWidthPx;

        var all = AllTasksCombined;
        var tasks = report.Filter switch
        {
            "new30" => all.Where(t => t.CreatedAt >= DateTime.Today.AddDays(-30)).ToList(),
            "done30" => all.Where(t => t.Status == TaskStatusDto.Done).ToList(),
            "byprojects" => all.Where(t => _projectService.GetTaskProject(t.Id).HasValue).ToList(),
            "withdue" => all.Where(t => t.DueDate.HasValue).ToList(),
            _ => all.ToList()
        };

        var colorsPool = new[] { "#4CAF50", "#2196F3", "#FF9800", "#9C27B0", "#00BCD4", "#F44336", "#795548" };
        var ganttItems = new List<GanttTask>();
        int row = 0;

        foreach (var t in tasks)
        {
            var start = t.CreatedAt.Date;
            var end = t.DueDate?.Date ?? start.AddDays(3);
            var visStart = start < _timelineStart ? _timelineStart : start;
            var visEnd = end > timelineEnd ? timelineEnd : end;
            if (visEnd < _timelineStart || visStart > timelineEnd) continue;

            var left = (visStart - _timelineStart).TotalDays * DayWidthPx;
            var width = Math.Max(DayWidthPx, (visEnd - visStart).TotalDays * DayWidthPx + DayWidthPx);

            var pid = _projectService.GetTaskProject(t.Id);
            var project = pid.HasValue ? _projectService.Projects.FirstOrDefault(p => p.Id == pid.Value) : null;
            var color = project?.Color ?? colorsPool[row % colorsPool.Length];
            var assignee = _taskMeta.GetAssignee(t.Id).Username;
            var initials = assignee?.Length > 0 ? assignee[0].ToString().ToUpper()
                           : (t.Title.Length > 0 ? t.Title[0].ToString().ToUpper() : "?");

            ganttItems.Add(new GanttTask
            {
                Title = t.Title,
                ProjectName = project?.Name ?? string.Empty,
                Start = start, End = end,
                Color = color,
                IsCompleted = t.Status == TaskStatusDto.Done,
                Initials = initials,
                Left = left,
                BarWidth = width,
                Top = row * 56.0
            });
            row++;
        }

        GanttTasks = new ObservableCollection<GanttTask>(ganttItems);
        GanttCanvasHeight = Math.Max(200, row * 56.0 + 20);
    }

    // ── Employees tab ─────────────────────────────────────────────────────────

    private void BuildEmployeesTab()
    {
        var allTasks = AllTasksCombined;
        var rows = allTasks.Select(t =>
        {
            var pid = _projectService.GetTaskProject(t.Id);
            var project = pid.HasValue ? _projectService.Projects.FirstOrDefault(p => p.Id == pid.Value) : null;
            var bid = _projectService.GetTaskBoard(t.Id);
            var board = bid.HasValue ? project?.Boards.FirstOrDefault(b => b.Id == bid.Value) : null;

            var path = project is not null
                ? (board is not null ? $"{project.Name} / {board.Name}" : project.Name)
                : "Общие задачи";

            var (_, assigneeUsername) = _taskMeta.GetAssignee(t.Id);

            return new EmployeeTaskRow
            {
                Title = t.Title,
                ProjectPath = path,
                AssignedToUsername = assigneeUsername ?? "—",
                StatusText = t.Status switch
                {
                    TaskStatusDto.ToDo => "К выполнению",
                    TaskStatusDto.InProgress => "В работе",
                    TaskStatusDto.Review => "На ревью",
                    TaskStatusDto.Done => "Готово",
                    _ => t.Status.ToString()
                },
                StatusColor = t.Status switch
                {
                    TaskStatusDto.ToDo => "#E53935",
                    TaskStatusDto.InProgress => "#7C4DFF",
                    TaskStatusDto.Review => "#FF9800",
                    TaskStatusDto.Done => "#43A047",
                    _ => "#607D8B"
                },
                PriorityText = t.Priority switch
                {
                    TaskPriorityDto.Critical => "Критично!",
                    TaskPriorityDto.High => "Важно",
                    TaskPriorityDto.Normal => "Нормально",
                    TaskPriorityDto.Low => "Не важно",
                    _ => "—"
                },
                PriorityColor = t.Priority switch
                {
                    TaskPriorityDto.Critical => "#C62828",
                    TaskPriorityDto.High => "#E65100",
                    TaskPriorityDto.Normal => "#2E7D32",
                    TaskPriorityDto.Low => "#757575",
                    _ => "#607D8B"
                },
                CreatedAt = t.CreatedAt,
                DueDate = t.DueDate,
                IsCompleted = t.Status == TaskStatusDto.Done
            };
        }).ToList();

        EmployeeRows = new ObservableCollection<EmployeeTaskRow>(rows);
    }

    // ── Time-in-columns tab ───────────────────────────────────────────────────

    private void BuildTimeTab()
    {
        var pid = SelectedTimeProject?.Id;
        var bid = SelectedTimeBoard?.Id;

        var cols = _projectService.GetColumns(pid, bid);
        if (cols.Count == 0) cols = ProjectService.CreateDefaultColumns();

        var allTasks = AllTasksCombined;
        var tasksForProject = (pid.HasValue
            ? allTasks.Where(t => _projectService.GetTaskProject(t.Id) == pid)
            : allTasks)
            .Where(t => !bid.HasValue || _projectService.GetTaskBoard(t.Id) == bid)
            .ToList();

        var colAvgMinutes = new double[cols.Count];
        var colTaskCount = new int[cols.Count];

        foreach (var t in tasksForProject)
        {
            int currentColIdx = Math.Min(t.Status switch
            {
                TaskStatusDto.ToDo => 0,
                TaskStatusDto.InProgress => 1,
                TaskStatusDto.Review => 2,
                TaskStatusDto.Done => 3,
                _ => 0
            }, cols.Count - 1);

            double totalMinutes = Math.Max(1, (t.UpdatedAt - t.CreatedAt).TotalMinutes);
            double perCol = totalMinutes / (currentColIdx + 1);

            for (int i = 0; i <= currentColIdx && i < cols.Count; i++)
            {
                colAvgMinutes[i] += perCol;
                colTaskCount[i]++;
            }
        }

        var averages = new double[cols.Count];
        for (int i = 0; i < cols.Count; i++)
            averages[i] = colTaskCount[i] > 0 ? colAvgMinutes[i] / colTaskCount[i] : 0;

        double maxAvg = averages.Max() is double m and > 0 ? m : 1;

        TimeHeaders = new ObservableCollection<TimeColumnHeader>(
            cols.Select((c, i) => new TimeColumnHeader
            {
                Name = c.Name,
                Color = c.Color,
                AverageTimeText = FormatMinutes(averages[i]),
                BarHeightPixels = averages[i] / maxAvg * MaxBarHeightPx
            }));

        var rows = tasksForProject.Take(50).Select(t =>
        {
            int currentColIdx = Math.Min(t.Status switch
            {
                TaskStatusDto.ToDo => 0,
                TaskStatusDto.InProgress => 1,
                TaskStatusDto.Review => 2,
                TaskStatusDto.Done => 3,
                _ => 0
            }, cols.Count - 1);

            double totalMinutes = Math.Max(1, (t.UpdatedAt - t.CreatedAt).TotalMinutes);
            double perCol = totalMinutes / (currentColIdx + 1);

            var cells = new ObservableCollection<TimeCell>();
            for (int i = 0; i < cols.Count; i++)
            {
                if (i <= currentColIdx)
                    cells.Add(new TimeCell { Text = FormatMinutes(perCol), IsHighlighted = averages[i] > 0 && perCol > averages[i] * 1.2 });
                else
                    cells.Add(new TimeCell { Text = "0:00:00", IsHighlighted = false });
            }

            return new TimeColumnRow
            {
                TaskTitle = t.Title,
                IsCompleted = t.Status == TaskStatusDto.Done,
                Cells = cells
            };
        }).ToList();

        TimeRows = new ObservableCollection<TimeColumnRow>(rows);
    }

    [RelayCommand]
    private void SetTimeProject(ProjectItem? project)
    {
        SelectedTimeProject = project;
        // OnSelectedTimeProjectChanged fires BuildTimeTab automatically
    }

    [RelayCommand]
    private void SetTimeBoard(BoardItem? board)
    {
        SelectedTimeBoard = board;
        // OnSelectedTimeBoardChanged fires BuildTimeTab automatically
    }

    private static string FormatMinutes(double minutes)
    {
        if (minutes < 1) return "0:00:00";
        var ts = TimeSpan.FromMinutes(minutes);
        if (ts.TotalDays >= 1) return $"{(int)ts.TotalDays} д., {ts.Hours} мин.";
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours} ч., {ts.Minutes} мин.";
        return $"0:{ts.Minutes:00}:{ts.Seconds:00}";
    }

    // ── Export ────────────────────────────────────────────────────────────────

    [RelayCommand]
    private void DownloadReport()
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("Проект;Открытых задач;Выполненных задач");
            foreach (var row in GeneralRows)
                sb.AppendLine($"{row.Name};{row.OpenTasks};{row.CompletedTasks}");
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"report_{DateTime.Now:yyyyMMdd_HHmm}.csv");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            MessageBox.Show($"Отчёт сохранён:\n{path}", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
