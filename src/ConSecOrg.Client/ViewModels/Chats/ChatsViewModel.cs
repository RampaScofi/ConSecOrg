using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Infrastructure.Api;
using ConSecOrg.Client.Services;
using ConSecOrg.Shared.DTOs.Contacts;
using ConSecOrg.Shared.DTOs.Projects;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace ConSecOrg.Client.ViewModels.Chats;

// ── Message status ────────────────────────────────────────────────────────────
public enum MsgStatus { Sending, Sent, Read, Failed }

// ── Message view model ────────────────────────────────────────────────────────
public partial class ChatMsgVm : ObservableObject
{
    [ObservableProperty] private Guid _id;
    [ObservableProperty] private string _senderUsername = string.Empty;
    [ObservableProperty] private string _text = string.Empty;
    [ObservableProperty] private DateTime _sentAt;
    [ObservableProperty] private bool _isMine;
    [ObservableProperty] private string? _attachmentFileId;
    [ObservableProperty] private string? _attachmentFileName;
    [ObservableProperty] private long? _attachmentSize;
    [ObservableProperty] private string? _attachmentUrl;
    [ObservableProperty] private MsgStatus _status = MsgStatus.Sent;
    [ObservableProperty] private string? _senderAvatarBase64;

    partial void OnSenderAvatarBase64Changed(string? value)
    {
        OnPropertyChanged(nameof(HasAvatar));
        OnPropertyChanged(nameof(AvatarImage));
    }

    public string SenderLetter => SenderUsername.Length > 0 ? SenderUsername[0].ToString().ToUpper() : "?";
    public bool HasAvatar => !string.IsNullOrEmpty(SenderAvatarBase64);
    public System.Windows.Media.Imaging.BitmapImage? AvatarImage => Services.AvatarCacheService.Base64ToImage(SenderAvatarBase64);
    public string TimeDisplay => SentAt.ToLocalTime().ToString("HH:mm");
    public bool HasAttachment => !string.IsNullOrEmpty(AttachmentFileId);
    public bool IsNonImageAttachment => HasAttachment && !IsImageAttachment;
    public bool IsImageAttachment => HasAttachment &&
        (AttachmentFileName?.EndsWith(".png", StringComparison.OrdinalIgnoreCase) == true ||
         AttachmentFileName?.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) == true ||
         AttachmentFileName?.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) == true ||
         AttachmentFileName?.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) == true ||
         AttachmentFileName?.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) == true ||
         AttachmentFileName?.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) == true);

    public string FileSizeDisplay => AttachmentSize.HasValue
        ? AttachmentSize.Value < 1024 * 1024
            ? $"{AttachmentSize.Value / 1024.0:F1} КБ"
            : $"{AttachmentSize.Value / (1024.0 * 1024):F1} МБ"
        : string.Empty;

    // Derived status booleans for XAML binding
    public bool IsSending => Status == MsgStatus.Sending;
    public bool IsSent    => Status == MsgStatus.Sent;
    public bool IsRead    => Status == MsgStatus.Read;
    public bool IsFailed  => Status == MsgStatus.Failed;

    partial void OnStatusChanged(MsgStatus value)
    {
        OnPropertyChanged(nameof(IsSending));
        OnPropertyChanged(nameof(IsSent));
        OnPropertyChanged(nameof(IsRead));
        OnPropertyChanged(nameof(IsFailed));
    }
}

// ── Chat list item (message OR date separator) ────────────────────────────────
public class ChatListItem
{
    public ChatMsgVm? Message   { get; init; }
    public string?   DateLabel  { get; init; }
    public bool IsSeparator => DateLabel is not null;
    public bool IsMessage   => Message  is not null;
}

// ── Chat summary VM (with observable UnreadCount) ─────────────────────────────
public partial class ChatSummaryVm : ObservableObject
{
    public string  ChatId    { get; init; } = string.Empty;
    public string  ChatType  { get; init; } = string.Empty;
    public Guid?   ProjectId    { get; init; }
    public Guid?   OtherUserId  { get; init; }
    public Guid?   GroupChatId  { get; init; }
    public Guid?   OwnerUserId  { get; init; }

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private int      _unreadCount;
    [ObservableProperty] private string   _lastMessageText = string.Empty;
    [ObservableProperty] private DateTime? _lastMessageAt;
    [ObservableProperty] private string   _lastSenderUsername = string.Empty;
    [ObservableProperty] private bool     _isPinned;

