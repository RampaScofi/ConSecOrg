namespace ConSecOrg.Shared.DTOs.Audit;

public class AuditChainVerifyResultDto
{
    public bool IsIntact { get; set; }
    public long? TamperedAtSequence { get; set; }
    public string Message { get; set; } = string.Empty;
    public int TotalRecordsChecked { get; set; }
    public DateTime VerifiedAt { get; set; } = DateTime.UtcNow;
}
