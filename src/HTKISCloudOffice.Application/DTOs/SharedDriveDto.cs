using DriveType = HTKISCloudOffice.Domain.Enums.DriveType;
using HTKISCloudOffice.Domain.Enums;

namespace HTKISCloudOffice.Application.DTOs;

public class SharedDriveDto
{
    public string drive_id { get; set; } = string.Empty;
    public string drive_name { get; set; } = string.Empty;
    public DriveType drive_type { get; set; }
    public string drive_letter { get; set; } = string.Empty;
    public string samba_path { get; set; } = string.Empty;
    public AccessMode access_mode { get; set; }
}

public class DriveMappingResult
{
    public List<DriveMappingDto> mappings { get; set; } = new();
}

public class DriveMappingDto
{
    public string drive_letter { get; set; } = string.Empty;
    public string drive_name { get; set; } = string.Empty;
    public string samba_path { get; set; } = string.Empty;
    public AccessMode access_mode { get; set; }
}

public class CreateSharedDriveRequest
{
    public string drive_name { get; set; } = string.Empty;
    public DriveType drive_type { get; set; }
    public string samba_path { get; set; } = string.Empty;
    public string drive_letter { get; set; } = string.Empty;
    public List<DrivePermissionDto> permissions { get; set; } = new();
    public long quota_mb { get; set; }
}

public class DrivePermissionDto
{
    public string role_id { get; set; } = string.Empty;
    public AccessMode access_mode { get; set; }
}

public class QuotaInfo
{
    public string drive_id { get; set; } = string.Empty;
    public long quota_mb { get; set; }
    public long used_mb { get; set; }
    public long available_mb { get; set; }
    public bool unlimited => quota_mb == 0;
}