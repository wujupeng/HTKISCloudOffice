using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HTKISCloudOffice.Server.Endpoints;

public static class FileCenterEndpoints
{
    public static void MapFileCenterEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/file-center").RequireAuthorization();

        group.MapGet("/drives", async (IFileCenterService file_svc, System.Security.Claims.ClaimsPrincipal user) =>
        {
            var user_id = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value ?? "";
            var drives = await file_svc.GetDrivesForUserAsync(user_id);
            return Results.Ok(ApiResponse<List<FileDriveDto>>.Ok(drives));
        });

        group.MapGet("/drives/{drive_id:guid}/files", async (Guid drive_id, string? path, IFileCenterService file_svc, System.Security.Claims.ClaimsPrincipal user) =>
        {
            var user_id = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value ?? "";
            var result = await file_svc.ListFilesAsync(user_id, drive_id.ToString(), path);
            return result.success
                ? Results.Ok(ApiResponse<FileListResult>.Ok(result))
                : Results.BadRequest(ApiResponse<FileListResult>.Fail(result.error_code, result.error_message));
        });

        group.MapPost("/drives/{drive_id:guid}/upload", async (Guid drive_id, HttpContext ctx, IFileCenterService file_svc, System.Security.Claims.ClaimsPrincipal user) =>
        {
            var user_id = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value ?? "";
            var form = await ctx.Request.ReadFormAsync();
            var file = form.Files.GetFile("file");
            var path = form["path"].ToString();

            if (file == null || file.Length == 0)
                return Results.BadRequest(ApiResponse<FileUploadResult>.Fail("NO_FILE", "未提供文件"));

            using var stream = file.OpenReadStream();
            var ip_address = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var result = await file_svc.UploadFileAsync(user_id, drive_id.ToString(), path, file.FileName, stream, file.Length, ip_address);
            return result.success
                ? Results.Ok(ApiResponse<FileUploadResult>.Ok(result))
                : Results.BadRequest(ApiResponse<FileUploadResult>.Fail(result.error_code, result.error_message));
        }).DisableAntiforgery();

        group.MapGet("/drives/{drive_id:guid}/download", async (Guid drive_id, string file_path, IFileCenterService file_svc, System.Security.Claims.ClaimsPrincipal user) =>
        {
            var user_id = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value ?? "";
            var result = await file_svc.DownloadFileAsync(user_id, drive_id.ToString(), file_path);
            if (!result.success) return Results.BadRequest(result.error_message);
            return Results.File(result.content_stream!, result.content_type, result.file_name);
        });

        group.MapGet("/drives/{drive_id:guid}/preview", async (Guid drive_id, string file_path, IFileCenterService file_svc, System.Security.Claims.ClaimsPrincipal user) =>
        {
            var user_id = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value ?? "";
            var result = await file_svc.PreviewFileAsync(user_id, drive_id.ToString(), file_path);
            if (!result.success) return Results.BadRequest(result.error_message);
            if (result.preview_type == "text") return Results.Ok(new { type = "text", content = result.text_content });
            return Results.File(result.content_stream!, result.content_type ?? "application/octet-stream");
        });

        group.MapDelete("/drives/{drive_id:guid}/files", async (Guid drive_id, string file_path, IFileCenterService file_svc, HttpContext ctx, System.Security.Claims.ClaimsPrincipal user) =>
        {
            var user_id = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value ?? "";
            var ip_address = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var result = await file_svc.DeleteFileAsync(user_id, drive_id.ToString(), file_path, ip_address);
            return result.success
                ? Results.Ok(ApiResponse<FileOperationResult>.Ok(result))
                : Results.BadRequest(ApiResponse<FileOperationResult>.Fail(result.error_code, result.error_message));
        });

        group.MapPost("/drives/{drive_id:guid}/directories", async (Guid drive_id, [FromBody] CreateDirectoryRequest request, IFileCenterService file_svc, System.Security.Claims.ClaimsPrincipal user) =>
        {
            var user_id = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value ?? "";
            var result = await file_svc.CreateDirectoryAsync(user_id, drive_id.ToString(), request.path, request.dir_name);
            return result.success
                ? Results.Ok(ApiResponse<DirectoryCreateResult>.Ok(result))
                : Results.BadRequest(ApiResponse<DirectoryCreateResult>.Fail(result.error_code, result.error_message));
        }).DisableAntiforgery();
    }
}

public record CreateDirectoryRequest(string path, string dir_name);