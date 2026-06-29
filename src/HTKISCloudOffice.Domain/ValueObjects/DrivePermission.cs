using HTKISCloudOffice.Domain.Enums;

namespace HTKISCloudOffice.Domain.ValueObjects;

public class DrivePermission
{
    public string role_id { get; set; } = string.Empty;
    public AccessMode access_mode { get; set; }
}