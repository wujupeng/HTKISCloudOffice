namespace HTKISCloudOffice.Domain.ValueObjects;

public class SshConnectionParams
{
    public string hostname { get; set; } = string.Empty;
    public int ssh_port { get; set; } = 22;
    public string ssh_username { get; set; } = string.Empty;
    public string ssh_password_encrypted { get; set; } = string.Empty;
    public string? private_key { get; set; }
    public string font_name { get; set; } = "monospace";
    public int font_size { get; set; } = 12;
}