    public bool HasUnread => UnreadCount > 0;
    partial void OnUnreadCountChanged(int value) => OnPropertyChanged(nameof(HasUnread));

    // Whether current user is owner (set by ChatsViewModel after load)
    public bool IsGroupOwner { get; set; }
    public bool IsGroupChat  => ChatType == "group";
    public bool IsDirectChat => ChatType == "direct";

    public static ChatSummaryVm FromDto(ChatSummaryDto dto, Guid? currentUserId = null) => new()
    {
        ChatId           = dto.ChatId,
        ChatType         = dto.ChatType,
        ProjectId        = dto.ProjectId,
        OtherUserId      = dto.OtherUserId,
        GroupChatId      = dto.GroupChatId,
        OwnerUserId      = dto.OwnerUserId,
        Title            = dto.Title,
        LastMessageText  = dto.LastMessageText,
        LastMessageAt    = dto.LastMessageAt,
        LastSenderUsername = dto.LastSenderUsername,
        UnreadCount      = dto.UnreadCount,
        IsGroupOwner     = dto.ChatType == "group" && dto.OwnerUserId.HasValue
                           && dto.OwnerUserId == currentUserId
    };
}

// ── Create group chat dialog VM ───────────────────────────────────────────────
public partial class CreateGroupChatVm : ObservableObject
{
    [ObservableProperty] private string _groupName = string.Empty;
    [ObservableProperty] private bool   _isOpen;
    public ObservableCollection<ContactMemberVm> Contacts { get; } = new();
    public List<Guid> SelectedMemberIds =>
        Contacts.Where(c => c.IsSelected && c.LinkedUserId.HasValue)
                .Select(c => c.LinkedUserId!.Value).ToList();
}

public partial class ContactMemberVm : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public Guid? LinkedUserId { get; set; }
    [ObservableProperty] private bool _isSelected;
}

// ── Member item in Manage Members dialog ──────────────────────────────────────
public partial class ChatMemberItemVm : ObservableObject
{
    public Guid   UserId       { get; init; }
    public string Username     { get; init; } = string.Empty;
    [ObservableProperty] private bool _isOwner;
    [ObservableProperty] private bool _isCurrentUser;
    [ObservableProperty] private bool _canManage;

    public string OwnerBadge   => IsOwner ? "Владелец" : string.Empty;
    public bool   ShowOwnerBadge => IsOwner;
    partial void OnIsOwnerChanged(bool value) => OnPropertyChanged(nameof(ShowOwnerBadge));
}

// ── Main ChatsViewModel ───────────────────────────────────────────────────────
public partial class ChatsViewModel : ConSecOrg.Client.ViewModels.Base.BasePageViewModel
{
    private readonly IChatApiService _api;
    private readonly IContactsApiService _contactsApi;
    private readonly IUserSearchApiService _userSearchApi;
    private readonly Services.SharedProjectsHubClient _hub;
    private readonly SessionService _session;
    private readonly ModeService _mode;
    private readonly Services.AvatarCacheService _avatarCache;

    public override string Title => "Чаты";

    // ── State ─────────────────────────────────────────────────────────────────
    [ObservableProperty] private bool    _isLoadingChats;
    [ObservableProperty] private bool    _isLoadingMessages;
    [ObservableProperty] private string? _statusText;

    // ── Chat list ─────────────────────────────────────────────────────────────
    public ObservableCollection<ChatSummaryVm> AllChats { get; } = new();
    [ObservableProperty] private ChatSummaryVm? _activeChatSummary;

    // ── Active chat ───────────────────────────────────────────────────────────
    [ObservableProperty] private string _activeChatTitle = string.Empty;
    [ObservableProperty] private string _newMessage = string.Empty;
    [ObservableProperty] private bool   _hasChatOpen;

    // Rendered items: messages interleaved with date separators
    public ObservableCollection<ChatListItem> ChatItems { get; } = new();

    // Raw messages (used to rebuild ChatItems)
    private readonly List<ChatMsgVm> _rawMessages = [];
    private readonly HashSet<Guid>   _pendingOptimisticIds = [];

    // Active chat context
    private Guid? _activeProjectId;
    private Guid? _activeDirectUserId;
    private Guid? _activeGroupChatId;

