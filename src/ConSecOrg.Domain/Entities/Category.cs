using ConSecOrg.Domain.Common;

namespace ConSecOrg.Domain.Entities;

public class Category : AuditableEntity<Guid>
{
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Color { get; private set; } = "#607D8B";
    public string? Icon { get; private set; }

    private readonly List<Note> _notes = [];
    public IReadOnlyCollection<Note> Notes => _notes.AsReadOnly();

    protected Category() { }

    public Category(Guid id, Guid userId, string name, string color, string? icon = null) : base(id)
    {
        UserId = userId;
        Name = name;
        Color = color;
        Icon = icon;
    }

    public void Update(string name, string color, string? icon)
    {
        Name = name;
        Color = color;
        Icon = icon;
    }
}
