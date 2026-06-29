using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Domain.Enums;
using HTKISCloudOffice.Domain.ValueObjects;

namespace HTKISCloudOffice.Application.DTOs;

public class RoleDto
{
    public string role_id { get; set; } = string.Empty;
    public string role_name { get; set; } = string.Empty;
    public string? description { get; set; }
    public List<PermissionEntryDto> permissions { get; set; } = new();
}

public class PermissionEntryDto
{
    public ResourceType resource_type { get; set; }
    public string resource_id { get; set; } = string.Empty;
    public AccessMode access_mode { get; set; }
}

public class CreateRoleRequest
{
    public string role_name { get; set; } = string.Empty;
    public string? description { get; set; }
    public List<PermissionEntryDto> permissions { get; set; } = new();
}