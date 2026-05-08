using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace ConSecOrg.Server.Hubs;

[Authorize]
public class BoardHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        // Auto-join personal group for direct messages
        var userIdClaim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? Context.User?.FindFirstValue("sub");
        if (Guid.TryParse(userIdClaim, out var userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        await base.OnConnectedAsync();
    }

    // Legacy single-board sync (kept for backward compat)
    public async Task TaskMoved(Guid taskId, int newColumn, int newStatus)
    {
        await Clients.OthersInGroup("board").SendAsync("TaskMoved", taskId, newColumn, newStatus);
    }

    public Task JoinBoard() => Groups.AddToGroupAsync(Context.ConnectionId, "board");
    public Task LeaveBoard() => Groups.RemoveFromGroupAsync(Context.ConnectionId, "board");

    // Per-shared-project sync
    public Task JoinProject(Guid projectId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, ProjectGroup(projectId));

    public Task LeaveProject(Guid projectId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, ProjectGroup(projectId));

    public static string ProjectGroup(Guid projectId) => $"project:{projectId:N}";
    public static string UserGroup(Guid userId) => $"user:{userId:N}";
}
