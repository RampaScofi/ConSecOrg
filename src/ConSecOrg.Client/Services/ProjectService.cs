using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConSecOrg.Client.Services;

public sealed class BoardItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#607D8B";
    public List<ColumnModel> Columns { get; set; } = [];
}

public sealed class ColumnModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#607D8B";
    public int Order { get; set; }
    // Maps column to a server status ("todo"|"inprogress"|"review"|"done"). Null = fully custom.
    public string? StatusKey { get; set; }
}

public sealed class ProjectItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#2196F3";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    // ObservableCollection so the sidebar ItemsControl reacts to board deletions
    public ObservableCollection<BoardItem> Boards { get; set; } = [];
    public List<ColumnModel> Columns { get; set; } = [];
}

public sealed class TaskProjectMapping
{
    public string? ProjectId { get; set; }
    public string? BoardId { get; set; }
    public string? ColumnId { get; set; }
}

public sealed class ProjectService
{
    private static string BaseDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ConSecOrg");

    private string _userDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ConSecOrg", "default");

    private string FilePath => Path.Combine(_userDir, "projects.json");
    private string TaskMapPath => Path.Combine(_userDir, "task_projects.json");
    private string GlobalColumnsPath => Path.Combine(_userDir, "global_columns.json");

    private readonly ObservableCollection<ProjectItem> _projects = [];
    private Dictionary<string, TaskProjectMapping> _taskMap = [];
    private List<ColumnModel> _globalColumns = [];

    public event Action? Changed;
    public ObservableCollection<ProjectItem> Projects => _projects;

    // Default 4 columns (YouGile-style colors)
    public static List<ColumnModel> CreateDefaultColumns() =>
    [
        new() { Name = "К выполнению", Color = "#E53935", Order = 0, StatusKey = "todo" },
        new() { Name = "В работе",      Color = "#7C4DFF", Order = 1, StatusKey = "inprogress" },
        new() { Name = "На ревью",      Color = "#FF9800", Order = 2, StatusKey = "review" },
        new() { Name = "Готово",         Color = "#43A047", Order = 3, StatusKey = "done" },
    ];

    // Color palette for new custom columns
    private static readonly string[] ColumnColors =
    [
        "#0097A7", "#E91E63", "#1976D2", "#8E24AA",
        "#00897B", "#FB8C00", "#C62828", "#37474F"
    ];

    public ProjectService() { }

    /// <summary>Load user-specific projects. Call after login with the authenticated user ID.</summary>
    public void LoadForUser(Guid userId)
    {
        _userDir = Path.Combine(BaseDir, userId.ToString());
        _projects.Clear();
        _taskMap.Clear();
        _globalColumns.Clear();
        Load();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var loaded = JsonSerializer.Deserialize<List<ProjectItem>>(File.ReadAllText(FilePath)) ?? [];
                foreach (var p in loaded) _projects.Add(p);
            }

            if (File.Exists(TaskMapPath))
            {
                var text = File.ReadAllText(TaskMapPath);
                try
                {
                    _taskMap = JsonSerializer.Deserialize<Dictionary<string, TaskProjectMapping>>(text) ?? [];
                }
                catch
                {
                    var old = JsonSerializer.Deserialize<Dictionary<string, string>>(text) ?? [];
                    _taskMap = old.ToDictionary(kv => kv.Key, kv => new TaskProjectMapping { ProjectId = kv.Value });
                }
            }

