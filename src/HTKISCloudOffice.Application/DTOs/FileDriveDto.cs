using DriveType = HTKISCloudOffice.Domain.Enums.DriveType;
using HTKISCloudOffice.Domain.Enums;

namespace HTKISCloudOffice.Application.DTOs;

public class FileDriveDto
{
    public string drive_id { get; set; } = string.Empty;
    public string drive_name { get; set; } = string.Empty;
    public DriveType drive_type { get; set; }
    public string samba_path { get; set; } = string.Empty;
    public AccessMode access_mode { get; set; }
    public long quota_mb { get; set; }
    public long used_mb { get; set; }
    public long available_mb { get; set; }
}