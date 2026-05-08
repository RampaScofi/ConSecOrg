using ConSecOrg.Domain.Common;

namespace ConSecOrg.Domain.Entities;

/// <summary>
/// Chat message — either project group chat (ProjectId set, ToUserId null)
/// or direct message between two users (ProjectId null, ToUserId set).
/// </summary>
public class ChatMessage : BaseEntity<Guid>
{
    public Guid? ProjectId { get; private set; }
    public Guid SenderUserId { get; private set; }
    public Guid? ToUserId { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public DateTime SentAt { get; private set; }

    public User? Sender { get; private set; }

    protected ChatMessage() { }

    public ChatMessage(Guid id, Guid senderUserId, string text, Guid? projectId, Guid? toUserId) : base(id)
    {
        SenderUserId = senderUserId;
        Text = text;
        ProjectId = projectId;
        ToUserId = toUserId;
        SentAt = DateTime.UtcNow;
    }
}
