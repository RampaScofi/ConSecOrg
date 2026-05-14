using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Infrastructure.Api;
using ConSecOrg.Client.Services;
using ConSecOrg.Client.ViewModels.Chats;
using ConSecOrg.Shared.DTOs.Projects;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;

namespace ConSecOrg.Client.ViewModels.Company;

// ChatMsgPanelVm wraps a ChatMsgVm for the panel — reuses MsgStatus from Chats namespace

public partial class SharedChatSummaryVm : ObservableObject
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

    public static SharedChatSummaryVm FromDto(ChatSummaryDto dto) => new()
    {
        ChatId             = dto.ChatId,
        ChatType           = dto.ChatType,
        ProjectId          = dto.ProjectId,
        OtherUserId        = dto.OtherUserId,
        GroupChatId        = dto.GroupChatId,
        Title              = dto.Title,
        LastMessageText    = dto.LastMessageText,
        LastMessageAt      = dto.LastMessageAt,
        LastSenderUsername = dto.LastSenderUsername,
        UnreadCount        = dto.UnreadCount
    };
}

public partial class PanelMsgVm : ObservableObject
{
    [ObservableProperty] private Guid        _id;
    [ObservableProperty] private string      _senderUsername = string.Empty;
    [ObservableProperty] private string      _text           = string.Empty;
    [ObservableProperty] private DateTime    _sentAt;
    [ObservableProperty] private bool        _isMine;
    [ObservableProperty] private string?     _attachmentUrl;
    [ObservableProperty] private string?     _attachmentFileId;
    [ObservableProperty] private string?     _attachmentFileName;
    [ObservableProperty] private long?       _attachmentSize;
    [ObservableProperty] private MsgStatus   _status = MsgStatus.Sent;

    public string SenderLetter => SenderUsername.Length > 0 ? SenderUsername[0].ToString().ToUpper() : "?";
    public string TimeDisplay  => SentAt.ToLocalTime().ToString("HH:mm");
    public bool   HasAttachment     => !string.IsNullOrEmpty(AttachmentFileId);
    public bool   IsImageAttachment => HasAttachment &&
        (AttachmentFileName?.EndsWith(".png",  StringComparison.OrdinalIgnoreCase) == true ||
         AttachmentFileName?.EndsWith(".jpg",  StringComparison.OrdinalIgnoreCase) == true ||
         AttachmentFileName?.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) == true ||
         AttachmentFileName?.EndsWith(".gif",  StringComparison.OrdinalIgnoreCase) == true ||
         AttachmentFileName?.EndsWith(".bmp",  StringComparison.OrdinalIgnoreCase) == true ||
         AttachmentFileName?.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) == true);

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

public class PanelChatListItem
{
    public PanelMsgVm? Message   { get; init; }
    public string?     DateLabel { get; init; }
    public bool IsSeparator => DateLabel is not null;
    public bool IsMessage   => Message  is not null;
}

public partial class SharedChatPanelViewModel : ObservableObject
{
    private readonly IChatApiService _api;
    private readonly SharedProjectsHubClient _hub;
    private readonly SessionService _session;
    private readonly ModeService _mode;

    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _newMessage = string.Empty;

    // Active chat context
    [ObservableProperty] private Guid? _projectId;
    [ObservableProperty] private Guid? _otherUserId;

    // Chat list mode
    [ObservableProperty] private bool _showingChatList;
    public ObservableCollection<SharedChatSummaryVm> AllChats { get; } = new();

    // Rendered items (messages + date separators)
    public ObservableCollection<PanelChatListItem> ChatItems { get; } = new();
    private readonly List<PanelMsgVm> _rawMessages = [];
    private readonly HashSet<Guid>    _pendingOptimisticIds = [];

    private SharedChatSummaryVm? _activeSummary;

    // Emoji picker
    [ObservableProperty] private bool _isEmojiPickerOpen;

    // Scroll event for code-behind
    public event Action? ScrollToBottomRequested;

    public static readonly string[] CommonEmojis =
    [
        "😀","😃","😄","😁","😆","😅","🤣","😂","🙂","🙃","😉","😊","😇",
        "🥰","😍","🤩","😘","😗","😚","😙","🥲","😋","😛","😜","🤪","😝",
        "🤑","🤗","🤭","🤫","🤔","🤐","🤨","😐","😑","😶","😏","😒","🙄",
        "😬","🤥","😌","😔","😪","🤤","😴","😷","🤒","🤕","🤢","🤮","🤧",
        "🥵","🥶","🥴","😵","🤯","🤠","🥳","🥸","😎","🤓","🧐",
        "😕","😟","🙁","☹️","😮","😯","😲","😳","🥺","😦","😧","😨","😰",
        "😥","😢","😭","😱","😖","😣","😞","😓","😩","😫","🥱","😤","😡",
        "😠","🤬","😈","👿","💀","☠️","💩","🤡","👹","👺","👻","👽","🤖",
        "👍","👎","👌","✌️","🤞","🤟","🤘","🤙","👈","👉","👆","👇",
        "☝️","👋","🤚","🖐️","✋","🖖","👏","🙌","👐","🤲","🙏","✍️","💪",
        "❤️","🧡","💛","💚","💙","💜","🖤","🤍","🤎","💔","❣️","💕","💞",
        "💓","💗","💖","💘","💝","💟","✅","❌","⭐","🔥","💯","🎉","🎊","🚀"
    ];

