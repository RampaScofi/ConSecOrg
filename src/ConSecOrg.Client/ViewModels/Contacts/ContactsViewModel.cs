using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Infrastructure.Api;
using ConSecOrg.Client.Services;
using ConSecOrg.Client.ViewModels.Base;
using ConSecOrg.Client.ViewModels.Company;
using ConSecOrg.Shared.DTOs.Contacts;
using ConSecOrg.Shared.DTOs.Projects;
using ConSecOrg.Shared.DTOs.Users;
using System.Collections.ObjectModel;

namespace ConSecOrg.Client.ViewModels.Contacts;

public partial class ContactsViewModel : BasePageViewModel
{
    private readonly IContactsApiService _contactsService;
    private readonly IUserSearchApiService _userSearchService;
    private readonly ISharedProjectsApiService _sharedProjectsService;
    private readonly NavigationService _navigation;
    private readonly ModeService _modeService;
    private readonly SharedChatPanelViewModel _chatPanel;

    public ContactsViewModel(IContactsApiService contactsService,
        IUserSearchApiService userSearchService,
        ISharedProjectsApiService sharedProjectsService,
        NavigationService navigation,
        ModeService modeService,
        SharedChatPanelViewModel chatPanel)
    {
        _contactsService = contactsService;
        _userSearchService = userSearchService;
        _sharedProjectsService = sharedProjectsService;
        _navigation = navigation;
        _modeService = modeService;
        _chatPanel = chatPanel;
    }

    public override string Title => "Контакты";
    public bool IsCorporate => _modeService.IsCorporate;

    // ── Contact list ───────────────────────────────────────────────────────────
    private List<ContactDto> _allContacts = [];
    [ObservableProperty] private ObservableCollection<ContactDto> _contacts = [];
    [ObservableProperty] private string _filterQuery = string.Empty;
    [ObservableProperty] private ContactDto? _selectedContact;

    partial void OnFilterQueryChanged(string value) => ApplyFilter();

    [RelayCommand] private void ClearFilter() => FilterQuery = string.Empty;

    private void ApplyFilter()
    {
        var q = FilterQuery.Trim().ToLowerInvariant();
        var filtered = string.IsNullOrEmpty(q)
            ? _allContacts
            : _allContacts.Where(c =>
                (c.Name?.ToLowerInvariant().Contains(q) ?? false) ||
                (c.Email?.ToLowerInvariant().Contains(q) ?? false) ||
                (c.Phone?.ToLowerInvariant().Contains(q) ?? false)).ToList();
        Contacts = new ObservableCollection<ContactDto>(filtered);
    }
    [ObservableProperty] private bool _isEditing;

    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private string? _editEmail;
    [ObservableProperty] private string? _editPhone;
    [ObservableProperty] private string? _editNotes;

    public bool IsCreatingNew => IsEditing && SelectedContact is null;
    public bool ShowEmptyState => SelectedContact is null && !IsCreatingNew;

