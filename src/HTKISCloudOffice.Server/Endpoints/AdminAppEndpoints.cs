using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HTKISCloudOffice.Server.Endpoints;

public static class AdminAppEndpoints
{
    public static void MapAdminAppEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/v1/admin/applications").RequireAuthorization("SuperAdmin");

        admin.MapGet("/", async (IApplicationRepository app_repo) =>
        {
            var apps = await app_repo.GetAllAsync(include_inactive: true);
            var dtos = apps.Select(a => new ApplicationDto
            {
                app_id = a.app_id.ToString(),
                app_name = a.app_name,
                app_type = a.app_type,
                icon_url = a.icon_url,
                category = a.category,
                description = a.launch_params
            }).ToList();
            return Results.Ok(ApiResponse<List<ApplicationDto>>.Ok(dtos));
        });

        admin.MapPost("/", async ([FromBody] CreateAppRequest request, IApplicationRepository app_repo) =>
        {
            var app = new Domain.Entities.Application
            {
                app_name = request.app_name,
                app_type = request.app_type,
                icon_url = request.icon_url,
                category = request.category,
                launch_params = request.launch_params,
                sort_order = request.sort_order
            };
            await app_repo.CreateAsync(app);
            return Results.Ok(ApiResponse<object>.Ok(new { app_id = app.app_id }));
        });

        admin.MapPut("/{app_id:guid}", async (Guid app_id, [FromBody] UpdateAppRequest request, IApplicationRepository app_repo) =>
        {
            var app = await app_repo.GetByIdAsync(app_id);
            if (app == null) return Results.NotFound();
            app.app_name = request.app_name ?? app.app_name;
            app.launch_params = request.launch_params ?? app.launch_params;
            app.is_active = request.is_active ?? app.is_active;
            await app_repo.UpdateAsync(app);
            return Results.Ok(ApiResponse<object>.Ok(null!));
        });

        admin.MapDelete("/{app_id:guid}", async (Guid app_id, IApplicationRepository app_repo) =>
        {
            var app = await app_repo.GetByIdAsync(app_id);
            if (app == null) return Results.NotFound();
            app.is_active = false;
            await app_repo.UpdateAsync(app);
            return Results.Ok(ApiResponse<object>.Ok(null!));
        });
    }
}

public record CreateAppRequest(string app_name, Domain.Enums.AppType app_type, string icon_url, Domain.Enums.AppCategory category, string launch_params, int sort_order);
public record UpdateAppRequest(string? app_name, string? launch_params, bool? is_active);