using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Domain.Enums;
using HTKISCloudOffice.Domain.ValueObjects;

namespace HTKISCloudOffice.Application.Interfaces;

public interface IDesktopService
{
    Task<DesktopConnectionResult> ConnectAsync(string user_id, string? app_id);
    Task DisconnectAsync(string user_id, string connection_id);
    Task<DesktopConnectionResult> ReconnectAsync(string user_id, string connection_id);
    Task<Domain.Entities.VmConfig?> GetUserBoundVmAsync(string user_id);
    Task<ConnectionStatus> GetConnectionStatusAsync(string connection_id);
}

public interface IGuacamoleApiClient
{
    Task<string> AuthenticateAsync();
    Task<GuacamoleConnectionResult> CreateConnectionAsync(GuacamoleConnectionParams param);
    Task<GuacamoleConnectionResult> CreateVncConnectionAsync(VncConnectionParams param);
    Task<GuacamoleConnectionResult> CreateSshConnectionAsync(SshConnectionParams param);
    Task<GuacamoleConnectionResult> CreateRemoteAppConnectionAsync(RemoteAppConnectionParams param);
    Task<GuacamoleConnectionDetail?> GetConnectionAsync(string connection_id);
    Task DeleteConnectionAsync(string connection_id);
    Task<List<GuacamoleConnectionDetail>> GetActiveConnectionsAsync();
}