using ConSecOrg.Domain.Common;

namespace ConSecOrg.Domain.Entities;

public class SharedProjectTask : AuditableEntity<Guid>
{
    public Guid ProjectId { get; private set; }
    public Guid? ColumnId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string Tags { get; private set; } = string.Empty; // JSON array stored as string
    public string Status { get; private set; } = "todo";
    public int Priority { get; private set; } = 1;
    public DateTime? DueDate { get; private set; }
    public int Position { get; private set; }
    public bool IsCompleted { get; private set; }
    public Guid? AssignedUserId { get; private set; }

    public SharedProject? Project { get; private set; }
    public User? AssignedUser { get; private set; }

    protected SharedProjectTask() { }

    public SharedProjectTask(Guid id, Guid projectId, string title, Guid? columnId, int position) : base(id)
    {
        ProjectId = projectId;
        ColumnId = columnId;
        Title = title;
        Position = position;
    }

    public void Update(string title, string? description, Guid? columnId, int priority,
        DateTime? dueDate, bool isCompleted, int position, Guid? assignedUserId, string tags = "[]")
    {
        Title = title;
        Description = description;
        ColumnId = columnId;
        Priority = priority;
        DueDate = dueDate;
        IsCompleted = isCompleted;
        Position = position;
        AssignedUserId = assignedUserId;
        Tags = tags;
    }

    public void SetCompleted(bool completed) => IsCompleted = completed;
    public void MoveToColumn(Guid? columnId, int position) { ColumnId = columnId; Position = position; }
}
