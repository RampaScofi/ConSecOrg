using ConSecOrg.Application.Features.Audit.Queries;
using ConSecOrg.Shared.DTOs.Audit;
using ConSecOrg.Shared.Pagination;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;

namespace ConSecOrg.Server.Controllers;

[ApiController]
[Route("api/v1/audit")]
[Authorize(Roles = "Admin,Auditor")]
public class AuditController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<AuditLogDto>>> GetLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] Guid? userId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? action = null,
        CancellationToken ct = default)
        => Ok(await sender.Send(new GetAuditLogsQuery(page, pageSize, userId, from, to, action), ct));

    [HttpGet("verify")]
    public async Task<ActionResult<AuditChainVerifyResultDto>> Verify(CancellationToken ct)
        => Ok(await sender.Send(new VerifyAuditChainQuery(), ct));

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var result = await sender.Send(new GetAuditLogsQuery(1, 10000), ct);
        var csv = new StringBuilder();
        csv.AppendLine("SequenceNum,Timestamp,UserId,Action,EntityType,EntityId,Status,IpAddress");
        foreach (var l in result.Items)
            csv.AppendLine($"{l.SequenceNum},{l.Timestamp:O},{l.UserId},{l.Action},{l.EntityType},{l.EntityId},{l.Status},{l.IpAddress}");

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "audit_export.csv");
    }
}
