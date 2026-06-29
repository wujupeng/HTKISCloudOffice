using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HTKISCloudOffice.Server.Endpoints;

public static class AdminIconEndpoints
{
    public static void MapAdminIconEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/v1/admin/icons").RequireAuthorization("SuperAdmin");

        admin.MapGet("/", async (IAppCenterService center_svc) =>
        {
            var icons = await center_svc.GetAppIconsAsync();
            return Results.Ok(ApiResponse<List<AppIconDto>>.Ok(icons));
        });

        admin.MapPost("/", async (HttpContext ctx, IAppCenterService center_svc, System.Security.Claims.ClaimsPrincipal user) =>
        {
            var form = await ctx.Request.ReadFormAsync();
            var file = form.Files.GetFile("file");
            if (file == null || file.Length == 0)
                return Results.BadRequest(ApiResponse<AppIconDto>.Fail("NO_FILE", "未提供图标文件"));

            var icon_name = form["icon_name"].ToString() ?? file.FileName;
            var user_id = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var icon_data = ms.ToArray();

            var result = await center_svc.UploadAppIconAsync(icon_name, icon_data, file.ContentType, user_id);
            return Results.Ok(ApiResponse<AppIconDto>.Ok(result));
        }).DisableAntiforgery();
    }
}