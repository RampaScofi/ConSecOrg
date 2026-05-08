namespace ConSecOrg.Shared.DTOs.Projects;

public class SharedProjectTaskDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ColumnId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "todo";
    public int Priority { get; set; }
    public DateTime? DueDate { get; set; }
    public int Position { get; set; }
    public bool IsCompleted { get; set; }
    public Guid? AssignedUserId { get; set; }
    public string? AssignedUsername { get; set; }
    public List<string> Tags { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateSharedProjectTaskDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ColumnId { get; set; }
    public int Priority { get; set; }
    public DateTime? DueDate { get; set; }
    public Guid? AssignedUserId { get; set; }
    public List<string> Tags { get; set; } = [];
}

public class UpdateSharedProjectTaskDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ColumnId { get; set; }
    public int Priority { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsCompleted { get; set; }
    public int Position { get; set; }
    public Guid? AssignedUserId { get; set; }
    public List<string> Tags { get; set; } = [];
}

public class MoveSharedTaskDto
{
    public Guid ColumnId { get; set; }
    public int Position { get; set; }
}
