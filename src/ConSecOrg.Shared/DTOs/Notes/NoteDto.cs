using ConSecOrg.Shared.Enums;

namespace ConSecOrg.Shared.DTOs.Notes;

public class NoteDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public SecurityLevelDto SecurityLevel { get; set; }
    public bool IsPinned { get; set; }
    public bool IsTemplate { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public List<string> Tags { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
