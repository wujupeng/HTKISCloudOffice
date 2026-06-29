using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HTKISCloudOffice.Server.Endpoints;

public static class AppCenterEndpoints
{
    public static void MapAppCenterEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/applications").RequireAuthorization();

        group.MapGet("/center", async (IAppCenterService center_svc, System.Security.Claims.ClaimsPrincipal user) =>
        {
            var user_id = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value ?? "";
            var view = await center_svc.GetAppCenterViewAsync(user_id);
            return Results.Ok(ApiResponse<AppCenterView>.Ok(view));
        });

        group.MapGet("/search", async (string? q, IAppCenterService center_svc, System.Security.Claims.ClaimsPrincipal user) =>
        {
            var user_id = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value ?? "";
            var results = await center_svc.SearchApplicationsAsync(user_id, q ?? "");
            return Results.Ok(ApiResponse<List<ApplicationDto>>.Ok(results));
        });

        group.MapPost("/{app_id:guid}/favorite", async (Guid app_id, IAppCenterService center_svc, System.Security.Claims.ClaimsPrincipal user) =>
        {
            var user_id = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value ?? "";
            var result = await center_svc.AddFavoriteAsync(user_id, app_id.ToString());
            return result.success
                ? Results.Ok(ApiResponse<FavoriteResult>.Ok(result))
                : Results.Conflict(ApiResponse<FavoriteResult>.Fail(result.error_code, result.error_message));
        }).DisableAntiforgery();

        group.MapDelete("/{app_id:guid}/favorite", async (Guid app_id, IAppCenterService center_svc, System.Security.Claims.ClaimsPrincipal user) =>
        {
            var user_id = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value ?? "";
            var result = await center_svc.RemoveFavoriteAsync(user_id, app_id.ToString());
            return result.success
                ? Results.Ok(ApiResponse<FavoriteResult>.Ok(result))
                : Results.BadRequest(ApiResponse<FavoriteResult>.Fail(result.error_code, result.error_message));
        });

        group.MapGet("/favorites", async (IAppCenterService center_svc, System.Security.Claims.ClaimsPrincipal user) =>
        {
            var user_id = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value ?? "";
            var favorites = await center_svc.GetUserFavoritesAsync(user_id);
            return Results.Ok(ApiResponse<List<ApplicationDto>>.Ok(favorites));
        });
    }
}