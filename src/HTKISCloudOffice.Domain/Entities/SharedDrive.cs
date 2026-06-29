using HTKISCloudOffice.Domain.ValueObjects;
using DriveType = HTKISCloudOffice.Domain.Enums.DriveType;

namespace HTKISCloudOffice.Domain.Entities;

public class SharedDrive
{
    public Guid drive_id { get; set; } = Guid.NewGuid();
    public string drive_name { get; set; } = string.Empty;
    public DriveType drive_type { get; set; }
    public string samba_path { get; set; } = string.Empty;
    public string drive_letter { get; set; } = string.Empty;
    public List<DrivePermission> allowed_permissions { get; set; } = new();
    public Guid? owner_id { get; set; }
    public long quota_mb { get; set; }
    public bool is_active { get; set; } = true;
    public DateTime created_at { get; set; } = DateTime.UtcNow;
    public DateTime updated_at { get; set; } = DateTime.UtcNow;
}