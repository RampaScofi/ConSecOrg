using ConSecOrg.Domain.Common;

namespace ConSecOrg.Domain.Entities;

public class GroupChat : BaseEntity<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public ICollection<GroupChatMember> Members { get; private set; } = new List<GroupChatMember>();
    public ICollection<ChatMessage> Messages { get; private set; } = new List<ChatMessage>();

    protected GroupChat() { }

    public GroupChat(Guid id, string name, Guid createdByUserId) : base(id)
    {
        Name = name;
        CreatedByUserId = createdByUserId;
        CreatedAt = DateTime.UtcNow;
    }

    public void Rename(string name) => Name = name;
}
