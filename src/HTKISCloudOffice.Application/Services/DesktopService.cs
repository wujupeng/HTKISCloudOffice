using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Domain.Enums;
using HTKISCloudOffice.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace HTKISCloudOffice.Application.Services;

public class DesktopService : IDesktopService
{
    private readonly IGuacamoleApiClient _guac_client;
    private readonly IVmConfigRepository _vm_config_repo;
    private readonly IUserRepository _user_repo;
    private readonly IAuditService _audit_svc;
    private readonly IAesEncryptionService _encryption_svc;
    private readonly ILogger<DesktopService> _logger;

    public DesktopService(
        IGuacamoleApiClient guac_client,
        IVmConfigRepository vm_config_repo,
        IUserRepository user_repo,
        IAuditService audit_svc,
        IAesEncryptionService encryption_svc,
        ILogger<DesktopService> logger)
    {
        _guac_client = guac_client;
        _vm_config_repo = vm_config_repo;
        _user_repo = user_repo;
        _audit_svc = audit_svc;
        _encryption_svc = encryption_svc;
        _logger = logger;
    }

    public async Task<DesktopConnectionResult> ConnectAsync(string user_id, string? app_id)
    {
        if (!Guid.TryParse(user_id, out var uid))
            return DesktopConnectionResult.Fail("INVALID_USER", "无效的用户ID");

        var vm = await _vm_config_repo.GetByUserIdAsync(uid);
        if (vm == null || !vm.is_active)
            return DesktopConnectionResult.Fail("VM_UNAVAILABLE", "云桌面暂时无法连接，请联系管理员");

        try
        {
            var rdp_password = _encryption_svc.Decrypt(vm.rdp_password_encrypted);

            var conn_params = new GuacamoleConnectionParams
            {
                vm_hostname = vm.hostname,
                rdp_port = vm.rdp_port,
                rdp_username = vm.rdp_username,
                rdp_password_encrypted = rdp_password,
                resolution = "1920x1080",
                color_depth = 32
            };

            var result = await _guac_client.CreateConnectionAsync(conn_params);

            return new DesktopConnectionResult
            {
                success = true,
                connection_id = result.connection_id,
                guacamole_url = result.guacamole_url,
                vm_name = vm.vm_name,
                status = "connecting"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to VM for user {UserId}", user_id);
            return DesktopConnectionResult.Fail("VM_UNAVAILABLE", "云桌面暂时无法连接，请联系管理员");
        }
    }

    public async Task DisconnectAsync(string user_id, string connection_id)
    {
        try
        {
            await _guac_client.DeleteConnectionAsync(connection_id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to disconnect connection {ConnectionId}", connection_id);
        }
    }

    public async Task<DesktopConnectionResult> ReconnectAsync(string user_id, string connection_id)
    {
        try
        {
            var conn = await _guac_client.GetConnectionAsync(connection_id);
            if (conn == null)
                return await ConnectAsync(user_id, null);

            return new DesktopConnectionResult
            {
                success = true,
                connection_id = connection_id,
                guacamole_url = $"/guacamole/#/client/{connection_id}",
                status = "reconnecting"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reconnect for user {UserId}", user_id);
            return DesktopConnectionResult.Fail("VM_UNAVAILABLE", "云桌面暂时无法连接，请联系管理员");
        }
    }

    public async Task<Domain.Entities.VmConfig?> GetUserBoundVmAsync(string user_id)
    {
        if (!Guid.TryParse(user_id, out var uid)) return null;
        return await _vm_config_repo.GetByUserIdAsync(uid);
    }

    public async Task<ConnectionStatus> GetConnectionStatusAsync(string connection_id)
    {
        try
        {
            var conn = await _guac_client.GetConnectionAsync(connection_id);
            if (conn == null) return ConnectionStatus.Disconnected;
            return conn.status?.ToLower() switch
            {
                "connected" => ConnectionStatus.Connected,
                "connecting" => ConnectionStatus.Connecting,
                _ => ConnectionStatus.Disconnected
            };
        }
        catch
        {
            return ConnectionStatus.Error;
        }
    }
}