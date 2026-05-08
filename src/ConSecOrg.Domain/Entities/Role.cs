using ConSecOrg.Domain.Common;

namespace ConSecOrg.Domain.Entities;

public class Role : BaseEntity<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public string PermissionsJson { get; private set; } = "{}";

    private readonly List<User> _users = [];
    public IReadOnlyCollection<User> Users => _users.AsReadOnly();

    protected Role() { }

    public Role(Guid id, string name, string permissionsJson) : base(id)
    {
        Name = name;
        PermissionsJson = permissionsJson;
    }
}
