using ConSecOrg.Shared.Enums;

namespace ConSecOrg.Shared.DTOs.Tasks;

public class CreateTaskRequestDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskPriorityDto Priority { get; set; } = TaskPriorityDto.Normal;
    public DateTime? DueDate { get; set; }
}
