using HTKISCloudOffice.Domain.ValueObjects;

namespace HTKISCloudOffice.Domain.Entities;

public class Role
{
    public Guid role_id { get; set; } = Guid.NewGuid();
    public string role_name { get; set; } = string.Empty;
    public string? description { get; set; }
    public List<PermissionEntry> permissions { get; set; } = new();
    public DateTime created_at { get; set; } = DateTime.UtcNow;

    public ICollection<UserRole> user_roles { get; set; } = new List<UserRole>();
    public ICollection<AppAllowedRole> app_allowed_roles { get; set; } = new List<AppAllowedRole>();
}