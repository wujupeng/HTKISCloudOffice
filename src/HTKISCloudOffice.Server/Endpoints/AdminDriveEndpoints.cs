using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HTKISCloudOffice.Server.Endpoints;

public static class AdminDriveEndpoints
{
    public static void MapAdminDriveEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/v1/admin/shared-drives").RequireAuthorization("SuperAdmin");

        admin.MapGet("/", async (ISharedDriveRepository drive_repo) =>
        {
            var drives = await drive_repo.GetAllAsync();
            return Results.Ok(ApiResponse<object>.Ok(drives));
        });

        admin.MapPost("/", async ([FromBody] CreateSharedDriveRequest request, IFileShareService file_svc) =>
        {
            var result = await file_svc.CreateSharedDriveAsync(request);
            return Results.Ok(ApiResponse<SharedDriveDto>.Ok(result));
        });

        admin.MapPut("/{drive_id:guid}", async (Guid drive_id, [FromBody] List<DrivePermissionDto> permissions, IFileShareService file_svc) =>
        {
            await file_svc.UpdateDrivePermissionsAsync(drive_id.ToString(), permissions);
            return Results.Ok(ApiResponse<object>.Ok(null!));
        });

        admin.MapDelete("/{drive_id:guid}", async (Guid drive_id, ISharedDriveRepository drive_repo, ISambaConfigManager samba_mgr) =>
        {
            var drive = await drive_repo.GetByIdAsync(drive_id);
            if (drive == null) return Results.NotFound();
            drive.is_active = false;
            await drive_repo.UpdateAsync(drive);
            await samba_mgr.RemoveShareAsync(drive.samba_path.Split('/').Last());
            return Results.Ok(ApiResponse<object>.Ok(null!));
        });
    }
}