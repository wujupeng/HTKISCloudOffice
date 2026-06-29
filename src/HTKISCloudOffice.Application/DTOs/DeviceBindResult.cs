namespace HTKISCloudOffice.Application.DTOs;

public class DeviceBindResult
{
    public bool success { get; init; }
    public Guid binding_id { get; init; }
    public string device_token { get; init; } = string.Empty;
    public DateTime device_token_expires_at { get; init; }
    public string error_code { get; init; } = string.Empty;
    public string error_message { get; init; } = string.Empty;

    public static DeviceBindResult Ok(Guid binding_id, string device_token, DateTime expires_at) => new()
    {
        success = true, binding_id = binding_id, device_token = device_token,
        device_token_expires_at = expires_at
    };

    public static DeviceBindResult Fail(string error_code, string error_message) => new()
    {
        success = false, error_code = error_code, error_message = error_message
    };
}