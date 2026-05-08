namespace ConSecOrg.Shared.DTOs.Projects;

public class SharedProjectColumnDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#607D8B";
    public int Order { get; set; }
}

public class CreateSharedProjectColumnDto
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#607D8B";
}

public class UpdateSharedProjectColumnDto
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#607D8B";
    public int Order { get; set; }
}
