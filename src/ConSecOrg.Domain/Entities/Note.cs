using ConSecOrg.Domain.Common;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Domain.ValueObjects;

namespace ConSecOrg.Domain.Entities;

public class Note : AuditableEntity<Guid>
{
    public Guid UserId { get; private set; }
    public Guid? CategoryId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public EncryptedContent Content { get; private set; } = null!;
    public SecurityLevel SecurityLevel { get; private set; }
    public bool IsPinned { get; private set; }
    public bool IsTemplate { get; private set; }
    public DateTime? ExpiresAt { get; private set; }

    public User? User { get; private set; }
    public Category? Category { get; private set; }

    private readonly List<Tag> _tags = [];
    public IReadOnlyCollection<Tag> Tags => _tags.AsReadOnly();

    protected Note() { }

    public Note(Guid id, Guid userId, string title, EncryptedContent content,
        SecurityLevel securityLevel = SecurityLevel.Internal,
        Guid? categoryId = null,
        bool isTemplate = false) : base(id)
    {
        UserId = userId;
        Title = title;
        Content = content;
        SecurityLevel = securityLevel;
        CategoryId = categoryId;
        IsTemplate = isTemplate;
    }

    public void UpdateContent(string title, EncryptedContent content, SecurityLevel securityLevel, Guid? categoryId)
    {
        Title = title;
        Content = content;
        SecurityLevel = securityLevel;
        CategoryId = categoryId;
    }

    public void SetDestructionTimer(DateTime expiresAt)
    {
        if (expiresAt <= DateTime.UtcNow)
            throw new ArgumentException("Expiry must be in the future.", nameof(expiresAt));
        ExpiresAt = expiresAt;
    }

    public void ClearDestructionTimer() => ExpiresAt = null;

    public void Pin() => IsPinned = true;
    public void Unpin() => IsPinned = false;

    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value <= DateTime.UtcNow;
}
