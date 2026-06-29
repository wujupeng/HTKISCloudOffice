using HTKISCloudOffice.Application.DTOs;

namespace HTKISCloudOffice.Application.Interfaces;

public interface IConnectionService
{
    Task<ConnectionConnectResult> CreateConnectionAsync(string user_id, Guid connection_id, string ip_address);
    Task<List<ConnectionConfigDto>> GetAvailableConnectionsAsync(string user_id);
    Task<List<ConnectionConfigDto>> GetAllConnectionConfigsAsync();
    Task<ConnectionConfigDto> CreateConnectionConfigAsync(CreateConnectionConfigRequest request, string operator_id, string ip_address);
    Task<ConnectionConfigDto> UpdateConnectionConfigAsync(Guid connection_id, UpdateConnectionConfigRequest request, string operator_id, string ip_address);
    Task DeleteConnectionConfigAsync(Guid connection_id, string operator_id, string ip_address);
    Task DisconnectAndKeepSessionAsync(string user_id, string guacamole_connection_id, Guid connection_config_id, string ip_address);
    Task<ConnectionConnectResult> ReconnectSessionAsync(string user_id, string guacamole_connection_id, Guid connection_config_id, string ip_address);
}