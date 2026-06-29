using System.Security.Claims;
using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HTKISCloudOffice.Server.Endpoints;

public static class AdminConnectionEndpoints
{
    public static void MapAdminConnectionEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/v1/admin/connections").RequireAuthorization("SuperAdmin");

        admin.MapGet("/", async (IConnectionService conn_svc) =>
        {
            var configs = await conn_svc.GetAllConnectionConfigsAsync();
            return Results.Ok(ApiResponse<List<ConnectionConfigDto>>.Ok(configs));
        });

        admin.MapPost("/", async ([FromBody] CreateConnectionConfigRequest request, IConnectionService conn_svc, ClaimsPrincipal user, HttpContext ctx) =>
        {
            var operator_id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? user.FindFirst("sub")?.Value ?? "";
            var ip_address = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var result = await conn_svc.CreateConnectionConfigAsync(request, operator_id, ip_address);
            return Results.Ok(ApiResponse<ConnectionConfigDto>.Ok(result));
        }).DisableAntiforgery();

        admin.MapPut("/{connection_id:guid}", async (Guid connection_id, [FromBody] UpdateConnectionConfigRequest request, IConnectionService conn_svc, ClaimsPrincipal user, HttpContext ctx) =>
        {
            var operator_id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? user.FindFirst("sub")?.Value ?? "";
            var ip_address = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var result = await conn_svc.UpdateConnectionConfigAsync(connection_id, request, operator_id, ip_address);
            return Results.Ok(ApiResponse<ConnectionConfigDto>.Ok(result));
        }).DisableAntiforgery();

        admin.MapDelete("/{connection_id:guid}", async (Guid connection_id, IConnectionService conn_svc, ClaimsPrincipal user, HttpContext ctx) =>
        {
            var operator_id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? user.FindFirst("sub")?.Value ?? "";
            var ip_address = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            await conn_svc.DeleteConnectionConfigAsync(connection_id, operator_id, ip_address);
            return Results.Ok(ApiResponse<object>.Ok(null!));
        }).DisableAntiforgery();
    }
}