using HTKISCloudOffice.Domain.Enums;

namespace HTKISCloudOffice.Domain.ValueObjects;

public class PermissionEntry
{
    public ResourceType resource_type { get; set; }
    public string resource_id { get; set; } = string.Empty;
    public AccessMode access_mode { get; set; }
}