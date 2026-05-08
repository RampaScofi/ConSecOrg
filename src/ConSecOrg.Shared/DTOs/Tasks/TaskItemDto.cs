using ConSecOrg.Shared.Enums;

namespace ConSecOrg.Shared.DTOs.Tasks;

public class TaskItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskStatusDto Status { get; set; }
    public TaskPriorityDto Priority { get; set; }
    public int BoardColumn { get; set; }
    public int ColumnPosition { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
