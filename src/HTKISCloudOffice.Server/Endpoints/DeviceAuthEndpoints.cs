using System.Security.Claims;
using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HTKISCloudOffice.Server.Endpoints;

public static class DeviceAuthEndpoints
{
    public static void MapDeviceAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/auth");

        group.MapPost("/device-login", async (HttpContext ctx, IDeviceAuthService device_auth_svc) =>
        {
            try
            {
                using var reader = new StreamReader(ctx.Request.Body);
                var body = await reader.ReadToEndAsync();
                var request = System.Text.Json.JsonSerializer.Deserialize<DeviceLoginRequest>(body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (request == null) return Results.BadRequest(new { error = "Invalid request body" });

                var ip_address = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var result = await device_auth_svc.AutoLoginWithDeviceAsync(request.device_token, request.device_id, ip_address);

                return result.success
                    ? Results.Ok(ApiResponse<AuthResult>.Ok(result))
                    : Results.Unauthorized();
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).DisableAntiforgery();

        group.MapPost("/device-bind", async (HttpContext ctx, IDeviceAuthService device_auth_svc, ClaimsPrincipal user) =>
        {
            var user_id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value;
            if (user_id == null) return Results.Unauthorized();

            try
            {
                using var reader = new StreamReader(ctx.Request.Body);
                var body = await reader.ReadToEndAsync();
                var request = System.Text.Json.JsonSerializer.Deserialize<DeviceBindRequest>(body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (request == null) return Results.BadRequest(new { error = "Invalid request body" });

                var ip_address = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var result = await device_auth_svc.BindDeviceAsync(Guid.Parse(user_id), request.device_id, request.device_name, ip_address);

                return result.success
                    ? Results.Ok(ApiResponse<DeviceBindResult>.Ok(result))
                    : Results.BadRequest(ApiResponse<DeviceBindResult>.Fail(result.error_code, result.error_message));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization().DisableAntiforgery();

        group.MapDelete("/device-bind/{binding_id:guid}", async (Guid binding_id, IDeviceAuthService device_auth_svc, ClaimsPrincipal user, HttpContext ctx) =>
        {
            var user_id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value;
            if (user_id == null) return Results.Unauthorized();

            var ip_address = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            await device_auth_svc.UnbindDeviceAsync(binding_id, Guid.Parse(user_id), ip_address);
            return Results.Ok(ApiResponse<object>.Ok(null!));
        }).RequireAuthorization().DisableAntiforgery();

        group.MapGet("/devices", async (IDeviceAuthService device_auth_svc, ClaimsPrincipal user) =>
        {
            var user_id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value;
            if (user_id == null) return Results.Unauthorized();

            var devices = await device_auth_svc.GetUserDevicesAsync(Guid.Parse(user_id));
            return Results.Ok(ApiResponse<List<DeviceBindingDto>>.Ok(devices));
        }).RequireAuthorization();

        group.MapPost("/token-refresh", async (HttpContext ctx, IDeviceAuthService device_auth_svc, ClaimsPrincipal user) =>
        {
            var auth_header = ctx.Request.Headers.Authorization.ToString();
            if (string.IsNullOrEmpty(auth_header) || !auth_header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return Results.Unauthorized();

            var token = auth_header["Bearer ".Length..].Trim();
            var result = await device_auth_svc.RefreshTokenAsync(token);

            return result.success
                ? Results.Ok(ApiResponse<TokenRefreshResult>.Ok(result))
                : Results.Unauthorized();
        }).RequireAuthorization().DisableAntiforgery();
    }
}

public record DeviceLoginRequest(string device_token, string device_id);
public record DeviceBindRequest(string device_id, string device_name);