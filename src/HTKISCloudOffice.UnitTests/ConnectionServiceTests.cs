using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Application.Services;
using HTKISCloudOffice.Domain.Entities;
using HTKISCloudOffice.Domain.Enums;
using HTKISCloudOffice.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;

namespace HTKISCloudOffice.UnitTests;

public class ConnectionServiceTests
{
    private readonly Mock<IConnectionConfigRepository> _config_repo;
    private readonly Mock<IGuacamoleApiClient> _guac_client;
    private readonly Mock<IAesEncryptionService> _encryption_svc;
    private readonly Mock<IAuditService> _audit_svc;
    private readonly Mock<IPermissionService> _permission_svc;
    private readonly Mock<ILogger<ConnectionService>> _logger;
    private readonly ConnectionService _service;

    public ConnectionServiceTests()
    {
        _config_repo = new Mock<IConnectionConfigRepository>();
        _guac_client = new Mock<IGuacamoleApiClient>();
        _encryption_svc = new Mock<IAesEncryptionService>();
        _audit_svc = new Mock<IAuditService>();
        _permission_svc = new Mock<IPermissionService>();
        _logger = new Mock<ILogger<ConnectionService>>();
        _service = new ConnectionService(
            _config_repo.Object,
            _guac_client.Object,
            _encryption_svc.Object,
            _audit_svc.Object,
            _permission_svc.Object,
            _logger.Object);
    }
    private static ConnectionConfig CreateTestConfig(ConnectionProtocol protocol = ConnectionProtocol.RDP, bool is_remote_app = false)
    {
        var role_id = Guid.NewGuid();
        return new ConnectionConfig
        {
            connection_id = Guid.NewGuid(),
            connection_name = "Test Connection",
            protocol = protocol,
            hostname = "192.168.1.100",
            port = protocol == ConnectionProtocol.SSH ? 22 : protocol == ConnectionProtocol.VNC ? 5900 : 3389,
            username = "admin",
            password_encrypted = "encrypted_pwd",
            is_remote_app = is_remote_app,
            is_active = true,
            connection_allowed_roles = new List<ConnectionAllowedRole>
            {
                new() { role_id = role_id }
            },
            connection_params = "{}"
        };
    }

    private void SetupUserWithAccess(ConnectionConfig config)
    {
        var role_id = config.connection_allowed_roles.First().role_id;
        _permission_svc.Setup(p => p.GetUserRolesAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<RoleDto> { new() { role_id = role_id.ToString() } });
    }

    private void SetupUserWithoutAccess()
    {
        _permission_svc.Setup(p => p.GetUserRolesAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<RoleDto> { new() { role_id = Guid.NewGuid().ToString() } });
    }
    [Fact]
    public async Task CreateConnectionAsync_RdpConnection_ReturnsSuccess()
    {
        var config = CreateTestConfig(ConnectionProtocol.RDP);
        _config_repo.Setup(r => r.GetByIdAsync(config.connection_id)).ReturnsAsync(config);
        SetupUserWithAccess(config);
        _encryption_svc.Setup(e => e.Decrypt("encrypted_pwd")).Returns("decrypted_pwd");
        _guac_client.Setup(g => g.CreateConnectionAsync(It.IsAny<GuacamoleConnectionParams>()))
            .ReturnsAsync(new GuacamoleConnectionResult { connection_id = "guac-1", guacamole_url = "/guacamole/#/client/guac-1" });
        _audit_svc.Setup(a => a.LogAsync(It.IsAny<AuditLogEntry>())).Returns(Task.CompletedTask);
        var user_id = Guid.NewGuid().ToString();
        var result = await _service.CreateConnectionAsync(user_id, config.connection_id, "127.0.0.1");
        Assert.True(result.success);
        Assert.Equal("guac-1", result.connection_id);
        Assert.Contains("guac-1", result.guacamole_url);
        _audit_svc.Verify(a => a.LogAsync(It.Is<AuditLogEntry>(e => e.action == AuditAction.ConnectionCreate)), Times.Once);
    }

