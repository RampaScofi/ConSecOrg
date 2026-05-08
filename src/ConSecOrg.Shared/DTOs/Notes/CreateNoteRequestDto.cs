using ConSecOrg.Shared.Enums;

namespace ConSecOrg.Shared.DTOs.Notes;

public class CreateNoteRequestDto
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public SecurityLevelDto SecurityLevel { get; set; } = SecurityLevelDto.Internal;
    public bool IsPinned { get; set; }
    public bool IsTemplate { get; set; }
    public Guid? CategoryId { get; set; }
    public List<string> Tags { get; set; } = [];
    public DateTime? ExpiresAt { get; set; }
}
