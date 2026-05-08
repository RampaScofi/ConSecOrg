using System.IO;
using System.Text.Json;

namespace ConSecOrg.Client.Services;

public enum NotePriority { Low = 0, Normal = 1, High = 2, Critical = 3 }

public sealed class NoteMetaEntry
{
    public List<string> Tags { get; set; } = [];
    public NotePriority Priority { get; set; } = NotePriority.Normal;
}

public sealed class NoteMetaService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ConSecOrg", "note_meta.json");

    private Dictionary<string, NoteMetaEntry> _meta = [];

    public event Action? Changed;

    public NoteMetaService() { Load(); }

    private void Load()
    {
        try
        {
            if (File.Exists(FilePath))
                _meta = JsonSerializer.Deserialize<Dictionary<string, NoteMetaEntry>>(File.ReadAllText(FilePath)) ?? [];
        }
        catch { }
    }

    private void Save()
    {
        var dir = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(_meta, new JsonSerializerOptions { WriteIndented = true }));
        Changed?.Invoke();
    }

    public NoteMetaEntry GetMeta(Guid noteId)
        => _meta.TryGetValue(noteId.ToString(), out var m) ? m : new NoteMetaEntry();

    public void SetMeta(Guid noteId, NoteMetaEntry entry)
    {
        _meta[noteId.ToString()] = entry;
        Save();
    }

    public void SetTags(Guid noteId, IEnumerable<string> tags)
    {
        var key = noteId.ToString();
        if (!_meta.TryGetValue(key, out var entry)) entry = new NoteMetaEntry();
        entry.Tags = tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToList();
        _meta[key] = entry;
        Save();
    }

    public void SetPriority(Guid noteId, NotePriority priority)
    {
        var key = noteId.ToString();
        if (!_meta.TryGetValue(key, out var entry)) entry = new NoteMetaEntry();
        entry.Priority = priority;
        _meta[key] = entry;
        Save();
    }

    public List<string> GetTags(Guid noteId) => GetMeta(noteId).Tags;
    public NotePriority GetPriority(Guid noteId) => GetMeta(noteId).Priority;

    public void Remove(Guid noteId) { _meta.Remove(noteId.ToString()); Save(); }
}
