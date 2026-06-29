using System.Security.Claims;
using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace HTKISCloudOffice.Server.Endpoints;

public static class AuditLogEndpoints
{
    public static void MapAuditLogEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/v1/admin").RequireAuthorization("SuperAdmin");

        admin.MapGet("/audit-logs", async (
            Guid? user_id, AuditAction? action,
            DateTime? start_time, DateTime? end_time,
            int page = 1, int page_size = 50,
            IAuditService audit_svc = null!) =>
        {
            var filter = new AuditLogFilter
            {
                user_id = user_id,
                action = action,
                start_time = start_time,
                end_time = end_time,
                page = page,
                page_size = page_size
            };
            var result = await audit_svc.QueryLogsAsync(filter);
            return Results.Ok(ApiResponse<PagedResult<AuditLogDto>>.Ok(result));
        });
    }
}