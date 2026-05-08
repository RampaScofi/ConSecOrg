using ConSecOrg.Shared.DTOs.Dashboard;

namespace ConSecOrg.Client.Infrastructure.Api;

public interface IDashboardApiService
{
    Task<SecurityDashboardDto> GetSecurityDashboardAsync();
}
