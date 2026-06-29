using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using HTKISCloudOffice.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;
using HTKISCloudOffice.Server.Hubs;

namespace HTKISCloudOffice.Server.Middleware;

public class TokenAutoRefreshMiddleware
{
    private readonly RequestDelegate _next;
    private const string NewTokenHeader = "X-New-Token";
    private const int RefreshThresholdMinutes = 30;
    private const int ExpiringThresholdMinutes = 5;

    public TokenAutoRefreshMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IDeviceAuthService device_auth_svc, IHubContext<DesktopHub> hub_context)
    {
        var auth_header = context.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(auth_header) && auth_header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = auth_header["Bearer ".Length..].Trim();
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(token);
                var expires = jwt.ValidTo;
                var remaining = expires - DateTime.UtcNow;

                if (remaining.TotalMinutes <= ExpiringThresholdMinutes && remaining.TotalMinutes > 0)
                {
                    var user_id_claim = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");
                    if (user_id_claim != null)
                    {
                        _ = hub_context.Clients.User(user_id_claim.Value).SendAsync("TokenExpiring", new
                        {
                            expires_at = expires,
                            message = "会话即将过期，请重新登录"
                        });
                    }
                }

                if (remaining.TotalMinutes <= RefreshThresholdMinutes && remaining.TotalMinutes > 0)
                {
                    var refresh_result = await device_auth_svc.RefreshTokenAsync(token);
                    if (refresh_result.success)
                    {
                        context.Response.Headers.Append(NewTokenHeader, refresh_result.token);
                    }
                }
            }
            catch
            {
            }
        }

        await _next(context);
    }
}