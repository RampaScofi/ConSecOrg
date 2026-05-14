using CommunityToolkit.Mvvm.ComponentModel;
using ConSecOrg.Shared.Enums;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConSecOrg.Client.Services;

public partial class SubTaskItem : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TaskPriorityDto Priority { get; set; } = TaskPriorityDto.Normal;
    public DateTime? DueDate { get; set; }

    [ObservableProperty] private bool _isCompleted;
    [ObservableProperty][JsonIgnore] private bool _isExpanded;
    [ObservableProperty][JsonIgnore] private string _newChildTitle = string.Empty;

    public ObservableCollection<SubTaskItem> Children { get; set; } = [];

    [JsonIgnore] public bool HasChildren => Children.Count > 0;
    [JsonIgnore] public int ChildCount => Children.Count;
    [JsonIgnore] public int CompletedChildCount => Children.Count(c => c.IsCompleted);
    [JsonIgnore] public string ChildProgressText => HasChildren ? $"{CompletedChildCount}/{ChildCount}" : string.Empty;

    [JsonIgnore]
    public string PriorityText => Priority switch
    {
        TaskPriorityDto.Low => "Не важно",
        TaskPriorityDto.Normal => "Нормально",
        TaskPriorityDto.High => "Важно",
        TaskPriorityDto.Critical => "Критично!",
        _ => "Нормально"
    };

    [JsonIgnore]
    public string PriorityColor => Priority switch
    {
        TaskPriorityDto.Low => "#616161",
        TaskPriorityDto.Normal => "#2E7D32",
        TaskPriorityDto.High => "#E65100",
        TaskPriorityDto.Critical => "#C62828",
        _ => "#2E7D32"
    };

    [JsonIgnore]
    public string PriorityBackground => Priority switch
    {
        TaskPriorityDto.Low => "#1A9E9E9E",
        TaskPriorityDto.Normal => "#1A4CAF50",
        TaskPriorityDto.High => "#1AFF9800",
        TaskPriorityDto.Critical => "#1AF44336",
        _ => "#1A4CAF50"
    };

    [JsonIgnore] public bool HasDueDate => DueDate.HasValue;
    [JsonIgnore] public bool IsDueDatePast => DueDate.HasValue && DueDate.Value.Date < DateTime.Today;

    partial void OnIsCompletedChanged(bool value) => RefreshChildStats();
    partial void OnIsExpandedChanged(bool value) { }

    public void RefreshChildStats()
    {
        OnPropertyChanged(nameof(HasChildren));
        OnPropertyChanged(nameof(ChildCount));
        OnPropertyChanged(nameof(CompletedChildCount));
        OnPropertyChanged(nameof(ChildProgressText));
    }

    public void RefreshDisplayProps()
    {
        OnPropertyChanged(nameof(PriorityText));
        OnPropertyChanged(nameof(PriorityColor));
        OnPropertyChanged(nameof(PriorityBackground));
        OnPropertyChanged(nameof(HasDueDate));
        OnPropertyChanged(nameof(IsDueDatePast));
    }
}

public sealed class TaskMetaEntry
{
    public List<string> Tags { get; set; } = [];
    public string Description { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public List<SubTaskItem> SubTasks { get; set; } = [];
    public string? AssignedToUserId { get; set; }
    public string? AssignedToUsername { get; set; }
}

public sealed class ChatMessageEntry
{
    public string SenderUsername { get; set; } = string.Empty;
    public string SenderUserId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public string? AttachmentPath { get; set; }
    public string? AttachmentFileName { get; set; }
}

public sealed class TaskMetaService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ConSecOrg", "task_meta.json");

    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    private Dictionary<string, TaskMetaEntry> _meta = [];

    public event Action? Changed;

    public TaskMetaService() { Load(); }

    private void Load()
    {
        try
        {
            if (File.Exists(FilePath))
                _meta = JsonSerializer.Deserialize<Dictionary<string, TaskMetaEntry>>(
                    File.ReadAllText(FilePath), _jsonOptions) ?? [];
        }
        catch { }
    }

