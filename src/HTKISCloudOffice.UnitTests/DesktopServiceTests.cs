using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Application.Services;
using HTKISCloudOffice.Domain.Entities;
using HTKISCloudOffice.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;

namespace HTKISCloudOffice.UnitTests;

public class DesktopServiceTests
{
    private readonly Mock<IGuacamoleApiClient> _guac_client;
    private readonly Mock<IVmConfigRepository> _vm_config_repo;
    private readonly Mock<IUserRepository> _user_repo;
    private readonly Mock<IAuditService> _audit_svc;
    private readonly Mock<IAesEncryptionService> _encryption_svc;
    private readonly Mock<ILogger<DesktopService>> _logger;
    private readonly DesktopService _service;

    public DesktopServiceTests()
    {
        _guac_client = new Mock<IGuacamoleApiClient>();
        _vm_config_repo = new Mock<IVmConfigRepository>();
        _user_repo = new Mock<IUserRepository>();
        _audit_svc = new Mock<IAuditService>();
        _encryption_svc = new Mock<IAesEncryptionService>();
        _logger = new Mock<ILogger<DesktopService>>();
        _service = new DesktopService(_guac_client.Object, _vm_config_repo.Object,
            _user_repo.Object, _audit_svc.Object, _encryption_svc.Object, _logger.Object);
    }

    private static VmConfig CreateTestVm()
    {
        return new VmConfig
        {
            vm_id = "vm_001",
            vm_name = "VM-Desktop-01",
            hostname = "192.168.1.100",
            rdp_port = 3389,
            rdp_username = "admin",
            rdp_password_encrypted = "encrypted_pwd",
            is_active = true
        };
    }

    [Fact]
    public async Task ConnectAsync_WithValidUser_ReturnsConnectionResult()
    {
        var user_id = Guid.NewGuid();
        var vm = CreateTestVm();

        _vm_config_repo.Setup(r => r.GetByUserIdAsync(user_id)).ReturnsAsync(vm);
        _encryption_svc.Setup(s => s.Decrypt("encrypted_pwd")).Returns("decrypted_pwd");
        _guac_client.Setup(c => c.CreateConnectionAsync(It.IsAny<Domain.ValueObjects.GuacamoleConnectionParams>()))
            .ReturnsAsync(new GuacamoleConnectionResult
            {
                connection_id = "conn_123",
                guacamole_url = "/guacamole/#/client/conn_123"
            });

        var result = await _service.ConnectAsync(user_id.ToString(), null);

        Assert.True(result.success);
        Assert.Equal("conn_123", result.connection_id);
        Assert.Equal("/guacamole/#/client/conn_123", result.guacamole_url);
    }

    [Fact]
    public async Task ConnectAsync_WithInvalidUserId_ReturnsInvalidUser()
    {
        var result = await _service.ConnectAsync("not-a-guid", null);

        Assert.False(result.success);
        Assert.Equal("INVALID_USER", result.error_code);
    }

    [Fact]
    public async Task ConnectAsync_WithNoVm_ReturnsVmUnavailable()
    {
        var user_id = Guid.NewGuid();
        _vm_config_repo.Setup(r => r.GetByUserIdAsync(user_id)).ReturnsAsync((VmConfig?)null);

        var result = await _service.ConnectAsync(user_id.ToString(), null);

        Assert.False(result.success);
        Assert.Equal("VM_UNAVAILABLE", result.error_code);
    }

    [Fact]
    public async Task ConnectAsync_WithInactiveVm_ReturnsVmUnavailable()
    {
        var user_id = Guid.NewGuid();
        var vm = CreateTestVm();
        vm.is_active = false;
        _vm_config_repo.Setup(r => r.GetByUserIdAsync(user_id)).ReturnsAsync(vm);

        var result = await _service.ConnectAsync(user_id.ToString(), null);

        Assert.False(result.success);
        Assert.Equal("VM_UNAVAILABLE", result.error_code);
    }

    [Fact]
    public async Task ConnectAsync_WhenGuacamoleFails_ReturnsVmUnavailable()
    {
        var user_id = Guid.NewGuid();
        var vm = CreateTestVm();

        _vm_config_repo.Setup(r => r.GetByUserIdAsync(user_id)).ReturnsAsync(vm);
        _encryption_svc.Setup(s => s.Decrypt("encrypted_pwd")).Returns("decrypted_pwd");
        _guac_client.Setup(c => c.CreateConnectionAsync(It.IsAny<Domain.ValueObjects.GuacamoleConnectionParams>()))
            .ThrowsAsync(new Exception("Guacamole unavailable"));

        var result = await _service.ConnectAsync(user_id.ToString(), null);

        Assert.False(result.success);
        Assert.Equal("VM_UNAVAILABLE", result.error_code);
    }

