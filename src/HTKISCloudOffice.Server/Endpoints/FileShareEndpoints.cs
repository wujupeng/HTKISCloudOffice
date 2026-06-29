using System.Security.Claims;
using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HTKISCloudOffice.Server.Endpoints;

public static class FileShareEndpoints
{
    public static void MapFileShareEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/shared-drives").RequireAuthorization();

        group.MapGet("/", async (IFileShareService file_svc, ClaimsPrincipal user) =>
        {
            var user_id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value ?? "";
            var drives = await file_svc.GetSharedDrivesForUserAsync(user_id);
            return Results.Ok(ApiResponse<SharedDrivesResponse>.Ok(new SharedDrivesResponse { drives = drives }));
        });

        group.MapGet("/{drive_id:guid}/quota", async (Guid drive_id, IFileShareService file_svc) =>
        {
            var quota = await file_svc.CheckDiskQuotaAsync(drive_id.ToString());
            return Results.Ok(ApiResponse<QuotaInfo>.Ok(quota));
        });
    }
}

public record SharedDrivesResponse
{
    public List<SharedDriveDto> drives { get; init; } = new();
}