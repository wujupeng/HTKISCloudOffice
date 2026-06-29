using System.Security.Claims;
using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HTKISCloudOffice.Server.Endpoints;

public static class PermissionEndpoints
{
    public static void MapPermissionEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/v1/admin").RequireAuthorization("SuperAdmin");

        admin.MapPut("/users/{user_id:guid}/roles", async (Guid user_id, [FromBody] UpdateRolesRequest request, IPermissionService perm_svc, ClaimsPrincipal user) =>
        {
            var operator_id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? user.FindFirst("sub")?.Value ?? "";
            await perm_svc.AssignRolesAsync(user_id.ToString(), request.role_ids, operator_id);
            return Results.Ok(ApiResponse<object>.Ok(null!));
        });

        admin.MapGet("/roles", async (IPermissionService perm_svc) =>
        {
            var roles = await perm_svc.GetAllRolesAsync();
            return Results.Ok(ApiResponse<List<RoleDto>>.Ok(roles));
        });

        admin.MapPost("/roles", async ([FromBody] CreateRoleRequest request, IPermissionService perm_svc, ClaimsPrincipal user) =>
        {
            var operator_id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? user.FindFirst("sub")?.Value ?? "";
            var role = await perm_svc.CreateRoleAsync(request, operator_id);
            return Results.Ok(ApiResponse<RoleDto>.Ok(role));
        });
    }
}

public record UpdateRolesRequest(List<string> role_ids);