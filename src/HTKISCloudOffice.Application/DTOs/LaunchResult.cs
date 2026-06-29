namespace HTKISCloudOffice.Application.DTOs;

public class LaunchResult
{
    public bool success { get; init; }
    public string connection_id { get; init; } = string.Empty;
    public string guacamole_url { get; init; } = string.Empty;
    public string error_code { get; init; } = string.Empty;
    public string error_message { get; init; } = string.Empty;

    public static LaunchResult Fail(string error_code, string error_message) => new()
    {
        success = false, error_code = error_code, error_message = error_message
    };
}