    public SharedChatPanelViewModel(IChatApiService api, SharedProjectsHubClient hub, SessionService session, ModeService mode)
    {
        _api = api;
        _hub = hub;
        _session = session;
        _mode = mode;
        _hub.ChatMessageReceived += OnIncomingMessage;
        _hub.MessagesRead += OnMessagesRead;
    }

    public async void OpenProject(Guid projectId, string projectName)
    {
        IsOpen = true;
        ShowingChatList = false;
        ProjectId = projectId;
        OtherUserId = null;
        _activeSummary = null;
        Title = $"Чат: {projectName}";
        _rawMessages.Clear();
        ChatItems.Clear();
        try
        {
            await _hub.EnsureConnectedAsync();
            await _hub.SubscribeAsync(projectId);
            var chatKey = $"project:{projectId:N}";
            _ = _api.MarkReadAsync(chatKey);
            var msgs = await _api.GetProjectMessagesAsync(projectId);
            foreach (var m in msgs) _rawMessages.Add(MapVm(m));
            RebuildChatItems();
            ScrollToBottomRequested?.Invoke();
        }
        catch { }
    }

    public async void OpenDirect(Guid otherUserId, string otherUsername)
    {
        IsOpen = true;
        ShowingChatList = false;
        ProjectId = null;
        OtherUserId = otherUserId;
        _activeSummary = null;
        Title = $"Личный чат: {otherUsername}";
        _rawMessages.Clear();
        ChatItems.Clear();
        try
        {
            await _hub.EnsureConnectedAsync();
            var chatKey = $"user:{otherUserId:N}";
            _ = _api.MarkReadAsync(chatKey);
            _ = _hub.NotifyDirectReadAsync(otherUserId);
            var msgs = await _api.GetDirectMessagesAsync(otherUserId);
            foreach (var m in msgs) _rawMessages.Add(MapVm(m));
            RebuildChatItems();
            ScrollToBottomRequested?.Invoke();
        }
        catch { }
    }

    public async void OpenChatList()
    {
        IsOpen = true;
        ShowingChatList = true;
        ProjectId = null;
        OtherUserId = null;
        _activeSummary = null;
        Title = "Чаты";
        try
        {
            await _hub.EnsureConnectedAsync();
            var chats = await _api.GetChatsAsync();
            AllChats.Clear();
            foreach (var c in chats) AllChats.Add(SharedChatSummaryVm.FromDto(c));
        }
        catch { }
    }

    [RelayCommand]
    private void OpenChatFromList(SharedChatSummaryVm? summary)
    {
        if (summary is null) return;
        _activeSummary = summary;
        summary.UnreadCount = 0;
        if (summary.ChatType == "project" && summary.ProjectId.HasValue)
            OpenProject(summary.ProjectId.Value, summary.Title);
        else if (summary.ChatType == "direct" && summary.OtherUserId.HasValue)
            OpenDirect(summary.OtherUserId.Value, summary.Title);
    }

    [RelayCommand]
    private void BackToList() => OpenChatList();

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        var text = NewMessage?.Trim();
        if (string.IsNullOrEmpty(text)) return;
        if (ProjectId is null && OtherUserId is null) return;

        NewMessage = string.Empty;

        var optimisticId = Guid.NewGuid();
        var optimisticVm = new PanelMsgVm
        {
            Id             = optimisticId,
            SenderUsername = _session.CurrentUser?.Username ?? "Я",
            Text           = text,
            SentAt         = DateTime.UtcNow,
            IsMine         = true,
            Status         = MsgStatus.Sending
        };
        _rawMessages.Add(optimisticVm);
        _pendingOptimisticIds.Add(optimisticId);
        AppendOrRebuildWithMessage(optimisticVm);
        ScrollToBottomRequested?.Invoke();

