namespace ConSecOrg.Shared.DTOs.Projects;

public class ChatMessageDto
{
    public Guid Id { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid SenderUserId { get; set; }
    public string SenderUsername { get; set; } = string.Empty;
    public Guid? ToUserId { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public string? AttachmentUrl { get; set; }
    public string? AttachmentFileName { get; set; }
}

public class SendChatMessageDto
{
    public Guid? ProjectId { get; set; }
    public Guid? ToUserId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
    public string? AttachmentFileName { get; set; }
}

public class ChatSummaryDto
{
    public string ChatId { get; set; } = string.Empty;     // "project:{guid}" or "user:{guid}"
    public string ChatType { get; set; } = "project";       // project | direct
    public Guid? ProjectId { get; set; }
    public Guid? OtherUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string LastMessageText { get; set; } = string.Empty;
    public DateTime? LastMessageAt { get; set; }
    public string LastSenderUsername { get; set; } = string.Empty;
    public int UnreadCount { get; set; }
}
