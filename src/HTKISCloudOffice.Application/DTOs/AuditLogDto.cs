using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Domain.Enums;

namespace HTKISCloudOffice.Application.DTOs;

public class AuditLogDto
{
    public Guid log_id { get; set; }
    public Guid user_id { get; set; }
    public AuditAction action { get; set; }
    public string resource_type { get; set; } = string.Empty;
    public Guid resource_id { get; set; }
    public string? detail { get; set; }
    public string ip_address { get; set; } = string.Empty;
    public DateTime created_at { get; set; }
}

public class PagedResult<T>
{
    public List<T> items { get; set; } = new();
    public int total { get; set; }
    public int page { get; set; }
    public int page_size { get; set; }
}