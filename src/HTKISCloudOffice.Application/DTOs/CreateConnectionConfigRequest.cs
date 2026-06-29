using HTKISCloudOffice.Domain.Enums;

namespace HTKISCloudOffice.Application.DTOs;

public class CreateConnectionConfigRequest
{
    public string connection_name { get; set; } = string.Empty;
    public ConnectionProtocol protocol { get; set; }
    public string hostname { get; set; } = string.Empty;
    public int port { get; set; }
    public string? username { get; set; }
    public string? password { get; set; }
    public string? connection_params { get; set; }
    public bool is_remote_app { get; set; }
    public string? remote_app_path { get; set; }
    public List<Guid> allowed_role_ids { get; set; } = new();
}