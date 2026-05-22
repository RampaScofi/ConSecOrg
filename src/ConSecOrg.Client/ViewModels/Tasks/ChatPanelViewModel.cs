using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;

namespace ConSecOrg.Client.ViewModels.Tasks;

public partial class ChatMessageViewModel : ObservableObject
{
    public string SenderUsername { get; init; } = string.Empty;
    public string SenderLetter => SenderUsername.Length > 0 ? SenderUsername[0].ToString().ToUpper() : "?";
    public string Text { get; init; } = string.Empty;
    public string TimeDisplay { get; init; } = string.Empty;
    public bool IsMine { get; init; }
    public string? AttachmentPath { get; init; }
    public string? AttachmentFileName { get; init; }
    public bool HasAttachment => !string.IsNullOrEmpty(AttachmentPath);
    public bool IsImageAttachment => HasAttachment &&
        (AttachmentFileName?.EndsWith(".png", StringComparison.OrdinalIgnoreCase) == true ||
         AttachmentFileName?.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) == true ||
         AttachmentFileName?.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) == true ||
         AttachmentFileName?.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) == true ||
         AttachmentFileName?.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) == true ||
         AttachmentFileName?.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) == true);
    public bool IsFileAttachment => HasAttachment && !IsImageAttachment;

    [ObservableProperty] private string? _senderAvatarBase64;
    public bool HasAvatar => !string.IsNullOrEmpty(SenderAvatarBase64);
    public BitmapImage? AvatarImage => AvatarCacheService.Base64ToImage(SenderAvatarBase64);

    partial void OnSenderAvatarBase64Changed(string? value)
    {
        OnPropertyChanged(nameof(HasAvatar));
        OnPropertyChanged(nameof(AvatarImage));
    }
}

public partial class ChatPanelViewModel(ChatService chatService, SessionService session, AvatarCacheService avatarCache) : ObservableObject
{
    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private string _chatTitle = string.Empty;
    [ObservableProperty] private string _newMessage = string.Empty;
    [ObservableProperty] private ObservableCollection<ChatMessageViewModel> _messages = [];

    private string _currentChatId = string.Empty;

    // View subscribes to this to scroll to bottom when new messages arrive
    public event Action? ScrollToBottomRequested;

    public void OpenForProject(string projectId, string projectName)
    {
        _currentChatId = projectId;
        ChatTitle = $"Чат — {projectName}";
        LoadMessages();
        IsOpen = true;
    }

    public void OpenDirect(Guid userId, string username)
    {
        _currentChatId = $"direct:{userId}";
        ChatTitle = $"Чат — {username}";
        LoadMessages();
        IsOpen = true;
    }

    public void Close() => IsOpen = false;

    [RelayCommand]
    private void Toggle() => IsOpen = !IsOpen;

    [RelayCommand]
    private void Send()
    {
        var text = NewMessage.Trim();
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(_currentChatId)) return;

        var userId = session.CurrentUser?.Id.ToString() ?? "local";
        var username = session.CurrentUser?.Username ?? "Я";

        chatService.AddMessage(_currentChatId, userId, username, text);
        NewMessage = string.Empty;
        LoadMessages();
    }

    [RelayCommand]
    private void AttachFile()
    {
        if (string.IsNullOrEmpty(_currentChatId)) return;

        var dlg = new OpenFileDialog
        {
            Title = "Выберите файл для отправки",
            Filter = "Все файлы|*.*|Изображения|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp|Текстовые файлы|*.txt;*.md;*.csv;*.json|Документы|*.pdf;*.doc;*.docx",
            CheckFileExists = true
        };
        if (dlg.ShowDialog() != true) return;

        var filePath = dlg.FileName;
        var info = new FileInfo(filePath);

        if (info.Length > 20 * 1024 * 1024)
        {
            System.Windows.MessageBox.Show("Размер файла превышает 20 МБ.", "Файл слишком большой",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        var userId = session.CurrentUser?.Id.ToString() ?? "local";
        var username = session.CurrentUser?.Username ?? "Я";
        var fileName = Path.GetFileName(filePath);

        chatService.AddMessage(_currentChatId, userId, username, $"📎 {fileName}", filePath);
        LoadMessages();
    }

    [RelayCommand]
    private static void OpenAttachment(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch { }
    }

    private void LoadMessages()
    {
        var myId = session.CurrentUser?.Id.ToString() ?? "local";
        var raw = chatService.LoadMessages(_currentChatId);
        var vms = raw.Select(m => new ChatMessageViewModel
        {
            SenderUsername = m.SenderUsername,
            Text = m.Text,
            TimeDisplay = m.SentAt.ToString("HH:mm"),
            IsMine = m.SenderUserId == myId,
            AttachmentPath = m.AttachmentPath,
            AttachmentFileName = m.AttachmentFileName
        }).ToList();
        Messages = new ObservableCollection<ChatMessageViewModel>(vms);
        ScrollToBottomRequested?.Invoke();
        _ = EnrichWithAvatarsAsync(vms, raw);
    }

    private async Task EnrichWithAvatarsAsync(List<ChatMessageViewModel> vms, List<ConSecOrg.Client.Services.ChatMessageEntry> raw)
    {
        for (int i = 0; i < vms.Count; i++)
        {
            if (vms[i].IsMine) continue;
            if (!Guid.TryParse(raw[i].SenderUserId, out var uid)) continue;
            var b64 = await avatarCache.GetAsync(uid);
            if (!string.IsNullOrEmpty(b64))
                System.Windows.Application.Current.Dispatcher.Invoke(() => vms[i].SenderAvatarBase64 = b64);
        }
    }
}
