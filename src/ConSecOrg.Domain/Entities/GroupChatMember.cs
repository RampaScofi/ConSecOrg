namespace ConSecOrg.Domain.Entities;

public class GroupChatMember
{
    public Guid GroupChatId { get; set; }
    public Guid UserId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public GroupChat? GroupChat { get; set; }
    public User? User { get; set; }
}