            if (File.Exists(GlobalColumnsPath))
                _globalColumns = JsonSerializer.Deserialize<List<ColumnModel>>(File.ReadAllText(GlobalColumnsPath)) ?? [];
        }
        catch { }
    }

    private void Save()
    {
        var dir = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(dir);
        var opts = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(FilePath, JsonSerializer.Serialize(_projects, opts));
        File.WriteAllText(TaskMapPath, JsonSerializer.Serialize(_taskMap));
        File.WriteAllText(GlobalColumnsPath, JsonSerializer.Serialize(_globalColumns, opts));
        Changed?.Invoke();
    }

    // ── Projects ───────────────────────────────────────────────────────────────

    public void CreateProject(string name, string color)
    {
        _projects.Add(new ProjectItem
        {
            Name = name,
            Color = color,
            Columns = CreateDefaultColumns()
        });
        Save();
    }

    public void DeleteProject(Guid id)
    {
        var p = _projects.FirstOrDefault(x => x.Id == id);
        if (p != null) _projects.Remove(p);
        var toRemove = _taskMap.Where(kv => kv.Value.ProjectId == id.ToString()).Select(kv => kv.Key).ToList();
        foreach (var key in toRemove) _taskMap.Remove(key);
        Save();
    }

    // ── Boards (sub-sections within a project) ────────────────────────────────

    public void CreateBoard(Guid projectId, string name, string color = "#607D8B")
    {
        var project = _projects.FirstOrDefault(p => p.Id == projectId);
        if (project == null) return;
        project.Boards.Add(new BoardItem
        {
            Name = name,
            Color = color,
            Columns = CreateDefaultColumns()
        });
        Save();
    }

    public void DeleteBoard(Guid boardId)
    {
        foreach (var p in _projects)
        {
            var board = p.Boards.FirstOrDefault(b => b.Id == boardId);
            if (board != null) p.Boards.Remove(board);
        }
        foreach (var m in _taskMap.Values.Where(m => m.BoardId == boardId.ToString()))
            m.BoardId = null;
        Save();
    }

    public Guid? GetProjectForBoard(Guid boardId)
        => _projects.FirstOrDefault(p => p.Boards.Any(b => b.Id == boardId))?.Id;

    // ── Columns ───────────────────────────────────────────────────────────────

    /// <summary>Returns columns for the given project/board context.
    /// Falls back to global default columns if none are defined.</summary>
    public List<ColumnModel> GetColumns(Guid? projectId, Guid? boardId)
    {
        if (boardId.HasValue)
        {
            var board = _projects.SelectMany(p => p.Boards).FirstOrDefault(b => b.Id == boardId.Value);
            if (board != null)
            {
                if (board.Columns.Count == 0) board.Columns = CreateDefaultColumns();
                return board.Columns.OrderBy(c => c.Order).ToList();
            }
        }

        if (projectId.HasValue)
        {
            var project = _projects.FirstOrDefault(p => p.Id == projectId.Value);
            if (project != null)
            {
                if (project.Columns.Count == 0) project.Columns = CreateDefaultColumns();
                return project.Columns.OrderBy(c => c.Order).ToList();
            }
        }

        // Global columns (all-tasks view)
        if (_globalColumns.Count == 0) _globalColumns = CreateDefaultColumns();
        return _globalColumns.OrderBy(c => c.Order).ToList();
    }

    public ColumnModel AddColumn(Guid? projectId, Guid? boardId, string name)
    {
        var columns = GetColumnsRef(projectId, boardId);
        var col = new ColumnModel
        {
            Name = name,
            Color = ColumnColors[columns.Count % ColumnColors.Length],
            Order = columns.Count,
            StatusKey = null
        };
        columns.Add(col);
        Save();
        return col;
    }

    public void RenameColumn(Guid columnId, string newName, Guid? projectId, Guid? boardId)
    {
        var col = GetColumnsRef(projectId, boardId).FirstOrDefault(c => c.Id == columnId);
        if (col != null) { col.Name = newName; Save(); }
    }

    public void DeleteColumn(Guid columnId, Guid? projectId, Guid? boardId)
    {
        var cols = GetColumnsRef(projectId, boardId);
        cols.RemoveAll(c => c.Id == columnId);
        // Reassign order
        for (int i = 0; i < cols.Count; i++) cols[i].Order = i;
        // Remove task assignments to this column
        foreach (var m in _taskMap.Values.Where(m => m.ColumnId == columnId.ToString()))
            m.ColumnId = null;
        Save();
    }

    public void SetColumnColor(Guid columnId, string color, Guid? projectId, Guid? boardId)
    {
        var col = GetColumnsRef(projectId, boardId).FirstOrDefault(c => c.Id == columnId);
        if (col != null) { col.Color = color; Save(); }
    }

    public void ReorderColumns(Guid? projectId, Guid? boardId, List<Guid> orderedIds)
    {
        var cols = GetColumnsRef(projectId, boardId);
        for (int i = 0; i < orderedIds.Count; i++)
        {
            var c = cols.FirstOrDefault(x => x.Id == orderedIds[i]);
            if (c != null) c.Order = i;
        }
        Save();
    }

    private List<ColumnModel> GetColumnsRef(Guid? projectId, Guid? boardId)
    {
        if (boardId.HasValue)
        {
            var board = _projects.SelectMany(p => p.Boards).FirstOrDefault(b => b.Id == boardId.Value);
            if (board != null) return board.Columns;
        }
        if (projectId.HasValue)
        {
            var project = _projects.FirstOrDefault(p => p.Id == projectId.Value);
            if (project != null) return project.Columns;
        }
        return _globalColumns;
    }

    // ── Task assignments ───────────────────────────────────────────────────────

    public Guid? GetTaskProject(Guid taskId)
    {
        if (_taskMap.TryGetValue(taskId.ToString(), out var m) && Guid.TryParse(m.ProjectId, out var g))
            return g;
        return null;
    }

    public Guid? GetTaskBoard(Guid taskId)
    {
        if (_taskMap.TryGetValue(taskId.ToString(), out var m) && Guid.TryParse(m.BoardId, out var g))
            return g;
        return null;
    }

    public Guid? GetTaskColumn(Guid taskId)
    {
        if (_taskMap.TryGetValue(taskId.ToString(), out var m) && Guid.TryParse(m.ColumnId, out var g))
            return g;
        return null;
    }

    public void AssignTask(Guid taskId, Guid? projectId, Guid? boardId = null, Guid? columnId = null)
    {
        var key = taskId.ToString();
        if (projectId.HasValue || columnId.HasValue)
            _taskMap[key] = new TaskProjectMapping
            {
                ProjectId = projectId?.ToString(),
                BoardId   = boardId?.ToString(),
                ColumnId  = columnId?.ToString()
            };
        else
            _taskMap.Remove(key);
        Save();
    }

    public void AssignTaskToColumn(Guid taskId, Guid? columnId)
    {
        var key = taskId.ToString();
        if (!_taskMap.TryGetValue(key, out var m))
            m = new TaskProjectMapping();
        m.ColumnId = columnId?.ToString();
        _taskMap[key] = m;
        Save();
    }
}
