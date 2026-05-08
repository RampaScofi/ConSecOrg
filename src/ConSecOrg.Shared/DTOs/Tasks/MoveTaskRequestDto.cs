using ConSecOrg.Shared.Enums;

namespace ConSecOrg.Shared.DTOs.Tasks;

public class MoveTaskRequestDto
{
    public TaskStatusDto NewStatus { get; set; }
    public int NewColumn { get; set; }
    public int NewPosition { get; set; }
}
