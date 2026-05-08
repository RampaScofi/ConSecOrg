using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Infrastructure.Api;
using ConSecOrg.Client.Services;
using ConSecOrg.Client.ViewModels.Base;
using ConSecOrg.Shared.DTOs.Notes;
using ConSecOrg.Shared.Enums;
using Markdig;
using System.Collections.ObjectModel;

namespace ConSecOrg.Client.ViewModels.Notes;

public partial class NoteEditorViewModel(
    INotesApiService notesService,
    NavigationService navigation,
    NotificationService notifications,
    NoteMetaService noteMetaService) : BasePageViewModel
{
    public override string Title => NoteId.HasValue ? "Редактировать заметку" : "Новая заметка";

    [ObservableProperty] private Guid? _noteId;
    [ObservableProperty] private string _noteTitle = string.Empty;
    [ObservableProperty] private string _markdownContent = string.Empty;
    [ObservableProperty] private string _renderedHtml = string.Empty;
    [ObservableProperty] private SecurityLevelDto _securityLevel = SecurityLevelDto.Internal;
    [ObservableProperty] private bool _isPinned;

    // Tags
    [ObservableProperty] private string _tagsInput = string.Empty;
    [ObservableProperty] private ObservableCollection<string> _tags = [];

    // Priority
    [ObservableProperty] private NotePriority _notePriority = NotePriority.Normal;

    // Timer fields (always in LOCAL time for the user)
    [ObservableProperty] private bool _timerEnabled;
    [ObservableProperty] private DateTime _timerDate = DateTime.Today.AddDays(1);
    [ObservableProperty] private int _timerHour = 23;
    [ObservableProperty] private int _timerMinute = 0;

    // Exact UTC expiry stored for presets (preserves seconds; not truncated to minute boundary)
    private DateTime? _presetExpiresUtc;

    /// <summary>Режим таймера: "Relative" (через N сек/мин/часов) или "Absolute" (конкретная дата+время).</summary>
    [ObservableProperty] private string _timerMode = "Relative";

    public bool IsTimerRelative => TimerMode == "Relative";
    public bool IsTimerAbsolute => TimerMode == "Absolute";

    partial void OnTimerModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsTimerRelative));
        OnPropertyChanged(nameof(IsTimerAbsolute));
        OnPropertyChanged(nameof(ExpiresAt));
    }

    /// <summary>UTC deadline. For presets returns the exact second; for "Своё время..." reconstructs from UI fields.</summary>
    public DateTime? ExpiresAt
    {
        get
        {
            if (!TimerEnabled) return null;
            // Preset: return precise UTC value (no truncation to minute)
            if (_presetExpiresUtc.HasValue && SelectedDurationLabel != "Своё время...")
                return _presetExpiresUtc.Value;
            // Custom: reconstruct from UI date/hour/minute fields → convert local → UTC
            var local = TimerDate.Date.AddHours(TimerHour).AddMinutes(TimerMinute);
            return DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();
        }
    }

    public int[] Hours { get; } = Enumerable.Range(0, 24).ToArray();
    public int[] Minutes { get; } = Enumerable.Range(0, 60).ToArray();
    public Array SecurityLevels => Enum.GetValues(typeof(SecurityLevelDto));
    public Array NotePriorities => Enum.GetValues(typeof(NotePriority));

    public string[] PriorityLabels { get; } = ["Низкий", "Обычный", "Высокий", "Критичный"];

    // Quick presets — добавлены короткие интервалы 30 сек / 1 мин / 2 мин / 5 мин
    public string[] DurationLabels { get; } =
    [
        "30 секунд", "1 минута", "2 минуты", "5 минут", "10 минут", "30 минут",
        "1 час", "3 часа", "6 часов", "1 день", "3 дня", "7 дней", "Своё время..."
    ];

    [ObservableProperty] private string _selectedDurationLabel = "1 минута";
    public bool ShowCustomTimer => SelectedDurationLabel == "Своё время...";

    partial void OnTagsInputChanged(string value)
    {
        Tags = new ObservableCollection<string>(
            value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .Where(t => !string.IsNullOrWhiteSpace(t)));
    }

    partial void OnSelectedDurationLabelChanged(string value)
    {
        var seconds = value switch
        {
            "30 секунд" => 30,
            "1 минута"  => 60,
            "2 минуты"  => 120,
            "5 минут"   => 300,
            "10 минут"  => 600,
            "30 минут"  => 1800,
            "1 час"     => 3600,
            "3 часа"    => 3 * 3600,
            "6 часов"   => 6 * 3600,
            "1 день"    => 24 * 3600,
            "3 дня"     => 3 * 24 * 3600,
            "7 дней"    => 7 * 24 * 3600,
            _ => -1
        };
        if (seconds > 0)
        {
            // Store exact UTC time for the preset (preserves seconds precision)
            _presetExpiresUtc = DateTime.UtcNow.AddSeconds(seconds);
            // Update display fields to show the equivalent local time in the UI
            var local = _presetExpiresUtc.Value.ToLocalTime();
            TimerDate = local.Date;
            TimerHour = local.Hour;
            TimerMinute = local.Minute;
        }
        else
        {
            _presetExpiresUtc = null;  // "Своё время..." — use UI fields
        }
        OnPropertyChanged(nameof(ExpiresAt));
        OnPropertyChanged(nameof(ShowCustomTimer));
    }

    partial void OnMarkdownContentChanged(string value)
        => RenderedHtml = Markdown.ToHtml(value ?? string.Empty);

    public void LoadNote(NoteDto note)
    {
        NoteId = note.Id;
        NoteTitle = note.Title;
        MarkdownContent = note.Content;
        SecurityLevel = note.SecurityLevel;
        IsPinned = note.IsPinned;

        // Load local meta (tags + priority)
        var meta = noteMetaService.GetMeta(note.Id);
        NotePriority = meta.Priority;
        TagsInput = string.Join(", ", meta.Tags);

        if (note.ExpiresAt.HasValue)
        {
            // ExpiresAt из БД хранится в UTC — конвертируем в локальное время для отображения
            var expiresLocal = note.ExpiresAt.Value.Kind switch
            {
                DateTimeKind.Utc => note.ExpiresAt.Value.ToLocalTime(),
                DateTimeKind.Local => note.ExpiresAt.Value,
                _ => DateTime.SpecifyKind(note.ExpiresAt.Value, DateTimeKind.Utc).ToLocalTime()
            };
            TimerEnabled = true;
            TimerDate = expiresLocal.Date;
            TimerHour = expiresLocal.Hour;
            TimerMinute = expiresLocal.Minute;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(NoteTitle)) { ErrorMessage = "Введите заголовок заметки."; return; }

        await ExecuteAsync(async () =>
        {
            Guid savedId;
            if (NoteId.HasValue)
            {
                await notesService.UpdateNoteAsync(NoteId.Value, new UpdateNoteRequestDto
                {
                    Title = NoteTitle,
                    Content = MarkdownContent,
                    SecurityLevel = SecurityLevel,
                    IsPinned = IsPinned
                });
                if (TimerEnabled && ExpiresAt.HasValue)
                    await notesService.SetDestructionTimerAsync(NoteId.Value, ExpiresAt.Value);
                savedId = NoteId.Value;
            }
            else
            {
                savedId = await notesService.CreateNoteAsync(new CreateNoteRequestDto
                {
                    Title = NoteTitle,
                    Content = MarkdownContent,
                    SecurityLevel = SecurityLevel,
                    IsPinned = IsPinned,
                    ExpiresAt = ExpiresAt
                });
                notifications.Success("Заметка создана", $"«{NoteTitle}» сохранена.");
            }

            // Save local meta
            noteMetaService.SetMeta(savedId, new NoteMetaEntry
            {
                Tags = Tags.ToList(),
                Priority = NotePriority
            });

            navigation.GoBack();
        });
    }

    [RelayCommand]
    private void Cancel() => navigation.GoBack();

    [RelayCommand]
    private void SetTimerMode(string mode) => TimerMode = mode;

    [RelayCommand]
    private void RemoveTag(string tag)
    {
        var list = Tags.Where(t => t != tag).ToList();
        Tags = new ObservableCollection<string>(list);
        TagsInput = string.Join(", ", list);
    }
}
