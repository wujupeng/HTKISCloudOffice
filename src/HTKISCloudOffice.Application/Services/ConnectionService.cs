using System.Text.Json;
using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Domain.Enums;
using HTKISCloudOffice.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace HTKISCloudOffice.Application.Services;

public class ConnectionService : IConnectionService
{
    private readonly IConnectionConfigRepository _config_repo;
    private readonly IGuacamoleApiClient _guac_client;
    private readonly IAesEncryptionService _encryption_svc;
    private readonly IAuditService _audit_svc;
    private readonly IPermissionService _permission_svc;
    private readonly ILogger<ConnectionService> _logger;

    public ConnectionService(
        IConnectionConfigRepository config_repo,
        IGuacamoleApiClient guac_client,
        IAesEncryptionService encryption_svc,
        IAuditService audit_svc,
        IPermissionService permission_svc,
        ILogger<ConnectionService> logger)
    {
        _config_repo = config_repo;
        _guac_client = guac_client;
        _encryption_svc = encryption_svc;
        _audit_svc = audit_svc;
        _permission_svc = permission_svc;
        _logger = logger;
    }

    public async Task<ConnectionConnectResult> CreateConnectionAsync(string user_id, Guid connection_id, string ip_address)
    {
        if (!Guid.TryParse(user_id, out var uid))
            return ConnectionConnectResult.Fail("INVALID_USER", "无效的用户ID");

        var config = await _config_repo.GetByIdAsync(connection_id);
        if (config == null || !config.is_active)
            return ConnectionConnectResult.Fail("CONNECTION_NOT_FOUND", "连接配置不存在或已禁用");

        var user_roles = await _permission_svc.GetUserRolesAsync(user_id);
        var user_role_ids = user_roles.Select(r => Guid.Parse(r.role_id)).ToList();
        var allowed_role_ids = config.connection_allowed_roles.Select(r => r.role_id).ToList();

        if (!user_role_ids.Any(r => allowed_role_ids.Contains(r)))
            return ConnectionConnectResult.Fail("ACCESS_DENIED", "您没有访问此连接的权限");

        try
        {
            var password = !string.IsNullOrEmpty(config.password_encrypted)
                ? _encryption_svc.Decrypt(config.password_encrypted)
                : "";

            GuacamoleConnectionResult guac_result;

            switch (config.protocol)
            {
                case ConnectionProtocol.RDP when config.is_remote_app:
                    guac_result = await TryRemoteAppThenFallback(config, password);
                    break;
                case ConnectionProtocol.RDP:
                    {
                        var rdp_params = BuildRdpParams(config, password);
                        guac_result = await _guac_client.CreateConnectionAsync(rdp_params);
                        break;
                    }
                case ConnectionProtocol.VNC:
                    {
                        var vnc_params = BuildVncParams(config, password);
                        guac_result = await _guac_client.CreateVncConnectionAsync(vnc_params);
                        break;
                    }
                case ConnectionProtocol.SSH:
                    {
                        var ssh_params = BuildSshParams(config, password);
                        guac_result = await _guac_client.CreateSshConnectionAsync(ssh_params);
                        break;
                    }
                default:
                    return ConnectionConnectResult.Fail("UNSUPPORTED_PROTOCOL", "不支持的协议类型");
            }

            await _audit_svc.LogAsync(new AuditLogEntry
            {
                user_id = uid,
                action = AuditAction.ConnectionCreate,
                resource_type = "connection_config",
                resource_id = connection_id,
                detail = $"protocol={config.protocol}",
                ip_address = ip_address
            });

            return new ConnectionConnectResult
            {
                success = true,
                connection_id = guac_result.connection_id,
                guacamole_url = guac_result.guacamole_url,
                protocol = config.protocol,
                status = "connecting"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create connection {ConnectionId} for user {UserId}", connection_id, user_id);
            return ConnectionConnectResult.Fail("CONNECTION_FAILED", "连接创建失败，请稍后重试");
        }
    }

    private async Task<GuacamoleConnectionResult> TryRemoteAppThenFallback(Domain.Entities.ConnectionConfig config, string password)
    {
        try
        {
            var rapp_params = BuildRemoteAppParams(config, password);
            return await _guac_client.CreateRemoteAppConnectionAsync(rapp_params);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RemoteApp failed for connection {ConnectionId}, falling back to full desktop", config.connection_id);
            var rdp_params = BuildRdpParams(config, password);
            return await _guac_client.CreateConnectionAsync(rdp_params);
        }
    }

    public async Task<List<ConnectionConfigDto>> GetAvailableConnectionsAsync(string user_id)
    {
        var user_roles = await _permission_svc.GetUserRolesAsync(user_id);
        var user_role_ids = user_roles.Select(r => Guid.Parse(r.role_id)).ToList();

        var configs = await _config_repo.GetByUserRolesAsync(user_role_ids);
        return configs.Select(MapToDto).ToList();
    }

    public async Task<List<ConnectionConfigDto>> GetAllConnectionConfigsAsync()
    {
        var configs = await _config_repo.GetAllAsync();
        return configs.Select(MapToDto).ToList();
    }

    public async Task<ConnectionConfigDto> CreateConnectionConfigAsync(CreateConnectionConfigRequest request, string operator_id, string ip_address)
    {
        var config = new Domain.Entities.ConnectionConfig
        {
            connection_name = request.connection_name,
            protocol = request.protocol,
            hostname = request.hostname,
            port = request.port,
            username = request.username,
            password_encrypted = !string.IsNullOrEmpty(request.password) ? _encryption_svc.Encrypt(request.password) : null,
            connection_params = request.connection_params ?? "{}",
            is_remote_app = request.is_remote_app,
            remote_app_path = request.remote_app_path,
            sort_order = 0
        };

        var created = await _config_repo.CreateAsync(config, request.allowed_role_ids);

        await _audit_svc.LogAsync(new AuditLogEntry
        {
            user_id = Guid.Parse(operator_id),
            action = AuditAction.ConnectionCreate,
            resource_type = "connection_config",
            resource_id = created.connection_id,
            detail = $"name={request.connection_name},protocol={request.protocol}",
            ip_address = ip_address
        });

        return MapToDto(created);
    }

    public async Task<ConnectionConfigDto> UpdateConnectionConfigAsync(Guid connection_id, UpdateConnectionConfigRequest request, string operator_id, string ip_address)
    {
        var config = await _config_repo.GetByIdAsync(connection_id)
            ?? throw new InvalidOperationException("Connection config not found");

        if (request.connection_name != null) config.connection_name = request.connection_name;
        if (request.protocol != null) config.protocol = request.protocol.Value;
        if (request.hostname != null) config.hostname = request.hostname;
        if (request.port != null) config.port = request.port.Value;
        if (request.username != null) config.username = request.username;
        if (request.password != null) config.password_encrypted = _encryption_svc.Encrypt(request.password);
        if (request.connection_params != null) config.connection_params = request.connection_params;
        if (request.is_remote_app != null) config.is_remote_app = request.is_remote_app.Value;
        if (request.remote_app_path != null) config.remote_app_path = request.remote_app_path;
        if (request.is_active != null) config.is_active = request.is_active.Value;
        if (request.sort_order != null) config.sort_order = request.sort_order.Value;

        var updated = await _config_repo.UpdateAsync(config, request.allowed_role_ids);

        await _audit_svc.LogAsync(new AuditLogEntry
        {
            user_id = Guid.Parse(operator_id),
            action = AuditAction.ConnectionCreate,
            resource_type = "connection_config",
            resource_id = connection_id,
            detail = "updated",
            ip_address = ip_address
        });

        return MapToDto(updated);
    }

    public async Task DeleteConnectionConfigAsync(Guid connection_id, string operator_id, string ip_address)
    {
        await _config_repo.DeleteAsync(connection_id);

        await _audit_svc.LogAsync(new AuditLogEntry
        {
            user_id = Guid.Parse(operator_id),
            action = AuditAction.ConnectionDelete,
            resource_type = "connection_config",
            resource_id = connection_id,
            detail = "soft_deleted",
            ip_address = ip_address
        });
    }

    public async Task DisconnectAndKeepSessionAsync(string user_id, string guacamole_connection_id, Guid connection_config_id, string ip_address)
    {
        try
        {
            await _guac_client.DeleteConnectionAsync(guacamole_connection_id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to disconnect Guacamole connection {ConnectionId}", guacamole_connection_id);
        }
    }

    public async Task<ConnectionConnectResult> ReconnectSessionAsync(string user_id, string guacamole_connection_id, Guid connection_config_id, string ip_address)
    {
        try
        {
            var conn = await _guac_client.GetConnectionAsync(guacamole_connection_id);
            if (conn != null)
            {
                var config = await _config_repo.GetByIdAsync(connection_config_id);
                return new ConnectionConnectResult
                {
                    success = true,
                    connection_id = guacamole_connection_id,
                    guacamole_url = $"/guacamole/#/client/{guacamole_connection_id}",
                    protocol = config?.protocol ?? ConnectionProtocol.RDP,
                    status = "reconnecting"
                };
            }

            return await CreateConnectionAsync(user_id, connection_config_id, ip_address);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reconnect for user {UserId}", user_id);
            return ConnectionConnectResult.Fail("RECONNECT_FAILED", "重连失败，之前的会话可能已丢失");
        }
    }

    private GuacamoleConnectionParams BuildRdpParams(Domain.Entities.ConnectionConfig config, string password)
    {
        return new GuacamoleConnectionParams
        {
            vm_hostname = config.hostname,
            rdp_port = config.port,
            rdp_username = config.username ?? "",
            rdp_password_encrypted = password,
            resolution = "1920x1080",
            color_depth = 32
        };
    }

    private VncConnectionParams BuildVncParams(Domain.Entities.ConnectionConfig config, string password)
    {
        var extra = ParseConnectionParams(config.connection_params);
        return new VncConnectionParams
        {
            hostname = config.hostname,
            vnc_port = config.port,
            vnc_password_encrypted = password,
            color_depth = extra.TryGetValue("color_depth", out var cd) ? int.Parse(cd.ToString()!) : 24,
            swap_red_blue = extra.TryGetValue("swap_red_blue", out var srb) && bool.Parse(srb.ToString()!),
            cursor = extra.TryGetValue("cursor", out var cur) ? cur.ToString()! : "default"
        };
    }

    private SshConnectionParams BuildSshParams(Domain.Entities.ConnectionConfig config, string password)
    {
        var extra = ParseConnectionParams(config.connection_params);
        return new SshConnectionParams
        {
            hostname = config.hostname,
            ssh_port = config.port,
            ssh_username = config.username ?? "",
            ssh_password_encrypted = password,
            private_key = extra.TryGetValue("private_key", out var pk) ? pk.ToString() : null,
            font_name = extra.TryGetValue("font_name", out var fn) ? fn.ToString()! : "monospace",
            font_size = extra.TryGetValue("font_size", out var fs) ? int.Parse(fs.ToString()!) : 12
        };
    }

    private RemoteAppConnectionParams BuildRemoteAppParams(Domain.Entities.ConnectionConfig config, string password)
    {
        var extra = ParseConnectionParams(config.connection_params);
        return new RemoteAppConnectionParams
        {
            vm_hostname = config.hostname,
            rdp_port = config.port,
            rdp_username = config.username ?? "",
            rdp_password_encrypted = password,
            resolution = "1920x1080",
            color_depth = 32,
            remote_app_program = config.remote_app_path ?? "",
            remote_app_dir = extra.TryGetValue("remote_app_dir", out var rad) ? rad.ToString() : null,
            remote_app_args = extra.TryGetValue("remote_app_args", out var raa) ? raa.ToString() : null,
            disable_wallpaper = !extra.TryGetValue("disable_wallpaper", out var dw) || bool.Parse(dw.ToString()!),
            disable_full_window_drag = !extra.TryGetValue("disable_full_window_drag", out var dfwd) || bool.Parse(dfwd.ToString()!),
            disable_menu_animations = !extra.TryGetValue("disable_menu_animations", out var dma) || bool.Parse(dma.ToString()!),
            disable_theming = extra.TryGetValue("disable_theming", out var dt) && bool.Parse(dt.ToString()!)
        };
    }

    private static Dictionary<string, object> ParseConnectionParams(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new();
        }
        catch
        {
            return new();
        }
    }

    private static ConnectionConfigDto MapToDto(Domain.Entities.ConnectionConfig config)
    {
        return new ConnectionConfigDto
        {
            connection_id = config.connection_id,
            connection_name = config.connection_name,
            protocol = config.protocol,
            hostname = config.hostname,
            port = config.port,
            username = config.username,
            is_remote_app = config.is_remote_app,
            remote_app_path = config.remote_app_path,
            is_active = config.is_active,
            sort_order = config.sort_order,
            allowed_role_ids = config.connection_allowed_roles.Select(r => r.role_id).ToList(),
            connection_params = config.connection_params
        };
    }
}