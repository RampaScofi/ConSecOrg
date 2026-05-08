using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConSecOrg.Client.Services;
using System.Collections.ObjectModel;

namespace ConSecOrg.Client.ViewModels.Tasks;

public partial class ChatMessageViewModel : ObservableObject
{
    public string SenderUsername { get; init; } = string.Empty;
    public string SenderLetter => SenderUsername.Length > 0 ? SenderUsername[0].ToString().ToUpper() : "?";
    public string Text { get; init; } = string.Empty;
    public string TimeDisplay { get; init; } = string.Empty;
    public bool IsMine { get; init; }
}

public partial class ChatPanelViewModel(ChatService chatService, SessionService session) : ObservableObject
{
    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private string _chatTitle = string.Empty;
    [ObservableProperty] private string _newMessage = string.Empty;
    [ObservableProperty] private ObservableCollection<ChatMessageViewModel> _messages = [];

    private string _currentChatId = string.Empty;

    public void OpenForProject(string projectId, string projectName)
    {
        _currentChatId = projectId;
        ChatTitle = $"Чат — {projectName}";
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

    private void LoadMessages()
    {
        var myId = session.CurrentUser?.Id.ToString() ?? "local";
        var raw = chatService.LoadMessages(_currentChatId);
        Messages = new ObservableCollection<ChatMessageViewModel>(
            raw.Select(m => new ChatMessageViewModel
            {
                SenderUsername = m.SenderUsername,
                Text = m.Text,
                TimeDisplay = m.SentAt.ToString("HH:mm"),
                IsMine = m.SenderUserId == myId
            }));
    }
}
