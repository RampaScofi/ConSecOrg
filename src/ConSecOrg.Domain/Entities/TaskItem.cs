using ConSecOrg.Domain.Common;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.ValueObjects;

namespace ConSecOrg.Domain.Entities;

public class TaskItem : AuditableEntity<Guid>
{
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public EncryptedContent? Description { get; private set; }
    public TaskItemStatus Status { get; private set; }
    public TaskPriority Priority { get; private set; }
    public int BoardColumn { get; private set; }
    public int ColumnPosition { get; private set; }
    public DateTime? DueDate { get; private set; }

    public User? User { get; private set; }

    protected TaskItem() { }

    public TaskItem(Guid id, Guid userId, string title,
        TaskPriority priority = TaskPriority.Normal) : base(id)
    {
        UserId = userId;
        Title = title;
        Priority = priority;
        Status = TaskItemStatus.ToDo;
        BoardColumn = 0;
        ColumnPosition = 0;
    }

    public void UpdateDetails(string title, EncryptedContent? description, TaskPriority priority, DateTime? dueDate)
    {
        Title = title;
        Description = description;
        Priority = priority;
        DueDate = dueDate;
    }

    public void MoveTo(TaskItemStatus newStatus, int column, int position)
    {
        Status = newStatus;
        BoardColumn = column;
        ColumnPosition = position;
    }
}