    [Fact]
    public async Task DisconnectAsync_CallsDeleteConnection()
    {
        await _service.DisconnectAsync(Guid.NewGuid().ToString(), "conn_123");

        _guac_client.Verify(c => c.DeleteConnectionAsync("conn_123"), Times.Once);
    }

    [Fact]
    public async Task DisconnectAsync_WhenDeleteFails_DoesNotThrow()
    {
        _guac_client.Setup(c => c.DeleteConnectionAsync("conn_123"))
            .ThrowsAsync(new Exception("Connection not found"));

        var exception = await Record.ExceptionAsync(() =>
            _service.DisconnectAsync(Guid.NewGuid().ToString(), "conn_123"));

        Assert.Null(exception);
    }

    [Fact]
    public async Task ReconnectAsync_WithExistingConnection_ReturnsReconnecting()
    {
        var user_id = Guid.NewGuid();
        _guac_client.Setup(c => c.GetConnectionAsync("conn_123"))
            .ReturnsAsync(new GuacamoleConnectionDetail
            {
                connection_id = "conn_123",
                status = "connected"
            });

        var result = await _service.ReconnectAsync(user_id.ToString(), "conn_123");

        Assert.True(result.success);
        Assert.Equal("reconnecting", result.status);
    }

    [Fact]
    public async Task ReconnectAsync_WithNoExistingConnection_CreatesNew()
    {
        var user_id = Guid.NewGuid();
        var vm = CreateTestVm();

        _guac_client.Setup(c => c.GetConnectionAsync("conn_123"))
            .ReturnsAsync((GuacamoleConnectionDetail?)null);
        _vm_config_repo.Setup(r => r.GetByUserIdAsync(user_id)).ReturnsAsync(vm);
        _encryption_svc.Setup(s => s.Decrypt("encrypted_pwd")).Returns("pwd");
        _guac_client.Setup(c => c.CreateConnectionAsync(It.IsAny<Domain.ValueObjects.GuacamoleConnectionParams>()))
            .ReturnsAsync(new GuacamoleConnectionResult
            {
                connection_id = "conn_new",
                guacamole_url = "/guacamole/#/client/conn_new"
            });

        var result = await _service.ReconnectAsync(user_id.ToString(), "conn_123");

        Assert.True(result.success);
        Assert.Equal("conn_new", result.connection_id);
    }

    [Fact]
    public async Task GetUserBoundVmAsync_ReturnsVm()
    {
        var user_id = Guid.NewGuid();
        var vm = CreateTestVm();
        _vm_config_repo.Setup(r => r.GetByUserIdAsync(user_id)).ReturnsAsync(vm);

        var result = await _service.GetUserBoundVmAsync(user_id.ToString());

        Assert.NotNull(result);
        Assert.Equal("vm_001", result.vm_id);
    }

    [Fact]
    public async Task GetConnectionStatusAsync_Connected_ReturnsConnected()
    {
        _guac_client.Setup(c => c.GetConnectionAsync("conn_123"))
            .ReturnsAsync(new GuacamoleConnectionDetail { status = "connected" });

        var result = await _service.GetConnectionStatusAsync("conn_123");

        Assert.Equal(ConnectionStatus.Connected, result);
    }

    [Fact]
    public async Task GetConnectionStatusAsync_NotFound_ReturnsDisconnected()
    {
        _guac_client.Setup(c => c.GetConnectionAsync("conn_123"))
            .ReturnsAsync((GuacamoleConnectionDetail?)null);

        var result = await _service.GetConnectionStatusAsync("conn_123");

        Assert.Equal(ConnectionStatus.Disconnected, result);
    }

    [Fact]
    public async Task GetConnectionStatusAsync_OnError_ReturnsError()
    {
        _guac_client.Setup(c => c.GetConnectionAsync("conn_123"))
            .ThrowsAsync(new Exception("Network error"));

        var result = await _service.GetConnectionStatusAsync("conn_123");

        Assert.Equal(ConnectionStatus.Error, result);
    }
}