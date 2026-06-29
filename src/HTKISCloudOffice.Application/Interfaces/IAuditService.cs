using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Domain.Enums;

namespace HTKISCloudOffice.Application.Interfaces;

public interface IAuditService
{
    Task LogAsync(AuditLogEntry entry);
    Task LogLoginAsync(Guid user_id, string ip_address, bool success);
    Task LogPermissionChangeAsync(string operator_id, string target_user_id, string detail, string ip_address);
    Task LogAppLaunchAsync(Guid user_id, Guid app_id, string ip_address);
    Task LogFileDeleteAsync(Guid user_id, Guid drive_id, string file_name, string ip_address);
    Task<PagedResult<AuditLogDto>> QueryLogsAsync(AuditLogFilter filter);
}

public class AuditLogEntry
{
    public Guid user_id { get; set; }
    public AuditAction action { get; set; }
    public string resource_type { get; set; } = string.Empty;
    public Guid resource_id { get; set; }
    public string? detail { get; set; }
    public string ip_address { get; set; } = string.Empty;
}

public class AuditLogFilter
{
    public Guid? user_id { get; set; }
    public AuditAction? action { get; set; }
    public DateTime? start_time { get; set; }
    public DateTime? end_time { get; set; }
    public int page { get; set; } = 1;
    public int page_size { get; set; } = 50;
}