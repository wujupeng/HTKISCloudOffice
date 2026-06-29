namespace HTKISCloudOffice.Domain.Entities;

public class VmConfig
{
    public string vm_id { get; set; } = string.Empty;
    public string vm_name { get; set; } = string.Empty;
    public string hostname { get; set; } = string.Empty;
    public int rdp_port { get; set; } = 3389;
    public string rdp_username { get; set; } = string.Empty;
    public string rdp_password_encrypted { get; set; } = string.Empty;
    public int max_users { get; set; } = 1;
    public bool is_active { get; set; } = true;
    public DateTime created_at { get; set; } = DateTime.UtcNow;
    public DateTime updated_at { get; set; } = DateTime.UtcNow;
}