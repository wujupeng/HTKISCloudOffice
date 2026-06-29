using System.Text.Json;
using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Domain.Entities;
using HTKISCloudOffice.Domain.Enums;

namespace HTKISCloudOffice.Application.Services;

public class AuditService : IAuditService
{
    private readonly IAuditLogRepository _audit_repo;

    public AuditService(IAuditLogRepository audit_repo)
    {
        _audit_repo = audit_repo;
    }

    public async Task LogAsync(AuditLogEntry entry)
    {
        var log = new AuditLog
        {
            user_id = entry.user_id,
            action = entry.action,
            resource_type = entry.resource_type,
            resource_id = entry.resource_id,
            detail = entry.detail,
            ip_address = entry.ip_address
        };
        await _audit_repo.LogAsync(log);
    }

    public async Task LogLoginAsync(Guid user_id, string ip_address, bool success)
    {
        await LogAsync(new AuditLogEntry
        {
            user_id = user_id,
            action = AuditAction.Login,
            resource_type = "user",
            resource_id = user_id,
            detail = JsonSerializer.Serialize(new { success }),
            ip_address = ip_address
        });
    }

    public async Task LogPermissionChangeAsync(string operator_id, string target_user_id, string detail, string ip_address)
    {
        await LogAsync(new AuditLogEntry
        {
            user_id = Guid.TryParse(operator_id, out var op_id) ? op_id : Guid.Empty,
            action = AuditAction.PermissionChange,
            resource_type = "user",
            resource_id = Guid.TryParse(target_user_id, out var tid) ? tid : Guid.Empty,
            detail = detail,
            ip_address = ip_address
        });
    }

    public async Task LogAppLaunchAsync(Guid user_id, Guid app_id, string ip_address)
    {
        await LogAsync(new AuditLogEntry
        {
            user_id = user_id,
            action = AuditAction.AppLaunch,
            resource_type = "application",
            resource_id = app_id,
            ip_address = ip_address
        });
    }

    public async Task LogFileDeleteAsync(Guid user_id, Guid drive_id, string file_name, string ip_address)
    {
        await LogAsync(new AuditLogEntry
        {
            user_id = user_id,
            action = AuditAction.FileDelete,
            resource_type = "shared_drive",
            resource_id = drive_id,
            detail = JsonSerializer.Serialize(new { file_name }),
            ip_address = ip_address
        });
    }

    public async Task<PagedResult<AuditLogDto>> QueryLogsAsync(AuditLogFilter filter)
    {
        var (items, total) = await _audit_repo.QueryAsync(
            filter.user_id, filter.action,
            filter.start_time, filter.end_time,
            filter.page, filter.page_size);

        return new PagedResult<AuditLogDto>
        {
            items = items.Select(l => new AuditLogDto
            {
                log_id = l.log_id,
                user_id = l.user_id,
                action = l.action,
                resource_type = l.resource_type,
                resource_id = l.resource_id,
                detail = l.detail,
                ip_address = l.ip_address,
                created_at = l.created_at
            }).ToList(),
            total = total,
            page = filter.page,
            page_size = filter.page_size
        };
    }
}