        try
        {
            await _api.SendMessageAsync(new SendChatMessageDto
            {
                ProjectId = ProjectId,
                ToUserId  = OtherUserId,
                Text      = text
            });
        }
        catch
        {
            optimisticVm.Status = MsgStatus.Failed;
            _pendingOptimisticIds.Remove(optimisticId);
            NewMessage = text;
        }
    }

    [RelayCommand]
    public void Close() => IsOpen = false;

    [RelayCommand]
    private void ToggleEmojiPicker() => IsEmojiPickerOpen = !IsEmojiPickerOpen;

    [RelayCommand]
    private void InsertEmoji(string emoji)
    {
        NewMessage += emoji;
        IsEmojiPickerOpen = false;
    }

    [RelayCommand]
    private async Task AttachFileAsync()
    {
        if (ProjectId is null && OtherUserId is null) return;
        var dlg = new OpenFileDialog
        {
            Title = "Выберите файл для отправки",
            Filter = "Все файлы|*.*|Изображения|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp|Документы|*.pdf;*.doc;*.docx;*.txt;*.xlsx",
            CheckFileExists = true
        };
        if (dlg.ShowDialog() != true) return;

        var filePath = dlg.FileName;
        var fileName = Path.GetFileName(filePath);
        var fileInfo = new FileInfo(filePath);

        if (fileInfo.Length > 20 * 1024 * 1024)
        {
            System.Windows.MessageBox.Show("Размер файла превышает 20 МБ.", "Файл слишком большой",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        try
        {
            var (fileId, uploadedName, _, size) = await _api.UploadChatFileAsync(filePath);
            await _api.SendMessageAsync(new SendChatMessageDto
            {
                ProjectId          = ProjectId,
                ToUserId           = OtherUserId,
                Text               = string.Empty,
                AttachmentFileId   = fileId,
                AttachmentFileName = uploadedName,
                AttachmentSize     = size
            });
        }
        catch
        {
            System.Windows.MessageBox.Show("Не удалось отправить файл.", "Ошибка",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void OnIncomingMessage(ChatMessageDto msg)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            bool matchesCurrent = false;
            if (ProjectId.HasValue && msg.ProjectId == ProjectId.Value) matchesCurrent = true;
            else if (OtherUserId.HasValue && msg.ProjectId is null)
            {
                var myId = _session.CurrentUser?.Id;
                if ((msg.SenderUserId == myId    && msg.ToUserId == OtherUserId.Value) ||
                    (msg.SenderUserId == OtherUserId.Value && msg.ToUserId == myId))
                    matchesCurrent = true;
            }

            if (matchesCurrent)
            {
                var myUserId = _session.CurrentUser?.Id;
                if (msg.SenderUserId == myUserId && _pendingOptimisticIds.Count > 0)
                {
                    var opt = _rawMessages.FirstOrDefault(m =>
                        _pendingOptimisticIds.Contains(m.Id) && m.Text == msg.Text);
                    if (opt is not null)
                    {
                        _pendingOptimisticIds.Remove(opt.Id);
                        var idx = _rawMessages.IndexOf(opt);
                        _rawMessages[idx] = MapVm(msg);
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

            // Update chat list unread counter
            var existing = AllChats.FirstOrDefault(c =>
                (c.ProjectId.HasValue  && c.ProjectId  == msg.ProjectId) ||
                (c.OtherUserId.HasValue && (c.OtherUserId == msg.SenderUserId || c.OtherUserId == msg.ToUserId)));
            if (existing is not null)
            {
                existing.LastMessageText    = msg.Text;
                existing.LastMessageAt      = msg.SentAt;
                existing.LastSenderUsername = msg.SenderUsername;
                var myId = _session.CurrentUser?.Id;
                if (!matchesCurrent && msg.SenderUserId != myId)
                    existing.UnreadCount++;
            }
        });
    }

    private void OnMessagesRead(string chatKey)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var activeChatKey = OtherUserId.HasValue ? $"user:{OtherUserId.Value:N}" : null;
            if (activeChatKey is null || !string.Equals(chatKey, activeChatKey, StringComparison.OrdinalIgnoreCase))
                return;
            foreach (var msg in _rawMessages.Where(m => m.IsMine && m.Status == MsgStatus.Sent))
                msg.Status = MsgStatus.Read;
        });
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
                ChatItems.Add(new PanelChatListItem { DateLabel = GetDateLabel(msg.SentAt) });
                lastDate = localDate;
            }
            ChatItems.Add(new PanelChatListItem { Message = msg });
        }
    }

    private void AppendOrRebuildWithMessage(PanelMsgVm msg)
    {
        var localDate = msg.SentAt.ToLocalTime().Date;
        var lastMsgDate = _rawMessages.Count > 1
            ? _rawMessages[^2].SentAt.ToLocalTime().Date
            : (DateTime?)null;
        if (lastMsgDate is null || localDate != lastMsgDate.Value)
            ChatItems.Add(new PanelChatListItem { DateLabel = GetDateLabel(msg.SentAt) });
        ChatItems.Add(new PanelChatListItem { Message = msg });
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

    private PanelMsgVm MapVm(ChatMessageDto m) => new()
    {
        Id                 = m.Id,
        SenderUsername     = m.SenderUsername,
        Text               = m.Text,
        SentAt             = m.SentAt,
        IsMine             = _session.CurrentUser?.Id == m.SenderUserId,
        AttachmentFileId   = m.AttachmentFileId,
        AttachmentFileName = m.AttachmentFileName,
        AttachmentSize     = m.AttachmentSize,
        AttachmentUrl      = string.IsNullOrEmpty(m.AttachmentFileId) ? null : BuildFileUrl(m.AttachmentFileId),
        Status             = m.IsReadByRecipient ? MsgStatus.Read : MsgStatus.Sent
    };
}
