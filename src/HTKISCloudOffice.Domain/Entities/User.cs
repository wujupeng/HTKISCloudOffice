namespace HTKISCloudOffice.Domain.Entities;

public class User
{
    public Guid user_id { get; set; } = Guid.NewGuid();
    public string username { get; set; } = string.Empty;
    public string password_hash { get; set; } = string.Empty;
    public string display_name { get; set; } = string.Empty;
    public string? department { get; set; }
    public bool is_active { get; set; } = true;
    public string? auto_login_token { get; set; }
    public DateTime? auto_login_token_expires_at { get; set; }
    public string? bound_vm_id { get; set; }
    public DateTime created_at { get; set; } = DateTime.UtcNow;
    public DateTime updated_at { get; set; } = DateTime.UtcNow;

    public VmConfig? BoundVm { get; set; }
    public ICollection<UserRole> user_roles { get; set; } = new List<UserRole>();
    public ICollection<AuditLog> audit_logs { get; set; } = new List<AuditLog>();
    public ICollection<RefreshToken> refresh_tokens { get; set; } = new List<RefreshToken>();
}