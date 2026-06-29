namespace HTKISCloudOffice.Domain.ValueObjects;

public class RemoteAppConnectionParams : GuacamoleConnectionParams
{
    public string remote_app_program { get; set; } = string.Empty;
    public string? remote_app_dir { get; set; }
    public string? remote_app_args { get; set; }
    public bool disable_wallpaper { get; set; } = true;
    public bool disable_full_window_drag { get; set; } = true;
    public bool disable_menu_animations { get; set; } = true;
    public bool disable_theming { get; set; }
}