    private void Save()
    {
        var dir = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(_meta, _jsonOptions));
        Changed?.Invoke();
    }

    public TaskMetaEntry GetMeta(Guid taskId)
        => _meta.TryGetValue(taskId.ToString(), out var m) ? m : new TaskMetaEntry();

    public void SetMeta(Guid taskId, TaskMetaEntry entry)
    {
        _meta[taskId.ToString()] = entry;
        Save();
    }

    public void SetCompleted(Guid taskId, bool completed)
    {
        var key = taskId.ToString();
        if (!_meta.TryGetValue(key, out var entry)) entry = new TaskMetaEntry();
        entry.IsCompleted = completed;
        _meta[key] = entry;
        Save();
    }

    public bool IsCompleted(Guid taskId)
        => _meta.TryGetValue(taskId.ToString(), out var m) && m.IsCompleted;

    public List<string> GetTags(Guid taskId) => GetMeta(taskId).Tags;
    public string GetDescription(Guid taskId) => GetMeta(taskId).Description;
    public List<SubTaskItem> GetSubTasks(Guid taskId) => GetMeta(taskId).SubTasks;

    public SubTaskItem AddSubTask(Guid taskId, string title)
    {
        var key = taskId.ToString();
        if (!_meta.TryGetValue(key, out var entry)) entry = new TaskMetaEntry();
        var sub = new SubTaskItem { Title = title };
        entry.SubTasks.Add(sub);
        _meta[key] = entry;
        Save();
        return sub;
    }

    public SubTaskItem AddChildSubTask(Guid taskId, Guid parentId, string title)
    {
        var key = taskId.ToString();
        if (!_meta.TryGetValue(key, out var entry)) entry = new TaskMetaEntry();
        var parent = FindSubTask(entry.SubTasks, parentId);
        if (parent == null) return AddSubTask(taskId, title);
        var child = new SubTaskItem { Title = title };
        parent.Children.Add(child); // ObservableCollection fires UI update automatically
        _meta[key] = entry;
        Save();
        return child;
    }

    public void UpdateSubTask(Guid taskId, SubTaskItem updated)
    {
        var key = taskId.ToString();
        if (!_meta.TryGetValue(key, out var entry)) return;
        var sub = FindSubTask(entry.SubTasks, updated.Id);
        if (sub == null) return;
        sub.Title = updated.Title;
        sub.Description = updated.Description;
        sub.Priority = updated.Priority;
        sub.DueDate = updated.DueDate;
        Save();
    }

    public void ToggleSubTask(Guid taskId, Guid subId)
    {
        var key = taskId.ToString();
        if (!_meta.TryGetValue(key, out var entry)) return;
        var sub = FindSubTask(entry.SubTasks, subId);
        if (sub == null) return;
        sub.IsCompleted = !sub.IsCompleted;
        Save();
    }

    public void RemoveSubTask(Guid taskId, Guid subId)
    {
        var key = taskId.ToString();
        if (!_meta.TryGetValue(key, out var entry)) return;
        if (!RemoveFromList(entry.SubTasks, subId))
            RemoveFromChildren(entry.SubTasks, subId);
        Save();
    }

    private static SubTaskItem? FindSubTask(IEnumerable<SubTaskItem> list, Guid id)
    {
        foreach (var s in list)
        {
            if (s.Id == id) return s;
            var found = FindSubTask(s.Children, id);
            if (found != null) return found;
        }
        return null;
    }

    private static bool RemoveFromList(IList<SubTaskItem> list, Guid id)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Id == id) { list.RemoveAt(i); return true; }
        }
        return false;
    }

    private static void RemoveFromChildren(IEnumerable<SubTaskItem> list, Guid id)
    {
        foreach (var s in list)
        {
            if (RemoveFromList(s.Children, id)) return;
            RemoveFromChildren(s.Children, id);
        }
    }

    public void SetAssignee(Guid taskId, string? userId, string? username)
    {
        var key = taskId.ToString();
        if (!_meta.TryGetValue(key, out var entry)) entry = new TaskMetaEntry();
        entry.AssignedToUserId = userId;
        entry.AssignedToUsername = username;
        _meta[key] = entry;
        Save();
    }

    public (string? UserId, string? Username) GetAssignee(Guid taskId)
    {
        var m = GetMeta(taskId);
        return (m.AssignedToUserId, m.AssignedToUsername);
    }

    public void Remove(Guid taskId) { _meta.Remove(taskId.ToString()); Save(); }
}