    partial void OnIsEditingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsCreatingNew));
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    partial void OnSelectedContactChanged(ContactDto? value)
    {
        OnPropertyChanged(nameof(IsCreatingNew));
        OnPropertyChanged(nameof(ShowEmptyState));
        // Hide shared projects panel when switching contacts
        ShowContactSharedProjects = false;
        ContactSharedProjects.Clear();
    }

    // ── User search ────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _showUserSearch;
    [ObservableProperty] private string _userSearchQuery = string.Empty;
    [ObservableProperty] private ObservableCollection<UserSearchDto> _searchResults = [];
    [ObservableProperty] private string? _searchStatusText;

    // ── Contact requests ───────────────────────────────────────────────────────
    [ObservableProperty] private bool _showRequests;
    [ObservableProperty] private ObservableCollection<ContactRequestDto> _pendingRequests = [];
    [ObservableProperty] private int _pendingCount;
    public bool HasNoPendingRequests => PendingRequests.Count == 0;

    partial void OnPendingRequestsChanged(ObservableCollection<ContactRequestDto> value)
        => OnPropertyChanged(nameof(HasNoPendingRequests));

    // ── Shared projects panel ──────────────────────────────────────────────────
    [ObservableProperty] private bool _showContactSharedProjects;
    [ObservableProperty] private bool _isLoadingSharedProjects;
    [ObservableProperty] private ObservableCollection<SharedProjectDto> _contactSharedProjects = [];
    [ObservableProperty] private string? _sharedProjectsStatusText;

    public override async Task OnNavigatedToAsync()
    {
        NotificationMessage = string.Empty;
        await LoadAsync();
        if (IsCorporate)
        {
            await LoadPendingRequestsAsync();
            await AutoAddMutualContactsAsync();
        }
    }

    // When someone accepts our outgoing request, auto-add them as a contact
    private async Task AutoAddMutualContactsAsync()
    {
        try
        {
            var sent = await _userSearchService.GetSentRequestsAsync();
            var accepted = sent.Where(r => r.Status == "Accepted").ToList();
            if (accepted.Count == 0) return;

            var existingLinkedIds = _allContacts
                .Where(c => c.LinkedUserId.HasValue)
                .Select(c => c.LinkedUserId!.Value)
                .ToHashSet();

            bool anyAdded = false;
            foreach (var req in accepted)
            {
                if (req.ReceiverId == Guid.Empty) continue;
                if (existingLinkedIds.Contains(req.ReceiverId)) continue;

                await _contactsService.CreateContactAsync(new CreateContactRequestDto
                {
                    Name = req.ReceiverUsername,
                    Email = req.ReceiverEmail,
                    LinkedUserId = req.ReceiverId
                });
                anyAdded = true;
            }
            if (anyAdded) await LoadAsync();
        }
        catch { /* network unavailable */ }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            var result = await _contactsService.GetContactsAsync();
            _allContacts = [.. result];
            ApplyFilter();
        });
    }

    [RelayCommand]
    private async Task LoadPendingRequestsAsync()
    {
        try
        {
            var pending = await _userSearchService.GetPendingRequestsAsync();
            PendingRequests = new ObservableCollection<ContactRequestDto>(pending);
            PendingCount = pending.Count;
        }
        catch { /* network unavailable */ }
    }

    [RelayCommand]
    private void SelectContact(ContactDto contact)
    {
        SelectedContact = contact;
        EditName = contact.Name;
        EditEmail = contact.Email;
        EditPhone = contact.Phone;
        EditNotes = contact.Notes;
        IsEditing = false;
        ShowUserSearch = false;
        ShowRequests = false;
    }

    [RelayCommand] private void StartEdit() => IsEditing = true;
    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        if (SelectedContact is not null)
            SelectContact(SelectedContact);
        else
        {
            EditName = string.Empty;
            EditEmail = EditPhone = EditNotes = null;
        }
    }

    [RelayCommand]
    private void NewContact()
    {
        SelectedContact = null;
        EditName = EditEmail = EditPhone = EditNotes = string.Empty;
        IsEditing = true;
        ShowUserSearch = false;
    }

    [RelayCommand]
    private void ToggleUserSearch()
    {
        ShowUserSearch = !ShowUserSearch;
        ShowRequests = false;
        if (!ShowUserSearch)
        {
            UserSearchQuery = string.Empty;
            SearchResults.Clear();
            SearchStatusText = null;
        }
    }

    [RelayCommand]
    private void ToggleRequests()
    {
        ShowRequests = !ShowRequests;
        ShowUserSearch = false;
    }

    [RelayCommand]
    private async Task SearchUsersAsync()
    {
        if (string.IsNullOrWhiteSpace(UserSearchQuery) || UserSearchQuery.Length < 2)
        {
            SearchStatusText = "Введите не менее 2 символов";
            return;
        }
        SearchStatusText = null;
        try
        {
            var results = await _userSearchService.SearchUsersAsync(UserSearchQuery);
            SearchResults = new ObservableCollection<UserSearchDto>(results);
            if (results.Count == 0)
                SearchStatusText = "Пользователи не найдены";
        }
        catch
        {
            SearchStatusText = "Ошибка поиска";
        }
    }

    [RelayCommand]
    private async Task SendRequestAsync(UserSearchDto user)
    {
        try
        {
            // Only send a friend request — contact appears for both after acceptance
            await _userSearchService.SendContactRequestAsync(new SendContactRequestDto { ReceiverId = user.Id });
            SearchStatusText = $"Запрос отправлен {user.Username}. Ожидайте подтверждения.";
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (msg.Contains("409") || msg.Contains("Conflict"))
                SearchStatusText = "Запрос уже отправлен";
            else if (msg.Contains("403") || msg.Contains("Forbidden") || msg.Contains("encryption key") || msg.Contains("log in"))
                SearchStatusText = "Ошибка сессии: войдите в систему заново.";
            else
                SearchStatusText = $"Ошибка: {msg}";
        }
    }

    [RelayCommand]
    private async Task AcceptRequestAsync(ContactRequestDto request)
    {
        try
        {
            await _userSearchService.AcceptContactRequestAsync(request.Id);
            PendingRequests.Remove(request);
            PendingCount = PendingRequests.Count;
            // Add accepted user as a contact with LinkedUserId so chat works
            if (!_allContacts.Any(c => c.LinkedUserId == request.SenderId))
                await _contactsService.CreateContactAsync(new CreateContactRequestDto
                {
                    Name = request.SenderUsername,
                    Email = request.SenderEmail,
                    LinkedUserId = request.SenderId
                });
            await LoadAsync();
        }
        catch { /* ignore */ }
    }

    [RelayCommand]
    private async Task DeclineRequestAsync(ContactRequestDto request)
    {
        try
        {
            await _userSearchService.DeclineContactRequestAsync(request.Id);
            PendingRequests.Remove(request);
            PendingCount = PendingRequests.Count;
        }
        catch { /* ignore */ }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EditName)) return;
        try
        {
            if (SelectedContact is null)
                await _contactsService.CreateContactAsync(new CreateContactRequestDto { Name = EditName, Email = EditEmail, Phone = EditPhone, Notes = EditNotes });
            else
                await _contactsService.UpdateContactAsync(SelectedContact.Id, new UpdateContactRequestDto { Name = EditName, Email = EditEmail, Phone = EditPhone, Notes = EditNotes });
            IsEditing = false;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (msg.Contains("403") || msg.Contains("Forbidden") || msg.Contains("encryption key") || msg.Contains("log in"))
                NotificationMessage = "Ошибка сессии: войдите в систему заново (сессия устарела).";
            else
                NotificationMessage = $"Ошибка сохранения: {msg}";
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(ContactDto contact)
    {
        await _contactsService.DeleteContactAsync(contact.Id);
        _allContacts.RemoveAll(c => c.Id == contact.Id);
        Contacts.Remove(contact);
        if (SelectedContact?.Id == contact.Id) SelectedContact = null;
    }

    // ── Три точки меню ────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task OpenChatAsync(ContactDto? contact)
    {
        if (contact is null) return;

        if (contact.LinkedUserId.HasValue)
        {
            _chatPanel.OpenDirect(contact.LinkedUserId.Value, contact.Name);
            return;
        }

        // Fallback: search by email to find the linked user
        if (!string.IsNullOrWhiteSpace(contact.Email))
        {
            try
            {
                var results = await _userSearchService.SearchUsersAsync(contact.Email);
                var match = results.FirstOrDefault(u =>
                    u.Email.Equals(contact.Email, StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                {
                    contact.LinkedUserId = match.Id;
                    _chatPanel.OpenDirect(match.Id, contact.Name);
                    return;
                }
            }
            catch { /* search failed */ }
        }

        NotificationMessage = $"Пользователь {contact.Name} не найден в системе.";
    }

    [ObservableProperty] private string _notificationMessage = string.Empty;

    [RelayCommand]
    private void PinContact(ContactDto? contact)
    {
        if (contact is null) return;
        contact.IsPinned = !contact.IsPinned;
        var sorted = _allContacts
            .OrderByDescending(c => c.IsPinned)
            .ThenBy(c => c.Name)
            .ToList();
        _allContacts = sorted;
        ApplyFilter();
    }

    [RelayCommand]
    private async Task ShowSharedProjectsAsync(ContactDto? contact)
    {
        if (contact is null) return;

        if (!contact.LinkedUserId.HasValue)
        {
            NotificationMessage = $"Пользователь {contact.Name} не связан с аккаунтом в системе.";
            return;
        }

        // Toggle off if already showing for this contact
        if (ShowContactSharedProjects)
        {
            ShowContactSharedProjects = false;
            ContactSharedProjects.Clear();
            SharedProjectsStatusText = null;
            return;
        }

        IsLoadingSharedProjects = true;
        ShowContactSharedProjects = true;
        ContactSharedProjects.Clear();
        SharedProjectsStatusText = null;

        try
        {
            var allProjects = await _sharedProjectsService.GetMyProjectsAsync();
            var mutual = allProjects
                .Where(p => p.OwnerUserId == contact.LinkedUserId.Value
                         || p.Members.Any(m => m.UserId == contact.LinkedUserId.Value))
                .ToList();

            ContactSharedProjects = new ObservableCollection<SharedProjectDto>(mutual);
            SharedProjectsStatusText = mutual.Count == 0 ? "Нет совместных проектов" : null;
        }
        catch
        {
            SharedProjectsStatusText = "Не удалось загрузить проекты";
        }
        finally
        {
            IsLoadingSharedProjects = false;
        }
    }

    [RelayCommand]
    private void HideSharedProjects()
    {
        ShowContactSharedProjects = false;
        ContactSharedProjects.Clear();
        SharedProjectsStatusText = null;
    }

    [RelayCommand]
    private void NavigateToSharedProject(SharedProjectDto? project)
    {
        if (project is null) return;
        _navigation.NavigateTo<SharedProjectBoardViewModel>(vm =>
        {
            _ = vm.OpenAsync(project);
        });
    }

    [RelayCommand]
    private void EditContact(ContactDto? contact)
    {
        if (contact is null) return;
        SelectContact(contact);
        StartEdit();
    }
}