    [Fact]
    public async Task CreateConnectionAsync_VncConnection_ReturnsSuccess()
    {
        var config = CreateTestConfig(ConnectionProtocol.VNC);
        _config_repo.Setup(r => r.GetByIdAsync(config.connection_id)).ReturnsAsync(config);
        SetupUserWithAccess(config);
        _encryption_svc.Setup(e => e.Decrypt("encrypted_pwd")).Returns("decrypted_pwd");
        _guac_client.Setup(g => g.CreateVncConnectionAsync(It.IsAny<VncConnectionParams>()))
            .ReturnsAsync(new GuacamoleConnectionResult { connection_id = "guac-vnc", guacamole_url = "/guacamole/#/client/guac-vnc" });
        _audit_svc.Setup(a => a.LogAsync(It.IsAny<AuditLogEntry>())).Returns(Task.CompletedTask);
        var user_id = Guid.NewGuid().ToString();
        var result = await _service.CreateConnectionAsync(user_id, config.connection_id, "127.0.0.1");
        Assert.True(result.success);
        Assert.Equal("guac-vnc", result.connection_id);
    }

    [Fact]
    public async Task CreateConnectionAsync_SshConnection_ReturnsSuccess()
    {
        var config = CreateTestConfig(ConnectionProtocol.SSH);
        _config_repo.Setup(r => r.GetByIdAsync(config.connection_id)).ReturnsAsync(config);
        SetupUserWithAccess(config);
        _encryption_svc.Setup(e => e.Decrypt("encrypted_pwd")).Returns("decrypted_pwd");
        _guac_client.Setup(g => g.CreateSshConnectionAsync(It.IsAny<SshConnectionParams>()))
            .ReturnsAsync(new GuacamoleConnectionResult { connection_id = "guac-ssh", guacamole_url = "/guacamole/#/client/guac-ssh" });
        _audit_svc.Setup(a => a.LogAsync(It.IsAny<AuditLogEntry>())).Returns(Task.CompletedTask);
        var user_id = Guid.NewGuid().ToString();
        var result = await _service.CreateConnectionAsync(user_id, config.connection_id, "127.0.0.1");
        Assert.True(result.success);
        Assert.Equal("guac-ssh", result.connection_id);
    }
    [Fact]
    public async Task CreateConnectionAsync_NoPermission_ReturnsAccessDenied()
    {
        var config = CreateTestConfig();
        _config_repo.Setup(r => r.GetByIdAsync(config.connection_id)).ReturnsAsync(config);
        SetupUserWithoutAccess();
        var user_id = Guid.NewGuid().ToString();
        var result = await _service.CreateConnectionAsync(user_id, config.connection_id, "127.0.0.1");
        Assert.False(result.success);
        Assert.Equal("ACCESS_DENIED", result.error_code);
    }

    [Fact]
    public async Task CreateConnectionAsync_ConnectionNotFound_ReturnsConnectionNotFound()
    {
        var connection_id = Guid.NewGuid();
        _config_repo.Setup(r => r.GetByIdAsync(connection_id)).ReturnsAsync((ConnectionConfig?)null);
        var user_id = Guid.NewGuid().ToString();
        var result = await _service.CreateConnectionAsync(user_id, connection_id, "127.0.0.1");
        Assert.False(result.success);
        Assert.Equal("CONNECTION_NOT_FOUND", result.error_code);
    }

    [Fact]
    public async Task GetAvailableConnectionsAsync_FiltersByUserRole()
    {
        var role_id = Guid.NewGuid();
        var config = CreateTestConfig();
        config.connection_allowed_roles = new List<ConnectionAllowedRole> { new() { role_id = role_id } };
        _permission_svc.Setup(p => p.GetUserRolesAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<RoleDto> { new() { role_id = role_id.ToString() } });
        _config_repo.Setup(r => r.GetByUserRolesAsync(It.IsAny<List<Guid>>()))
            .ReturnsAsync(new List<ConnectionConfig> { config });
        var user_id = Guid.NewGuid().ToString();
        var result = await _service.GetAvailableConnectionsAsync(user_id);
        Assert.Single(result);
        Assert.Equal(config.connection_id, result[0].connection_id);
    }
}
