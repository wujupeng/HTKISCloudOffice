using HTKISCloudOffice.Domain.Enums;

namespace HTKISCloudOffice.Domain.Entities;

public class ConnectionConfig
{
    public Guid connection_id { get; set; } = Guid.NewGuid();
    public string connection_name { get; set; } = string.Empty;
    public ConnectionProtocol protocol { get; set; }
    public string hostname { get; set; } = string.Empty;
    public int port { get; set; }
    public string? username { get; set; }
    public string? password_encrypted { get; set; }
    public string connection_params { get; set; } = "{}";
    public bool is_remote_app { get; set; }
    public string? remote_app_path { get; set; }
    public bool is_active { get; set; } = true;
    public int sort_order { get; set; }
    public DateTime created_at { get; set; } = DateTime.UtcNow;
    public DateTime updated_at { get; set; } = DateTime.UtcNow;

    public ICollection<ConnectionAllowedRole> connection_allowed_roles { get; set; } = new List<ConnectionAllowedRole>();
}

public class ConnectionAllowedRole
{
    public Guid connection_id { get; set; }
    public Guid role_id { get; set; }

    public ConnectionConfig ConnectionConfig { get; set; } = null!;
    public Role Role { get; set; } = null!;
}