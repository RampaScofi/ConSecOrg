using ConSecOrg.Domain.Common;

namespace ConSecOrg.Domain.Entities;

public class SharedProjectColumn : BaseEntity<Guid>
{
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Color { get; private set; } = "#607D8B";
    public int Order { get; private set; }

    public SharedProject? Project { get; private set; }

    protected SharedProjectColumn() { }

    public SharedProjectColumn(Guid projectId, string name, string color, int order) : base(Guid.NewGuid())
    {
        ProjectId = projectId;
        Name = name;
        Color = color;
        Order = order;
    }

    public void Update(string name, string color, int order)
    {
        Name = name;
        Color = color;
        Order = order;
    }

    public void Rename(string name) => Name = name;
    public void SetColor(string color) => Color = color;
    public void SetOrder(int order) => Order = order;
}
