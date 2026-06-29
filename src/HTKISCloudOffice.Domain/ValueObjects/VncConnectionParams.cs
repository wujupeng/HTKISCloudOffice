namespace HTKISCloudOffice.Domain.ValueObjects;

public class VncConnectionParams
{
    public string hostname { get; set; } = string.Empty;
    public int vnc_port { get; set; } = 5900;
    public string vnc_password_encrypted { get; set; } = string.Empty;
    public int color_depth { get; set; } = 24;
    public bool swap_red_blue { get; set; }
    public string cursor { get; set; } = "default";
}