using ConSecOrg.Application.Features.Dashboard.Queries;
using ConSecOrg.Shared.DTOs.Dashboard;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConSecOrg.Server.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
public class DashboardController(ISender sender) : ControllerBase
{
    [HttpGet("security")]
    public async Task<ActionResult<SecurityDashboardDto>> Security(CancellationToken ct)
        => Ok(await sender.Send(new GetSecurityDashboardQuery(), ct));
}
