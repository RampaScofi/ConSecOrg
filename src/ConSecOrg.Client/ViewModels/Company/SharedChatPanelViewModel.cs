using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Infrastructure.Api;
using ConSecOrg.Client.Services;
using ConSecOrg.Shared.DTOs.Projects;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;

namespace ConSecOrg.Client.ViewModels.Company;

public partial class ChatMessageViewModel : ObservableObject
{
    [ObservableProperty] private Guid _id;
    [ObservableProperty] private string _senderUsername = string.Empty;
    [ObservableProperty] private string _text = string.Empty;
    [ObservableProperty] private DateTime _sentAt;
    [ObservableProperty] private bool _isMine;
    [ObservableProperty] private string? _attachmentUrl;
    [ObservableProperty] private string? _attachmentFileName;

    public string SenderLetter => SenderUsername.Length > 0 ? SenderUsername[0].ToString().ToUpper() : "?";
    public string TimeDisplay => SentAt.ToLocalTime().ToString("HH:mm");
    public string DateDisplay => SentAt.ToLocalTime().ToString("d MMM");
    public bool HasAttachment => !string.IsNullOrEmpty(AttachmentUrl);
    public bool IsImageAttachment => HasAttachment &&
        (AttachmentFileName?.EndsWith(".png", StringComparison.OrdinalIgnoreCase) == true ||
         AttachmentFileName?.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) == true ||
         AttachmentFileName?.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) == true ||
         AttachmentFileName?.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) == true ||
         AttachmentFileName?.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) == true ||
         AttachmentFileName?.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) == true);
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
    public ObservableCollection<ChatSummaryDto> AllChats { get; } = new();

    public ObservableCollection<ChatMessageViewModel> Messages { get; } = new();

    // Emoji picker
    [ObservableProperty] private bool _isEmojiPickerOpen;

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
        "😺","😸","😹","😻","😼","😽","🙀","😿","😾",
        "👍","👎","👌","✌️","🤞","🤟","🤘","🤙","👈","👉","👆","🖕","👇",
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
    }

    public async void OpenProject(Guid projectId, string projectName)
    {
        IsOpen = true;
        ShowingChatList = false;
        ProjectId = projectId;
        OtherUserId = null;
        Title = $"Чат: {projectName}";
        Messages.Clear();
        try
        {
            await _hub.EnsureConnectedAsync();
            var msgs = await _api.GetProjectMessagesAsync(projectId);
            foreach (var m in msgs) Messages.Add(MapVm(m));
        }
        catch { }
    }

    public async void OpenDirect(Guid otherUserId, string otherUsername)
    {
        IsOpen = true;
        ShowingChatList = false;
        ProjectId = null;
        OtherUserId = otherUserId;
        Title = $"Личный чат: {otherUsername}";
        Messages.Clear();
        try
        {
            await _hub.EnsureConnectedAsync();
            var msgs = await _api.GetDirectMessagesAsync(otherUserId);
            foreach (var m in msgs) Messages.Add(MapVm(m));
        }
        catch { }
    }

    public async void OpenChatList()
    {
        IsOpen = true;
        ShowingChatList = true;
        Title = "Чаты";
        try
        {
            await _hub.EnsureConnectedAsync();
            var chats = await _api.GetChatsAsync();
            AllChats.Clear();
            foreach (var c in chats) AllChats.Add(c);
        }
        catch { }
    }

    [RelayCommand]
    private void OpenChatFromList(ChatSummaryDto? summary)
    {
        if (summary is null) return;
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
        try
        {
            await _api.SendMessageAsync(new SendChatMessageDto
            {
                ProjectId = ProjectId,
                ToUserId = OtherUserId,
                Text = text
            });
            // Echo arrives via SignalR
        }
        catch { NewMessage = text; }
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

        // Limit to 20 MB
        if (fileInfo.Length > 20 * 1024 * 1024)
        {
            System.Windows.MessageBox.Show("Размер файла превышает 20 МБ.", "Файл слишком большой",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        try
        {
            var (url, uploadedName) = await _api.UploadChatFileAsync(filePath);
            await _api.SendMessageAsync(new SendChatMessageDto
            {
                ProjectId = ProjectId,
                ToUserId = OtherUserId,
                Text = $"📎 {fileName}",
                AttachmentUrl = url,
                AttachmentFileName = uploadedName
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
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            // Only show if it belongs to currently open chat
            bool matchesCurrent = false;
            if (ProjectId.HasValue && msg.ProjectId == ProjectId.Value) matchesCurrent = true;
            else if (OtherUserId.HasValue && msg.ProjectId is null)
            {
                var myId = _session.CurrentUser?.Id;
                if ((msg.SenderUserId == myId && msg.ToUserId == OtherUserId.Value) ||
                    (msg.SenderUserId == OtherUserId.Value && msg.ToUserId == myId))
                    matchesCurrent = true;
            }
            if (matchesCurrent && !Messages.Any(m => m.Id == msg.Id))
                Messages.Add(MapVm(msg));
        });
    }

    private string? ResolveAttachmentUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        if (url.StartsWith("http://") || url.StartsWith("https://")) return url;
        // Relative path: prepend server base URL
        var serverUrl = _mode.ServerUrl.TrimEnd('/');
        return $"{serverUrl}{url}";
    }

    private ChatMessageViewModel MapVm(ChatMessageDto m) => new()
    {
        Id = m.Id,
        SenderUsername = m.SenderUsername,
        Text = m.Text,
        SentAt = m.SentAt,
        IsMine = _session.CurrentUser?.Id == m.SenderUserId,
        AttachmentUrl = ResolveAttachmentUrl(m.AttachmentUrl),
        AttachmentFileName = m.AttachmentFileName
    };
}
