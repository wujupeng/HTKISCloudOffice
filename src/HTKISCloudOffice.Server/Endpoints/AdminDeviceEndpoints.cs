using System.Security.Claims;
using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HTKISCloudOffice.Server.Endpoints;

public static class AdminDeviceEndpoints
{
    public static void MapAdminDeviceEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/v1/admin/devices").RequireAuthorization("SuperAdmin");

        admin.MapGet("/", async (Guid? user_id, bool? is_active, int page, int page_size, IDeviceAuthService device_auth_svc) =>
        {
            var filter = new DeviceBindingFilter
            {
                user_id = user_id,
                is_active = is_active,
                page = page < 1 ? 1 : page,
                page_size = page_size < 1 ? 20 : page_size
            };

            var result = await device_auth_svc.GetAllDeviceBindingsAsync(filter);
            return Results.Ok(ApiResponse<PagedResult<DeviceBindingDto>>.Ok(result));
        });

        admin.MapDelete("/{binding_id:guid}", async (Guid binding_id, IDeviceAuthService device_auth_svc, ClaimsPrincipal user, HttpContext ctx) =>
        {
            var user_id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value;
            if (user_id == null) return Results.Unauthorized();

            var ip_address = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            await device_auth_svc.UnbindDeviceAsync(binding_id, Guid.Parse(user_id), ip_address);
            return Results.Ok(ApiResponse<object>.Ok(null!));
        }).DisableAntiforgery();
    }
}