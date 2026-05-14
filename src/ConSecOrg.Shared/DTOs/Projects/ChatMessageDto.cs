namespace ConSecOrg.Shared.DTOs.Projects;

public class ChatMessageDto
{
    public Guid Id { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? GroupChatId { get; set; }
    public Guid SenderUserId { get; set; }
    public string SenderUsername { get; set; } = string.Empty;
    public Guid? ToUserId { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    // File attachment — null means text-only message
    public string? AttachmentFileId { get; set; }
    public string? AttachmentFileName { get; set; }
    public long? AttachmentSize { get; set; }
    // Resolved full URL for download (set by client after receiving response)
    public string? AttachmentUrl { get; set; }
    // True when the recipient has opened the chat after this message was sent
    public bool IsReadByRecipient { get; set; }
}

public class SendChatMessageDto
{
    public Guid? ProjectId { get; set; }
    public Guid? GroupChatId { get; set; }
    public Guid? ToUserId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? AttachmentFileId { get; set; }
    public string? AttachmentFileName { get; set; }
    public long? AttachmentSize { get; set; }
    // Legacy field kept for backward compat
    public string? AttachmentUrl { get; set; }
}

public class ChatSummaryDto
{
    public string ChatId { get; set; } = string.Empty;     // "project:{guid}" | "user:{guid}" | "group:{guid}"
    public string ChatType { get; set; } = "project";       // project | direct | group
    public Guid? ProjectId { get; set; }
    public Guid? OtherUserId { get; set; }
    public Guid? GroupChatId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string LastMessageText { get; set; } = string.Empty;
    public DateTime? LastMessageAt { get; set; }
    public string LastSenderUsername { get; set; } = string.Empty;
    public int UnreadCount { get; set; }
    // For group chats: the user ID of the owner/creator
    public Guid? OwnerUserId { get; set; }
}
