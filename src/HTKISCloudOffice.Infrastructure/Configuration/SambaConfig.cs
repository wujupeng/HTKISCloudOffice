namespace HTKISCloudOffice.Infrastructure.Configuration;

public class SambaConfig
{
    public string config_path { get; init; } = "/etc/samba/smb.conf";
    public string shares_root { get; init; } = "/data/shares";
    public string reload_command { get; init; } = "smbcontrol all reload-config";
}