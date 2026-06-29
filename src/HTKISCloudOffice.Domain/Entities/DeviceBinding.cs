namespace HTKISCloudOffice.Domain.Entities;

public class DeviceBinding
{
    public Guid binding_id { get; set; } = Guid.NewGuid();
    public Guid user_id { get; set; }
    public string device_id { get; set; } = string.Empty;
    public string device_name { get; set; } = string.Empty;
    public string device_token { get; set; } = string.Empty;
    public DateTime device_token_expires_at { get; set; }
    public DateTime? last_login_at { get; set; }
    public string? last_login_ip { get; set; }
    public bool is_active { get; set; } = true;
    public DateTime created_at { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
}