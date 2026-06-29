using System.Security.Claims;
using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HTKISCloudOffice.Server.Endpoints;

public static class DesktopEndpoints
{
    public static void MapDesktopEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/desktop").RequireAuthorization();

        group.MapPost("/connect", async ([FromBody] DesktopConnectRequest request, IDesktopService desktop_svc, ClaimsPrincipal user) =>
        {
            var user_id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value ?? "";
            var result = await desktop_svc.ConnectAsync(user_id, request.app_id);

            return result.success
                ? Results.Ok(ApiResponse<DesktopConnectionResult>.Ok(result))
                : Results.Json(ApiResponse<DesktopConnectionResult>.Fail(result.error_code, result.error_message),
                    statusCode: 503);
        });

        group.MapPost("/disconnect", async ([FromBody] DesktopDisconnectRequest request, IDesktopService desktop_svc, ClaimsPrincipal user) =>
        {
            var user_id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value ?? "";
            await desktop_svc.DisconnectAsync(user_id, request.connection_id);
            return Results.Ok(ApiResponse<object>.Ok(null!));
        });

        group.MapGet("/status", async (IDesktopService desktop_svc, ClaimsPrincipal user) =>
        {
            var user_id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value ?? "";
            var vm = await desktop_svc.GetUserBoundVmAsync(user_id);
            return Results.Ok(ApiResponse<object>.Ok(new
            {
                vm_name = vm?.vm_name,
                status = vm != null ? "available" : "no_vm_bound"
            }));
        });
    }
}

public record DesktopConnectRequest(string? app_id);
public record DesktopDisconnectRequest(string connection_id);