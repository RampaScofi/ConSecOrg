using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Infrastructure.Api;
using ConSecOrg.Client.Infrastructure.Local;
using ConSecOrg.Client.Services;
using ConSecOrg.Client.ViewModels.Base;
using ConSecOrg.Client.Views.Dialogs;
using ConSecOrg.Shared.DTOs.Notes;
using ConSecOrg.Shared.Enums;
using System.Collections.ObjectModel;

namespace ConSecOrg.Client.ViewModels.Notes;

public partial class NotesListViewModel : BasePageViewModel
{
    private readonly INotesApiService notesService;
    private readonly NavigationService navigation;
    private readonly NotificationService notifications;
    private readonly ModeService modeService;
    private readonly LocalUserStore userStore;
    private readonly UserSettingsService userSettings;

    public NotesListViewModel(
        INotesApiService notesService,
        NavigationService navigation,
        NotificationService notifications,
        ModeService modeService,
        LocalUserStore userStore,
        UserSettingsService userSettings)
    {
        this.notesService = notesService;
        this.navigation = navigation;
        this.notifications = notifications;
        this.modeService = modeService;
        this.userStore = userStore;
        this.userSettings = userSettings;
        notifications.NoteTimerExpired += OnNoteTimerExpired;
    }

    private void OnNoteTimerExpired(Guid noteId, string _)
    {
        _allNotes = _allNotes.Where(n => n.Id != noteId).ToList();
        // BeginInvoke: non-blocking, avoids reentrancy when called from within a Dispatcher.Invoke chain
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(ApplySortAndFilter);
    }

    public override string Title => "Заметки";

    [ObservableProperty] private ObservableCollection<NoteDto> _notes = [];
    [ObservableProperty] private NoteDto? _selectedNote;
    [ObservableProperty] private string _searchText = string.Empty;

    // Sort
    [ObservableProperty] private string _sortMode = "date_desc"; // date_desc | date_asc | title | security
    public static string[] SortModes { get; } = ["date_desc", "date_asc", "title", "security"];
    public static string[] SortLabels { get; } = ["Новые", "Старые", "A–Я", "Уровень"];

    // Security level filter — null = all
    [ObservableProperty] private SecurityLevelDto? _securityLevelFilter;

    // Timer filter
    [ObservableProperty] private bool _filterHasTimer;

    // View mode
    [ObservableProperty] private bool _isListView;

    private IReadOnlyList<NoteDto> _allNotes = [];

    public bool HasActiveFilter => SecurityLevelFilter.HasValue || FilterHasTimer;

    public override async Task OnNavigatedToAsync() => await LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            var result = await notesService.GetNotesAsync(pageSize: 200);
            _allNotes = result.Items;
            ApplySortAndFilter();
        });
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            await LoadAsync();
            return;
        }
        await ExecuteAsync(async () =>
        {
            var result = await notesService.SearchNotesAsync(SearchText);
            _allNotes = result;
            ApplySortAndFilter();
        });
    }

    [RelayCommand]
    private void SetSecurityFilter(string? levelStr)
    {
        if (levelStr == null || !Enum.TryParse<SecurityLevelDto>(levelStr, out var level))
        {
            SecurityLevelFilter = null;
        }
        else
        {
            SecurityLevelFilter = SecurityLevelFilter == level ? null : level;
        }
        OnPropertyChanged(nameof(HasActiveFilter));
        ApplySortAndFilter();
    }

    [RelayCommand]
    private void ToggleTimerFilter()
    {
        FilterHasTimer = !FilterHasTimer;
        OnPropertyChanged(nameof(HasActiveFilter));
        ApplySortAndFilter();
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SecurityLevelFilter = null;
        FilterHasTimer = false;
        OnPropertyChanged(nameof(HasActiveFilter));
        ApplySortAndFilter();
    }

    partial void OnSortModeChanged(string value) => ApplySortAndFilter();

    private void ApplySortAndFilter()
    {
        IEnumerable<NoteDto> filtered = _allNotes;

        if (SecurityLevelFilter.HasValue)
            filtered = filtered.Where(n => n.SecurityLevel == SecurityLevelFilter.Value);

        if (FilterHasTimer)
            filtered = filtered.Where(n => n.ExpiresAt.HasValue);

        var sorted = SortMode switch
        {
            "date_asc" => filtered.OrderBy(n => n.UpdatedAt),
            "title"    => filtered.OrderBy(n => n.Title, StringComparer.CurrentCultureIgnoreCase),
            "security" => filtered.OrderByDescending(n => n.SecurityLevel),
            _          => filtered.OrderByDescending(n => n.UpdatedAt),
        };
        Notes = new ObservableCollection<NoteDto>(sorted);
    }

    [RelayCommand]
    private void SetSort(string mode) => SortMode = mode;

    [RelayCommand]
    private void ToggleViewMode() => IsListView = !IsListView;

    [RelayCommand]
    private void CreateNote() => navigation.NavigateTo<NoteEditorViewModel>();

    /// <summary>Одиночный клик по карточке — открыть детальный просмотр.</summary>
    [RelayCommand]
    private void OpenNote(NoteDto note)
    {
        if (note is null) return;
        var requestedEdit = NoteDetailDialog.Show(note, userStore, userSettings, modeService);
        if (requestedEdit) EditNote(note);
    }

    /// <summary>Открыть редактор. Для Confidential/Secret требуется PIN (если установлен).</summary>
    [RelayCommand]
    private void EditNote(NoteDto note)
    {
        if (note is null) return;
        if (RequiresPin(note))
        {
            var levelLabel = note.SecurityLevel == SecurityLevelDto.Secret ? "секретной" : "конфиденциальной";
            if (!PinPromptDialog.Prompt(userStore, userSettings, modeService,
                    "Подтвердите доступ",
                    $"Введите PIN для редактирования {levelLabel} заметки"))
                return;
        }
        navigation.NavigateTo<NoteEditorViewModel>(vm => vm.LoadNote(note));
    }

    private bool RequiresPin(NoteDto note)
    {
        if (note.SecurityLevel != SecurityLevelDto.Confidential
            && note.SecurityLevel != SecurityLevelDto.Secret) return false;
        // Персональный — PIN из LocalUserStore. Корпоративный — PIN из UserSettings.
        if (modeService.IsPersonal) return userStore.HasPin;
        return !string.IsNullOrEmpty(userSettings.Current.NotesPinVerifier);
    }

    [RelayCommand]
    private async Task DeleteNoteAsync(NoteDto note)
    {
        if (!ConfirmDialog.Show("Удалить заметку",
                $"Удалить заметку «{note.Title}»?\nДанные будут надёжно уничтожены."))
            return;

        // Дополнительная PIN-проверка для удаления Confidential/Secret
        if (RequiresPin(note))
        {
            if (!PinPromptDialog.Prompt(userStore, userSettings, modeService,
                    "Подтвердите удаление",
                    "Для удаления защищённой заметки требуется PIN"))
                return;
        }

        await ExecuteAsync(async () =>
        {
            await notesService.DeleteNoteAsync(note.Id);
            Notes.Remove(note);
            notifications.Success("Удалено", $"Заметка «{note.Title}» надёжно уничтожена.");
        });
    }
}
