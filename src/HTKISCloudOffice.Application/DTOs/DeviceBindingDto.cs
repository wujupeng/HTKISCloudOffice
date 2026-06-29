namespace HTKISCloudOffice.Application.DTOs;

public class DeviceBindingDto
{
    public Guid binding_id { get; init; }
    public Guid user_id { get; init; }
    public string device_id { get; init; } = string.Empty;
    public string device_name { get; init; } = string.Empty;
    public DateTime device_token_expires_at { get; init; }
    public DateTime? last_login_at { get; init; }
    public string? last_login_ip { get; init; }
    public bool is_active { get; init; }
    public DateTime created_at { get; init; }
    public string? username { get; init; }
    public string? display_name { get; init; }
}