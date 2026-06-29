namespace HTKISCloudOffice.Application.DTOs;

public class DeviceTokenValidationResult
{
    public bool is_valid { get; init; }
    public Guid user_id { get; init; }
    public string device_id { get; init; } = string.Empty;
    public string error_code { get; init; } = string.Empty;
    public string error_message { get; init; } = string.Empty;

    public static DeviceTokenValidationResult Ok(Guid user_id, string device_id) => new()
    {
        is_valid = true, user_id = user_id, device_id = device_id
    };

    public static DeviceTokenValidationResult Fail(string error_code, string error_message) => new()
    {
        is_valid = false, error_code = error_code, error_message = error_message
    };
}