using ConSecOrg.Domain.Common;

namespace ConSecOrg.Domain.Entities;

public class SharedProject : AuditableEntity<Guid>
{
    public Guid OwnerUserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string InviteCode { get; private set; } = string.Empty;

    public User? Owner { get; private set; }
    public ICollection<SharedProjectMember> Members { get; private set; } = new List<SharedProjectMember>();

    protected SharedProject() { }

    public SharedProject(Guid id, Guid ownerUserId, string name) : base(id)
    {
        OwnerUserId = ownerUserId;
        Name = name;
        InviteCode = Guid.NewGuid().ToString("N")[..12].ToUpper();
    }

    public void Rename(string name) => Name = name;
}

public class SharedProjectMember : BaseEntity<Guid>
{
    public Guid ProjectId { get; private set; }
    public Guid UserId { get; private set; }
    public bool CanEdit { get; private set; }
    public DateTime JoinedAt { get; private set; }

    public SharedProject? Project { get; private set; }
    public User? User { get; private set; }

    protected SharedProjectMember() { }

    public SharedProjectMember(Guid projectId, Guid userId, bool canEdit = true) : base(Guid.NewGuid())
    {
        ProjectId = projectId;
        UserId = userId;
        CanEdit = canEdit;
        JoinedAt = DateTime.UtcNow;
    }
}
