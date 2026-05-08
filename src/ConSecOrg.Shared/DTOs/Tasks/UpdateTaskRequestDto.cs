using ConSecOrg.Shared.Enums;

namespace ConSecOrg.Shared.DTOs.Tasks;

public class UpdateTaskRequestDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskStatusDto Status { get; set; }
    public TaskPriorityDto Priority { get; set; }
    public DateTime? DueDate { get; set; }
}
