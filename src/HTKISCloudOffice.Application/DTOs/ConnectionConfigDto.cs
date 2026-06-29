using HTKISCloudOffice.Domain.Enums;

namespace HTKISCloudOffice.Application.DTOs;

public class ConnectionConfigDto
{
    public Guid connection_id { get; init; }
    public string connection_name { get; init; } = string.Empty;
    public ConnectionProtocol protocol { get; init; }
    public string hostname { get; init; } = string.Empty;
    public int port { get; init; }
    public string? username { get; init; }
    public bool is_remote_app { get; init; }
    public string? remote_app_path { get; init; }
    public bool is_active { get; init; }
    public int sort_order { get; init; }
    public List<Guid> allowed_role_ids { get; init; } = new();
    public string? connection_params { get; init; }
}

public class ConnectionConnectResult
{
    public bool success { get; init; }
    public string connection_id { get; init; } = string.Empty;
    public string guacamole_url { get; init; } = string.Empty;
    public ConnectionProtocol protocol { get; init; }
    public string status { get; init; } = "connecting";
    public string? error_code { get; init; }
    public string? error_message { get; init; }
    public bool fell_back_to_desktop { get; init; }

    public static ConnectionConnectResult Fail(string error_code, string error_message) => new()
    {
        success = false, error_code = error_code, error_message = error_message
    };
}