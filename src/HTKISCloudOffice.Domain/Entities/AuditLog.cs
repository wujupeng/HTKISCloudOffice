using HTKISCloudOffice.Domain.Enums;

namespace HTKISCloudOffice.Domain.Entities;

public class AuditLog
{
    public Guid log_id { get; set; } = Guid.NewGuid();
    public Guid user_id { get; set; }
    public AuditAction action { get; set; }
    public string resource_type { get; set; } = string.Empty;
    public Guid resource_id { get; set; }
    public string? detail { get; set; }
    public string ip_address { get; set; } = string.Empty;
    public DateTime created_at { get; set; } = DateTime.UtcNow;
}