namespace ConSecOrg.Shared.DTOs.Projects;

public class GroupChatDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<GroupChatMemberDto> Members { get; set; } = [];
}

public class GroupChatMemberDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
}

public class CreateGroupChatDto
{
    public string Name { get; set; } = string.Empty;
    public List<Guid> MemberIds { get; set; } = [];
}

