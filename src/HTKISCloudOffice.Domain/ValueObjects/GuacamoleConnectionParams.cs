using HTKISCloudOffice.Domain.Enums;

namespace HTKISCloudOffice.Domain.ValueObjects;

public class GuacamoleConnectionParams
{
    public string vm_hostname { get; set; } = string.Empty;
    public int rdp_port { get; set; } = 3389;
    public string rdp_username { get; set; } = string.Empty;
    public string rdp_password_encrypted { get; set; } = string.Empty;
    public string resolution { get; set; } = "1920x1080";
    public int color_depth { get; set; } = 32;
    public bool app_direct_launch { get; set; }
    public string app_program_path { get; set; } = string.Empty;
}