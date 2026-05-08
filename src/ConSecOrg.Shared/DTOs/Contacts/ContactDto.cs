namespace ConSecOrg.Shared.DTOs.Contacts;

public class ContactDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    // Client-side only: linked user ID for direct messaging
    public Guid? LinkedUserId { get; set; }
    // Client-side only: pinned state
    public bool IsPinned { get; set; }
}
