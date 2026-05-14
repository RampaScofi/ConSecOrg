using ConSecOrg.Domain.Common;

namespace ConSecOrg.Domain.Entities;

/// <summary>
/// Chat message — project chat (ProjectId set), direct message (ToUserId set), or group chat (GroupChatId set).
/// Text is encrypted at rest using ГОСТ Р 34.12-2015 when TextCipher is not null.
/// </summary>
public class ChatMessage : BaseEntity<Guid>
{
    public Guid? ProjectId { get; private set; }
    public Guid SenderUserId { get; private set; }
    public Guid? ToUserId { get; private set; }
    public Guid? GroupChatId { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public DateTime SentAt { get; private set; }

    // ГОСТ Кузнечик CTR encrypted text (nullable — absent on old unencrypted messages)
    public byte[]? TextCipher { get; private set; }
    public byte[]? TextNonce { get; private set; }
    public byte[]? TextHmac { get; private set; }
    public bool IsTextEncrypted => TextCipher != null;

    // File attachment
    public string? AttachmentFileId { get; private set; }
    public string? AttachmentFileName { get; private set; }
    public long? AttachmentSize { get; private set; }

    public User? Sender { get; private set; }
    public GroupChat? GroupChat { get; private set; }

    protected ChatMessage() { }

    public ChatMessage(Guid id, Guid senderUserId, string text,
        Guid? projectId, Guid? toUserId, Guid? groupChatId = null,
        string? attachmentFileId = null, string? attachmentFileName = null, long? attachmentSize = null)
        : base(id)
    {
        SenderUserId = senderUserId;
        Text = text;
        ProjectId = projectId;
        ToUserId = toUserId;
        GroupChatId = groupChatId;
        AttachmentFileId = attachmentFileId;
        AttachmentFileName = attachmentFileName;
        AttachmentSize = attachmentSize;
        SentAt = DateTime.UtcNow;
    }

    public void SetEncryptedText(byte[] cipher, byte[] nonce, byte[] hmac)
    {
        TextCipher = cipher;
        TextNonce = nonce;
        TextHmac = hmac;
        Text = string.Empty;
    }
}
