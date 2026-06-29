namespace HTKISCloudOffice.Application.DTOs;

public class DesktopConnectionResult
{
    public bool success { get; init; }
    public string connection_id { get; init; } = string.Empty;
    public string guacamole_url { get; init; } = string.Empty;
    public string vm_name { get; init; } = string.Empty;
    public string status { get; init; } = "connecting";
    public string error_code { get; init; } = string.Empty;
    public string error_message { get; init; } = string.Empty;

    public static DesktopConnectionResult Fail(string error_code, string error_message) => new()
    {
        success = false, error_code = error_code, error_message = error_message
    };
}

public class GuacamoleConnectionResult
{
    public string connection_id { get; set; } = string.Empty;
    public string connection_token { get; set; } = string.Empty;
    public string guacamole_url { get; set; } = string.Empty;
}

public class GuacamoleConnectionDetail
{
    public string connection_id { get; set; } = string.Empty;
    public string name { get; set; } = string.Empty;
    public string status { get; set; } = string.Empty;
}