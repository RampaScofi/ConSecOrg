using ConSecOrg.Shared.DTOs.Audit;

namespace ConSecOrg.Shared.DTOs.Dashboard;

public class SecurityDashboardDto
{
    public string EncryptionAlgorithm { get; set; } = "ГОСТ Р 34.12-2015 «Кузнечик»";
    public string HashAlgorithm { get; set; } = "ГОСТ Р 34.11-2012 «Стрибог-256»";
    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }
    public int ActiveSessionsCount { get; set; }
    public NotesByLevelDto NotesByLevel { get; set; } = new();
    public List<AuditLogDto> RecentAuditEvents { get; set; } = [];
}

public class NotesByLevelDto
{
    public int Public { get; set; }
    public int Internal { get; set; }
    public int Confidential { get; set; }
    public int Secret { get; set; }
    public int Total => Public + Internal + Confidential + Secret;
}
