using HTKISCloudOffice.Domain.Enums;

namespace HTKISCloudOffice.Domain.ValueObjects;

public class DriveMapping
{
    public string drive_letter { get; set; } = string.Empty;
    public string drive_name { get; set; } = string.Empty;
    public string samba_path { get; set; } = string.Empty;
    public AccessMode access_mode { get; set; }
}