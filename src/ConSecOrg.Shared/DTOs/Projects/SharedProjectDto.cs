namespace ConSecOrg.Shared.DTOs.Projects;

public class SharedProjectDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string InviteCode { get; set; } = string.Empty;
    public Guid OwnerUserId { get; set; }
    public string OwnerUsername { get; set; } = string.Empty;
    public List<SharedProjectMemberDto> Members { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}

public class SharedProjectMemberDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool CanEdit { get; set; }
    public DateTime JoinedAt { get; set; }
}

public class CreateSharedProjectDto
{
    public string Name { get; set; } = string.Empty;
}

public class JoinSharedProjectDto
{
    public string InviteCode { get; set; } = string.Empty;
}
