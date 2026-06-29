using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace HTKISCloudOffice.Server.Endpoints;

public static class AppPortalEndpoints
{
    public static void MapAppPortalEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/applications").RequireAuthorization();

        group.MapGet("/", async (string? category, IAppPortalService portal_svc, System.Security.Claims.ClaimsPrincipal user) =>
        {
            var user_id = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value ?? "";
            if (!string.IsNullOrEmpty(category) && Enum.TryParse<AppCategory>(category, true, out var cat))
            {
                var apps = await portal_svc.GetApplicationsByCategoryAsync(user_id, cat);
                return Results.Ok(ApiResponse<List<ApplicationDto>>.Ok(apps));
            }
            var all_apps = await portal_svc.GetApplicationsForUserAsync(user_id);
            return Results.Ok(ApiResponse<ApplicationsResponse>.Ok(new ApplicationsResponse { applications = all_apps }));
        });

        group.MapPost("/{app_id:guid}/launch", async (Guid app_id, IAppPortalService portal_svc, System.Security.Claims.ClaimsPrincipal user) =>
        {
            var user_id = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value ?? "";
            var result = await portal_svc.LaunchApplicationAsync(user_id, app_id.ToString());

            return result.success
                ? Results.Ok(ApiResponse<LaunchResult>.Ok(result))
                : Results.Ok(ApiResponse<LaunchResult>.Fail(result.error_code, result.error_message));
        });
    }
}

public record ApplicationsResponse
{
    public List<ApplicationDto> applications { get; init; } = new();
}