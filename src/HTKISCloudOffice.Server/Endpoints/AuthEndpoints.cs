using System.Security.Claims;
using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HTKISCloudOffice.Server.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/auth");

        group.MapPost("/login", async (HttpContext ctx, IAuthService auth_svc) =>
        {
            try
            {
                using var reader = new StreamReader(ctx.Request.Body);
                var body = await reader.ReadToEndAsync();
                var request = System.Text.Json.JsonSerializer.Deserialize<LoginRequest>(body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (request == null) return Results.BadRequest(new { error = "Invalid request body" });

                var ip_address = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var result = await auth_svc.LoginAsync(request.username, request.password, ip_address);

                return result.success
                    ? Results.Ok(ApiResponse<AuthResult>.Ok(result))
                    : Results.Unauthorized();
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).DisableAntiforgery();

        group.MapPost("/auto-login", async ([FromBody] AutoLoginRequest request, IAuthService auth_svc, HttpContext ctx) =>
        {
            var ip_address = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var result = await auth_svc.AutoLoginAsync(request.auto_login_token, ip_address);

            return result.success
                ? Results.Ok(ApiResponse<AuthResult>.Ok(result))
                : Results.Unauthorized();
        }).DisableAntiforgery();

        group.MapPost("/logout", async (IAuthService auth_svc, ClaimsPrincipal user) =>
        {
            var user_id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value;
            if (user_id != null)
            {
                await auth_svc.RevokeSessionAsync(user_id);
            }
            return Results.Ok(ApiResponse<object>.Ok(null!));
        }).RequireAuthorization();
    }
}

public record LoginRequest(string username, string password);
public record AutoLoginRequest(string auto_login_token);