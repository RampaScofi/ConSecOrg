using ConSecOrg.Application.Common.Interfaces;
using System.Security.Claims;

namespace ConSecOrg.Server.Middleware;

public class DeviceValidationMiddleware(RequestDelegate next, IConfiguration config)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        var requireBinding = config.GetValue<bool>("SecuritySettings:RequireDeviceBinding");

        if (requireBinding && ctx.User.Identity?.IsAuthenticated == true)
        {
            var deviceInToken = ctx.User.FindFirstValue("device_id");
            var deviceInHeader = ctx.Request.Headers["X-Device-Fingerprint"].FirstOrDefault();

            if (!string.IsNullOrEmpty(deviceInToken)
                && !string.IsNullOrEmpty(deviceInHeader)
                && deviceInToken != deviceInHeader)
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                await ctx.Response.WriteAsync("{\"message\":\"Device mismatch.\"}");
                return;
            }
        }

        await next(ctx);
    }
}
