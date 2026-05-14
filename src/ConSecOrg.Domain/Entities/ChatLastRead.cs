namespace ConSecOrg.Domain.Entities;

public class ChatLastRead
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string ChatKey { get; set; } = string.Empty; // "user:{id}", "project:{id}", "group:{id}"
    public DateTime LastReadAt { get; set; }

    public User? User { get; set; }
}
