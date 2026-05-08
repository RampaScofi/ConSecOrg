using ConSecOrg.Domain.Common;

namespace ConSecOrg.Domain.Entities;

public class Tag : BaseEntity<Guid>
{
    public string Name { get; private set; } = string.Empty;

    private readonly List<Note> _notes = [];
    public IReadOnlyCollection<Note> Notes => _notes.AsReadOnly();

    protected Tag() { }

    public Tag(Guid id, string name) : base(id)
    {
        Name = name;
    }
}