    // ── Emoji ─────────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isEmojiPickerOpen;
    public static readonly string[] CommonEmojis =
        ConSecOrg.Client.ViewModels.Company.SharedChatPanelViewModel.CommonEmojis;

    // ── Group chat creation ───────────────────────────────────────────────────
    public CreateGroupChatVm CreateGroupVm { get; } = new();
    [ObservableProperty] private string? _groupCreateError;

    // ── Rename dialog ─────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _isRenameDialogOpen;
    [ObservableProperty] private string _renameValue = string.Empty;
    [ObservableProperty] private string? _renameError;
    private ChatSummaryVm? _renamingChat;

    // ── Members dialog ────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isMembersDialogOpen;
    [ObservableProperty] private string? _membersError;
    [ObservableProperty] private string  _addMemberUsername = string.Empty;
    public ObservableCollection<ChatMemberItemVm> MemberItems { get; } = new();
    private ChatSummaryVm? _managingChat;

    // ── Pinned chats (local storage) ──────────────────────────────────────────
    private HashSet<string> _pinnedChatIds = [];
    private string PinnedChatsFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ConSecOrg",
        _session.CurrentUser?.Id.ToString() ?? "local",
        "pinned_chats.json");

    // ── Scroll helper ─────────────────────────────────────────────────────────
    public event Action? ScrollToBottomRequested;

    public ChatsViewModel(IChatApiService api, IContactsApiService contactsApi,
        IUserSearchApiService userSearchApi,
        Services.SharedProjectsHubClient hub, SessionService session, ModeService mode,
        Services.AvatarCacheService avatarCache)
    {
        _api = api;
        _contactsApi = contactsApi;
        _userSearchApi = userSearchApi;
        _hub = hub;
        _session = session;
        _mode = mode;
        _avatarCache = avatarCache;
        _hub.ChatMessageReceived += OnIncomingMessage;
        _hub.MessagesRead += OnMessagesRead;
        _hub.MessageDeleted += OnMessageDeleted;
    }

    public override async Task OnNavigatedToAsync() => await LoadChatsAsync();

    // ── Load chats list ───────────────────────────────────────────────────────
    [RelayCommand]
    private async Task LoadChatsAsync()
    {
        IsLoadingChats = true;
        StatusText = null;
        LoadPinnedChats();
        try
        {
            await _hub.EnsureConnectedAsync();
            var myId = _session.CurrentUser?.Id;
            var chats = await _api.GetChatsAsync();
            AllChats.Clear();
            foreach (var c in chats)
            {
                var vm = ChatSummaryVm.FromDto(c, myId);
                vm.IsPinned = _pinnedChatIds.Contains(vm.ChatId);
                AllChats.Add(vm);
            }
            // Pinned chats first, then by last message time
            SortChats();
            if (AllChats.Count == 0) StatusText = "Нет активных чатов";
        }
        catch (Exception ex) { StatusText = $"Ошибка загрузки: {ex.Message}"; }
        finally { IsLoadingChats = false; }
    }

    private void SortChats()
    {
        var sorted = AllChats
            .OrderByDescending(c => c.IsPinned)
            .ThenByDescending(c => c.LastMessageAt ?? DateTime.MinValue)
            .ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            var cur = AllChats.IndexOf(sorted[i]);
            if (cur != i) AllChats.Move(cur, i);
        }
    }

    // ── Open a chat from the list ─────────────────────────────────────────────
    [RelayCommand]
    private async Task OpenChatAsync(ChatSummaryVm? summary)
    {
        if (summary is null) return;
        ActiveChatSummary = summary;
        ActiveChatTitle   = summary.Title;
        HasChatOpen       = true;
        _rawMessages.Clear();
        ChatItems.Clear();
        _activeProjectId    = summary.ProjectId;
        _activeDirectUserId = summary.OtherUserId;
        _activeGroupChatId  = summary.GroupChatId;
        StatusText = null;

        // Mark chat as read locally and persist to server
        summary.UnreadCount = 0;
        _ = _api.MarkReadAsync(summary.ChatId);

        // Notify partner for direct chats (fires read receipt via SignalR)
        if (summary.ChatType == "direct" && summary.OtherUserId.HasValue)
            _ = _hub.NotifyDirectReadAsync(summary.OtherUserId.Value);

        // Join SignalR group for real-time messages
        if (summary.GroupChatId.HasValue)
            await _hub.JoinGroupChatAsync(summary.GroupChatId.Value);

        IsLoadingMessages = true;
        try
        {
            IReadOnlyList<ChatMessageDto> msgs = summary.ChatType switch
            {
                "project" when summary.ProjectId.HasValue =>
                    await _api.GetProjectMessagesAsync(summary.ProjectId.Value),
                "direct" when summary.OtherUserId.HasValue =>
                    await _api.GetDirectMessagesAsync(summary.OtherUserId.Value),
                "group" when summary.GroupChatId.HasValue =>
                    await _api.GetGroupMessagesAsync(summary.GroupChatId.Value),
                _ => []
            };
            var vms = msgs.Select(MapVm).ToList();
            foreach (var vm in vms) _rawMessages.Add(vm);
            RebuildChatItems();
            ScrollToBottomRequested?.Invoke();
            // загружаем аватарки в фоне — не блокируем UI
            _ = Task.Run(async () =>
            {
                foreach (var (m, vm) in msgs.Zip(vms))
                    await EnrichWithAvatarAsync(vm, m.SenderUserId);
            });
        }
        catch (Exception ex) { StatusText = $"Ошибка загрузки сообщений: {ex.Message}"; }
        finally { IsLoadingMessages = false; }
    }

    // Open a direct chat with a specific user (called from ContactsViewModel)
    public async Task OpenDirectAsync(Guid otherUserId, string username)
    {
        var summary = new ChatSummaryVm
        {
            ChatId      = $"user:{otherUserId:N}",
            ChatType    = "direct",
            OtherUserId = otherUserId,
            Title       = username
        };
        AllChats.Insert(0, summary);
        await OpenChatAsync(summary);
    }

    [RelayCommand]
    private void CloseChat()
    {
        HasChatOpen = false;
        ActiveChatSummary = null;
        _rawMessages.Clear();
        ChatItems.Clear();
    }

    // ── Send message ──────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task SendMessageAsync()
    {
        var text = NewMessage?.Trim();
        if (string.IsNullOrEmpty(text)) return;
        if (_activeProjectId is null && _activeDirectUserId is null && _activeGroupChatId is null) return;

        NewMessage = string.Empty;
        var optimisticId = Guid.NewGuid();
        var optimistic = new ChatMsgVm
        {
            Id             = optimisticId,
            SenderUsername = _session.CurrentUser?.Username ?? "Я",
            Text           = text,
            SentAt         = DateTime.UtcNow,
            IsMine         = true,
            Status         = MsgStatus.Sending
        };
        _rawMessages.Add(optimistic);
        _pendingOptimisticIds.Add(optimisticId);
        AppendOrRebuildWithMessage(optimistic);
        ScrollToBottomRequested?.Invoke();

        try
        {
            await _api.SendMessageAsync(new SendChatMessageDto
            {
                ProjectId    = _activeProjectId,
                ToUserId     = _activeDirectUserId,
                GroupChatId  = _activeGroupChatId,
                Text         = text
            });
            // Server confirmation comes via SignalR echo (replaces optimistic entry)
        }
        catch
        {
            optimistic.Status = MsgStatus.Failed;
            _pendingOptimisticIds.Remove(optimisticId);
            NewMessage = text;
        }
    }

    // ── Attach file ───────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task AttachFileAsync()
    {
        if (_activeProjectId is null && _activeDirectUserId is null && _activeGroupChatId is null) return;

        var dlg = new OpenFileDialog
        {
            Title  = "Выберите файл для отправки",
            Filter = "Все файлы|*.*|Изображения|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp|Документы|*.pdf;*.doc;*.docx;*.txt;*.xlsx",
            CheckFileExists = true
        };
        if (dlg.ShowDialog() != true) return;

        var filePath = dlg.FileName;
        if (new FileInfo(filePath).Length > 50 * 1024 * 1024)
        {
            System.Windows.MessageBox.Show("Размер файла превышает 50 МБ.", "Файл слишком большой",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        StatusText = $"Загрузка файла {Path.GetFileName(filePath)}...";
        try
        {
            var (fileId, fileName, url, size) = await _api.UploadChatFileAsync(filePath);
            await _api.SendMessageAsync(new SendChatMessageDto
            {
                ProjectId       = _activeProjectId,
                ToUserId        = _activeDirectUserId,
                GroupChatId     = _activeGroupChatId,
                Text            = string.Empty,
                AttachmentFileId   = fileId,
                AttachmentFileName = fileName,
                AttachmentSize     = size
            });
            StatusText = null;
        }
        catch (Exception ex) { StatusText = $"Ошибка отправки файла: {ex.Message}"; }
    }

    // ── Download file ─────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task DownloadFileAsync(ChatMsgVm? msg)
    {
        if (msg is null || string.IsNullOrEmpty(msg.AttachmentFileId)) return;

        var dlg = new SaveFileDialog
        {
            FileName = msg.AttachmentFileName ?? "file",
            Title    = "Сохранить файл как",
            Filter   = "Все файлы|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        StatusText = $"Скачивание {msg.AttachmentFileName}...";
        try
        {
            await _api.DownloadFileAsync(msg.AttachmentFileId, dlg.FileName);
            StatusText = $"Файл сохранён: {dlg.FileName}";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
        }
        catch (Exception ex) { StatusText = $"Ошибка скачивания: {ex.Message}"; }
    }

    // ── Emoji ─────────────────────────────────────────────────────────────────
    [RelayCommand] private void ToggleEmojiPicker() => IsEmojiPickerOpen = !IsEmojiPickerOpen;
    [RelayCommand] private void InsertEmoji(string emoji) { NewMessage += emoji; IsEmojiPickerOpen = false; }

    // ── Group chat creation ───────────────────────────────────────────────────
    [RelayCommand]
    private async Task OpenCreateGroupAsync()
    {
        GroupCreateError = null;
        CreateGroupVm.GroupName = string.Empty;
        CreateGroupVm.Contacts.Clear();
        try
        {
            var contacts = await _contactsApi.GetContactsAsync();
            foreach (var c in contacts.Where(c => c.LinkedUserId.HasValue))
                CreateGroupVm.Contacts.Add(new ContactMemberVm { Name = c.Name, LinkedUserId = c.LinkedUserId });
        }
        catch { }
        CreateGroupVm.IsOpen = true;
    }

    [RelayCommand]
    private async Task ConfirmCreateGroupAsync()
    {
        if (string.IsNullOrWhiteSpace(CreateGroupVm.GroupName))
        {
            GroupCreateError = "Введите название группы"; return;
        }
        GroupCreateError = null;
        try
        {
            var created = await _api.CreateGroupChatAsync(new CreateGroupChatDto
            {
                Name      = CreateGroupVm.GroupName.Trim(),
                MemberIds = CreateGroupVm.SelectedMemberIds
            });
            CreateGroupVm.IsOpen = false;
            var summary = new ChatSummaryVm
            {
                ChatId      = $"group:{created.Id:N}",
                ChatType    = "group",
                GroupChatId = created.Id,
                Title       = created.Name
            };
            AllChats.Insert(0, summary);
            await OpenChatAsync(summary);
        }
        catch (Exception ex) { GroupCreateError = ex.Message; }
    }

    [RelayCommand] private void CancelCreateGroup() => CreateGroupVm.IsOpen = false;

    // ── Pin / Unpin ───────────────────────────────────────────────────────────
    [RelayCommand]
    private void TogglePinChat(ChatSummaryVm? chat)
    {
        if (chat is null) return;
        chat.IsPinned = !chat.IsPinned;
        if (chat.IsPinned) _pinnedChatIds.Add(chat.ChatId);
        else               _pinnedChatIds.Remove(chat.ChatId);
        SavePinnedChats();
        SortChats();
    }

    // ── Rename ────────────────────────────────────────────────────────────────
    [RelayCommand]
    private void OpenRenameChat(ChatSummaryVm? chat)
    {
        if (chat is null || chat.ChatType != "group") return;
        _renamingChat   = chat;
        RenameValue     = chat.Title;
        RenameError     = null;
        IsRenameDialogOpen = true;
    }

    [RelayCommand]
    private async Task ConfirmRenameChatAsync()
    {
        if (string.IsNullOrWhiteSpace(RenameValue)) { RenameError = "Введите название"; return; }
        if (_renamingChat?.GroupChatId is null) return;
        RenameError = null;
        try
        {
            await _api.RenameGroupChatAsync(_renamingChat.GroupChatId.Value, RenameValue.Trim());
            _renamingChat.Title = RenameValue.Trim();
            if (ActiveChatSummary?.ChatId == _renamingChat.ChatId)
                ActiveChatTitle = _renamingChat.Title;
            IsRenameDialogOpen = false;
        }
        catch (Exception ex) { RenameError = ex.Message; }
    }

    [RelayCommand] private void CancelRenameChat() => IsRenameDialogOpen = false;

    // ── Delete chat ───────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task DeleteChatAsync(ChatSummaryVm? chat)
    {
        if (chat is null) return;
        var msg = chat.ChatType == "group"
            ? $"Удалить групповой чат «{chat.Title}»? Все сообщения будут потеряны."
            : $"Удалить переписку с «{chat.Title}»?";
        if (System.Windows.MessageBox.Show(msg, "Удалить чат",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning) != System.Windows.MessageBoxResult.Yes) return;

        if (chat.ChatType == "group" && chat.GroupChatId.HasValue)
        {
            try { await _api.DeleteGroupChatAsync(chat.GroupChatId.Value); }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Ошибка", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }
        }

        AllChats.Remove(chat);
        if (ActiveChatSummary?.ChatId == chat.ChatId) CloseChat();
    }

    // ── Manage members (group chats) ──────────────────────────────────────────
    [RelayCommand]
    private async Task OpenManageMembersAsync(ChatSummaryVm? chat)
    {
        if (chat is null || !chat.GroupChatId.HasValue) return;
        _managingChat = chat;
        MemberItems.Clear();
        MembersError = null;
        AddMemberUsername = string.Empty;
        try
        {
            var dto = await _api.GetGroupChatAsync(chat.GroupChatId.Value);
            foreach (var m in dto.Members)
                MemberItems.Add(new ChatMemberItemVm
                {
                    UserId       = m.UserId,
                    Username     = m.Username,
                    IsOwner      = m.UserId == dto.CreatedByUserId,
                    IsCurrentUser = m.UserId == _session.CurrentUser?.Id,
                    CanManage    = chat.IsGroupOwner && m.UserId != _session.CurrentUser?.Id
                });
            IsMembersDialogOpen = true;
        }
        catch (Exception ex) { MembersError = ex.Message; }
    }

    [RelayCommand]
    private async Task AddMemberAsync()
    {
        if (string.IsNullOrWhiteSpace(AddMemberUsername) || _managingChat?.GroupChatId is null) return;
        MembersError = null;
        try
        {
            // Search user by username
            var users = await _userSearchApi.SearchUsersAsync(AddMemberUsername.Trim());
            var target = users.FirstOrDefault();
            if (target is null) { MembersError = "Пользователь не найден"; return; }
            if (MemberItems.Any(m => m.UserId == target.Id)) { MembersError = "Уже участник"; return; }

            await _api.AddGroupChatMemberAsync(_managingChat.GroupChatId.Value, target.Id);
            MemberItems.Add(new ChatMemberItemVm
            {
                UserId       = target.Id,
                Username     = target.Username,
                IsOwner      = false,
                IsCurrentUser = target.Id == _session.CurrentUser?.Id,
                CanManage    = _managingChat.IsGroupOwner
            });
            AddMemberUsername = string.Empty;
        }
        catch (Exception ex) { MembersError = ex.Message; }
    }

    [RelayCommand]
    private async Task RemoveMemberAsync(ChatMemberItemVm? member)
    {
        if (member is null || _managingChat?.GroupChatId is null) return;
        MembersError = null;
        try
        {
            await _api.RemoveGroupChatMemberAsync(_managingChat.GroupChatId.Value, member.UserId);
            MemberItems.Remove(member);
        }
        catch (Exception ex) { MembersError = ex.Message; }
    }

    [RelayCommand]
    private async Task TransferOwnershipAsync(ChatMemberItemVm? member)
    {
        if (member is null || _managingChat?.GroupChatId is null) return;
        if (System.Windows.MessageBox.Show(
                $"Передать права владельца пользователю «{member.Username}»?",
                "Подтверждение",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question) != System.Windows.MessageBoxResult.Yes) return;
        MembersError = null;
        try
        {
            await _api.TransferGroupOwnershipAsync(_managingChat.GroupChatId.Value, member.UserId);
            // Update local state
            foreach (var m in MemberItems) { m.IsOwner = m.UserId == member.UserId; m.CanManage = false; }
            _managingChat.IsGroupOwner = false;
            IsMembersDialogOpen = false;
        }
        catch (Exception ex) { MembersError = ex.Message; }
    }

    [RelayCommand] private void CloseMembersDialog() => IsMembersDialogOpen = false;

    // ── Message actions ───────────────────────────────────────────────────────
    [RelayCommand]
    private static void CopyMessageText(ChatMsgVm? msg)
    {
        if (string.IsNullOrEmpty(msg?.Text)) return;
        System.Windows.Clipboard.SetText(msg.Text);
    }

    [RelayCommand]
    private async Task DeleteMessageAsync(ChatMsgVm? msg)
    {
        if (msg is null) return;
        try
        {
            await _api.DeleteChatMessageAsync(msg.Id);
            RemoveMessageLocally(msg.Id);
        }
        catch (Exception ex) { StatusText = $"Не удалось удалить: {ex.Message}"; }
    }

    private void RemoveMessageLocally(Guid messageId)
    {
        var vm = _rawMessages.FirstOrDefault(m => m.Id == messageId);
        if (vm is null) return;
        _rawMessages.Remove(vm);
        RebuildChatItems();
    }

    // ── Pin storage helpers ───────────────────────────────────────────────────
    private void LoadPinnedChats()
    {
        try
        {
            if (!File.Exists(PinnedChatsFile)) { _pinnedChatIds = []; return; }
            var json = File.ReadAllText(PinnedChatsFile);
            _pinnedChatIds = JsonSerializer.Deserialize<HashSet<string>>(json) ?? [];
        }
        catch { _pinnedChatIds = []; }
    }

    private void SavePinnedChats()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PinnedChatsFile)!);
            File.WriteAllText(PinnedChatsFile, JsonSerializer.Serialize(_pinnedChatIds));
        }
        catch { }
    }

    // ── Incoming message handler (SignalR) ────────────────────────────────────
    private void OnIncomingMessage(ChatMessageDto msg)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            bool matchesCurrent = IsCurrentChat(msg);

            if (matchesCurrent)
            {
                // Replace optimistic entry if this is our own message echo
                var myUserId = _session.CurrentUser?.Id;
                if (msg.SenderUserId == myUserId && _pendingOptimisticIds.Count > 0)
                {
                    var opt = _rawMessages.FirstOrDefault(m =>
                        _pendingOptimisticIds.Contains(m.Id) && m.Text == msg.Text);
                    if (opt is not null)
                    {
                        _pendingOptimisticIds.Remove(opt.Id);
                        var idx = _rawMessages.IndexOf(opt);
                        var confirmed = MapVm(msg);
                        _rawMessages[idx] = confirmed;
                        RebuildChatItems();
                        ScrollToBottomRequested?.Invoke();
                        return;
                    }
                }
                if (!_rawMessages.Any(m => m.Id == msg.Id))
                {
                    var vm = MapVm(msg);
                    _ = EnrichWithAvatarAsync(vm, msg.SenderUserId);
                    _rawMessages.Add(vm);
                    AppendOrRebuildWithMessage(vm);
                    ScrollToBottomRequested?.Invoke();
                }
            }

            // Update chat list summary
            var existing = AllChats.FirstOrDefault(c =>
                (c.ProjectId.HasValue  && c.ProjectId  == msg.ProjectId) ||
                (c.GroupChatId.HasValue && c.GroupChatId == msg.GroupChatId) ||
                (c.OtherUserId.HasValue && (c.OtherUserId == msg.SenderUserId || c.OtherUserId == msg.ToUserId)));
            if (existing is not null)
            {
                existing.LastMessageText = msg.Text;
                existing.LastMessageAt   = msg.SentAt;
                existing.LastSenderUsername = msg.SenderUsername;

                // Increment unread only if message is not mine and chat is not currently open
                var myId = _session.CurrentUser?.Id;
                if (!matchesCurrent && msg.SenderUserId != myId)
                    existing.UnreadCount++;

                var idx = AllChats.IndexOf(existing);
                if (idx > 0) AllChats.Move(idx, 0);
            }
        });
    }

    // Fired when partner has read our messages in a DM
    private void OnMessagesRead(string chatKey)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            // chatKey format = "user:{theirId:N}" — matches our active direct chat
            var activeChatKey = _activeDirectUserId.HasValue
                ? $"user:{_activeDirectUserId.Value:N}" : null;
            if (activeChatKey is null || !string.Equals(chatKey, activeChatKey, StringComparison.OrdinalIgnoreCase))
                return;

            foreach (var msg in _rawMessages.Where(m => m.IsMine && m.Status == MsgStatus.Sent))
                msg.Status = MsgStatus.Read;
        });
    }

    // Fired when a message is deleted (by its sender, from another device or by someone else)
    private void OnMessageDeleted(Guid messageId)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() => RemoveMessageLocally(messageId));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool IsCurrentChat(ChatMessageDto msg)
    {
        if (_activeProjectId.HasValue && msg.ProjectId == _activeProjectId.Value) return true;
        if (_activeGroupChatId.HasValue && msg.GroupChatId == _activeGroupChatId.Value) return true;
        if (_activeDirectUserId.HasValue && msg.ProjectId is null && msg.GroupChatId is null)
        {
            var myId = _session.CurrentUser?.Id;
            if ((msg.SenderUserId == myId && msg.ToUserId == _activeDirectUserId.Value) ||
                (msg.SenderUserId == _activeDirectUserId.Value && msg.ToUserId == myId))
                return true;
        }
        return false;
    }

    private void RebuildChatItems()
    {
        ChatItems.Clear();
        DateTime? lastDate = null;
        foreach (var msg in _rawMessages)
        {
            var localDate = msg.SentAt.ToLocalTime().Date;
            if (lastDate is null || localDate != lastDate.Value)
            {
                ChatItems.Add(new ChatListItem { DateLabel = GetDateLabel(msg.SentAt) });
                lastDate = localDate;
            }
            ChatItems.Add(new ChatListItem { Message = msg });
        }
    }

    // Append a new message to ChatItems, adding a date separator if the date changed
    private void AppendOrRebuildWithMessage(ChatMsgVm msg)
    {
        var localDate = msg.SentAt.ToLocalTime().Date;
        var lastMsgDate = _rawMessages.Count > 1
            ? _rawMessages[^2].SentAt.ToLocalTime().Date
            : (DateTime?)null;

        if (lastMsgDate is null || localDate != lastMsgDate.Value)
            ChatItems.Add(new ChatListItem { DateLabel = GetDateLabel(msg.SentAt) });

        ChatItems.Add(new ChatListItem { Message = msg });
    }

    private static string GetDateLabel(DateTime sentAtUtc)
    {
        var localDate = sentAtUtc.ToLocalTime().Date;
        var today = DateTime.Today;
        if (localDate == today) return "Сегодня";
        if (localDate == today.AddDays(-1)) return "Вчера";
        var ru = new CultureInfo("ru-RU");
        return localDate.Year == today.Year
            ? localDate.ToString("d MMMM", ru)
            : localDate.ToString("dd.MM.yyyy");
    }

    private string BuildFileUrl(string? fileId)
    {
        if (string.IsNullOrEmpty(fileId)) return string.Empty;
        return $"{_mode.ServerUrl.TrimEnd('/')}/api/v1/files/{fileId}";
    }

    private ChatMsgVm MapVm(ChatMessageDto m) => new()
    {
        Id                 = m.Id,
        SenderUsername     = m.SenderUsername,
        Text               = m.Text,
        SentAt             = m.SentAt,
        IsMine             = _session.CurrentUser?.Id == m.SenderUserId,
        AttachmentFileId   = m.AttachmentFileId,
        AttachmentFileName = m.AttachmentFileName,
        AttachmentSize     = m.AttachmentSize,
        AttachmentUrl      = string.IsNullOrEmpty(m.AttachmentFileId) ? null
                             : $"{_mode.ServerUrl.TrimEnd('/')}/api/v1/files/{m.AttachmentFileId}",
        Status             = m.IsReadByRecipient ? MsgStatus.Read : MsgStatus.Sent
    };

    private async Task EnrichWithAvatarAsync(ChatMsgVm vm, Guid senderUserId)
    {
        var base64 = await _avatarCache.GetAsync(senderUserId);
        if (!string.IsNullOrEmpty(base64))
        {
            vm.SenderAvatarBase64 = base64;
        }
    }
}
