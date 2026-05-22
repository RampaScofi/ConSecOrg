using ConSecOrg.Shared.DTOs.Audit;
using ConSecOrg.Shared.Pagination;

namespace ConSecOrg.Client.Infrastructure.Api;

public interface IAuditApiService
{
    Task<PagedResponse<AuditLogDto>> GetLogsAsync(int page = 1, int pageSize = 50, DateTime? from = null, DateTime? to = null, string? action = null);
    Task<AuditChainVerifyResultDto> VerifyChainAsync();
    Task<string> RecomputeChainAsync();
    Task<byte[]> ExportCsvAsync();
}
