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
    [ObservableProperty] private MsgStatus _status = MsgStatus.Sent;

    public string SenderLetter => SenderUsername.Length > 0 ? SenderUsername[0].ToString().ToUpper() : "?";
    public string TimeDisplay => SentAt.ToLocalTime().ToString("HH:mm");
    public bool HasAttachment => !string.IsNullOrEmpty(AttachmentFileId);
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
    public string  Title        { get; init; } = string.Empty;

    [ObservableProperty] private int      _unreadCount;
    [ObservableProperty] private string   _lastMessageText = string.Empty;
    [ObservableProperty] private DateTime? _lastMessageAt;
    [ObservableProperty] private string   _lastSenderUsername = string.Empty;

    public bool HasUnread => UnreadCount > 0;
    partial void OnUnreadCountChanged(int value) => OnPropertyChanged(nameof(HasUnread));

    public static ChatSummaryVm FromDto(ChatSummaryDto dto) => new()
    {
        ChatId           = dto.ChatId,
        ChatType         = dto.ChatType,
        ProjectId        = dto.ProjectId,
        OtherUserId      = dto.OtherUserId,
        GroupChatId      = dto.GroupChatId,
        Title            = dto.Title,
        LastMessageText  = dto.LastMessageText,
        LastMessageAt    = dto.LastMessageAt,
        LastSenderUsername = dto.LastSenderUsername,
        UnreadCount      = dto.UnreadCount
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

// ── Main ChatsViewModel ───────────────────────────────────────────────────────
public partial class ChatsViewModel : ConSecOrg.Client.ViewModels.Base.BasePageViewModel
{
    private readonly IChatApiService _api;
    private readonly IContactsApiService _contactsApi;
    private readonly Services.SharedProjectsHubClient _hub;
    private readonly SessionService _session;
    private readonly ModeService _mode;

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

    // ── Scroll helper ─────────────────────────────────────────────────────────
    public event Action? ScrollToBottomRequested;

    public ChatsViewModel(IChatApiService api, IContactsApiService contactsApi,
        Services.SharedProjectsHubClient hub, SessionService session, ModeService mode)
    {
        _api = api;
        _contactsApi = contactsApi;
        _hub = hub;
        _session = session;
        _mode = mode;
        _hub.ChatMessageReceived += OnIncomingMessage;
        _hub.MessagesRead += OnMessagesRead;
    }

    public override async Task OnNavigatedToAsync() => await LoadChatsAsync();

    // ── Load chats list ───────────────────────────────────────────────────────
    [RelayCommand]
    private async Task LoadChatsAsync()
    {
        IsLoadingChats = true;
        StatusText = null;
        try
        {
            await _hub.EnsureConnectedAsync();
            var chats = await _api.GetChatsAsync();
            AllChats.Clear();
            foreach (var c in chats) AllChats.Add(ChatSummaryVm.FromDto(c));
            if (AllChats.Count == 0) StatusText = "Нет активных чатов";
        }
        catch (Exception ex) { StatusText = $"Ошибка загрузки: {ex.Message}"; }
        finally { IsLoadingChats = false; }
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
            foreach (var m in msgs) _rawMessages.Add(MapVm(m));
            RebuildChatItems();
            ScrollToBottomRequested?.Invoke();
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
        Status             = m.IsReadByRecipient ? MsgStatus.Read : MsgStatus.Sent
    };
}
