using System.Security.Claims;
using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HTKISCloudOffice.Server.Endpoints;

public static class ConnectionEndpoints
{
    public static void MapConnectionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/connections").RequireAuthorization();

        group.MapGet("/", async (IConnectionService conn_svc, ClaimsPrincipal user) =>
        {
            var user_id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value ?? "";
            var connections = await conn_svc.GetAvailableConnectionsAsync(user_id);
            return Results.Ok(ApiResponse<List<ConnectionConfigDto>>.Ok(connections));
        });

        group.MapPost("/{connection_id:guid}/connect", async (Guid connection_id, IConnectionService conn_svc, ClaimsPrincipal user, HttpContext ctx) =>
        {
            var user_id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value ?? "";
            var ip_address = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var result = await conn_svc.CreateConnectionAsync(user_id, connection_id, ip_address);

            return result.success
                ? Results.Ok(ApiResponse<ConnectionConnectResult>.Ok(result))
                : result.error_code == "ACCESS_DENIED"
                    ? Results.Json(ApiResponse<ConnectionConnectResult>.Fail(result.error_code!, result.error_message!), statusCode: 403)
                    : Results.Json(ApiResponse<ConnectionConnectResult>.Fail(result.error_code!, result.error_message!), statusCode: 503);
        }).DisableAntiforgery();

        group.MapPost("/{connection_id:guid}/disconnect", async (Guid connection_id, [FromBody] ConnectionDisconnectRequest request, IConnectionService conn_svc, ClaimsPrincipal user, HttpContext ctx) =>
        {
            var user_id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value ?? "";
            var ip_address = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            await conn_svc.DisconnectAndKeepSessionAsync(user_id, request.guacamole_connection_id, connection_id, ip_address);
            return Results.Ok(ApiResponse<object>.Ok(null!));
        }).DisableAntiforgery();

        group.MapPost("/{connection_id:guid}/reconnect", async (Guid connection_id, [FromBody] ConnectionReconnectRequest request, IConnectionService conn_svc, ClaimsPrincipal user, HttpContext ctx) =>
        {
            var user_id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value ?? "";
            var ip_address = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var result = await conn_svc.ReconnectSessionAsync(user_id, request.guacamole_connection_id, connection_id, ip_address);

            return result.success
                ? Results.Ok(ApiResponse<ConnectionConnectResult>.Ok(result))
                : Results.Json(ApiResponse<ConnectionConnectResult>.Fail(result.error_code!, result.error_message!), statusCode: 503);
        }).DisableAntiforgery();
    }
}

public record ConnectionDisconnectRequest(string guacamole_connection_id);
public record ConnectionReconnectRequest(string guacamole_connection_id);