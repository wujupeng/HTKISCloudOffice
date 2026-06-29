using System.Security.Claims;
using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Application.Services;
using HTKISCloudOffice.Domain.Entities;
using HTKISCloudOffice.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace HTKISCloudOffice.UnitTests;

public class DeviceAuthServiceTests
{
    private readonly Mock<IDeviceBindingRepository> _binding_repo;
    private readonly Mock<IUserRepository> _user_repo;
    private readonly Mock<IJwtTokenProvider> _jwt_provider;
    private readonly Mock<IAuditService> _audit_svc;
    private readonly Mock<ILogger<DeviceAuthService>> _logger;
    private readonly DeviceAuthService _service;

    public DeviceAuthServiceTests()
    {
        _binding_repo = new Mock<IDeviceBindingRepository>();
        _user_repo = new Mock<IUserRepository>();
        _jwt_provider = new Mock<IJwtTokenProvider>();
        _audit_svc = new Mock<IAuditService>();
        _logger = new Mock<ILogger<DeviceAuthService>>();
        _service = new DeviceAuthService(
            _binding_repo.Object,
            _user_repo.Object,
            _jwt_provider.Object,
            _audit_svc.Object,
            _logger.Object);
    }

    private static User CreateTestUser(bool active = true)
    {
        var role_id = Guid.NewGuid();
        return new User
        {
            user_id = Guid.NewGuid(),
            username = "testuser",
            display_name = "Test User",
            department = "IT",
            is_active = active,
            password_hash = "hash",
            user_roles = new List<UserRole>
            {
                new()
                {
                    role_id = role_id,
                    Role = new Role { role_id = role_id, role_name = "all_staff" }
                }
            }
        };
    }

    private static DeviceBinding CreateTestBinding(Guid user_id, string device_id, bool active = true, DateTime? expires_at = null)
    {
        return new DeviceBinding
        {
            binding_id = Guid.NewGuid(),
            user_id = user_id,
            device_id = device_id,
            device_name = "Test Device",
            device_token = "device_token_value",
            device_token_expires_at = expires_at ?? DateTime.UtcNow.AddDays(30),
            is_active = active,
            last_login_at = DateTime.UtcNow,
            last_login_ip = "127.0.0.1"
        };
    }

    private static ClaimsPrincipal CreateDeviceTokenClaims(Guid user_id, string device_id)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", user_id.ToString()),
            new Claim("device_id", device_id),
            new Claim("token_type", "device_binding")
        }));
    }

    [Fact]
    public async Task AutoLoginWithDeviceAsync_ValidTokenAndMatchingDevice_ReturnsSuccess()
    {
        var user = CreateTestUser();
        var device_id = "device-001";
        var claims = CreateDeviceTokenClaims(user.user_id, device_id);
        var binding = CreateTestBinding(user.user_id, device_id);

        _jwt_provider.Setup(p => p.ValidateToken("valid_token")).Returns(claims);
        _binding_repo.Setup(r => r.GetByDeviceIdAsync(user.user_id, device_id)).ReturnsAsync(binding);
        _user_repo.Setup(r => r.GetByIdAsync(user.user_id)).ReturnsAsync(user);
        _jwt_provider.Setup(p => p.GenerateToken(user.user_id, user.username, It.IsAny<List<string>>(), It.IsAny<int?>()))
            .Returns("jwt_token");
        _jwt_provider.Setup(p => p.GetTokenExpiration("jwt_token"))
            .Returns(DateTime.UtcNow.AddHours(8));

        var result = await _service.AutoLoginWithDeviceAsync("valid_token", device_id, "127.0.0.1");

        Assert.True(result.success);
        Assert.Equal("jwt_token", result.token);
        Assert.NotNull(result.user);
        Assert.Equal(user.user_id, result.user.user_id);
        Assert.Contains("all_staff", result.user.roles);
        _binding_repo.Verify(r => r.UpdateLastLoginAsync(binding.binding_id, It.IsAny<DateTime>(), "127.0.0.1"), Times.Once);
        _audit_svc.Verify(a => a.LogLoginAsync(user.user_id, "127.0.0.1", true), Times.Once);
    }

    [Fact]
    public async Task AutoLoginWithDeviceAsync_TokenSignatureValidationFails_ReturnsDeviceTokenInvalid()
    {
        _jwt_provider.Setup(p => p.ValidateToken("bad_token"))
            .Throws(new SecurityTokenException("Invalid signature"));

        var result = await _service.AutoLoginWithDeviceAsync("bad_token", "device-001", "127.0.0.1");

        Assert.False(result.success);
        Assert.Equal("DEVICE_TOKEN_INVALID", result.error_code);
        _audit_svc.Verify(a => a.LogAsync(It.Is<AuditLogEntry>(e =>
            e.action == AuditAction.Login && e.detail!.Contains("signature validation failed"))), Times.Once);
    }

    [Fact]
    public async Task AutoLoginWithDeviceAsync_DeviceIdMismatch_ReturnsDeviceMismatch()
    {
        var user = CreateTestUser();
        var claims = CreateDeviceTokenClaims(user.user_id, "device-001");

        _jwt_provider.Setup(p => p.ValidateToken("valid_token")).Returns(claims);

        var result = await _service.AutoLoginWithDeviceAsync("valid_token", "device-002", "127.0.0.1");

        Assert.False(result.success);
        Assert.Equal("DEVICE_MISMATCH", result.error_code);
    }

    [Fact]
    public async Task AutoLoginWithDeviceAsync_TokenExpired_ReturnsDeviceTokenExpired()
    {
        var user = CreateTestUser();
        var device_id = "device-001";
        var claims = CreateDeviceTokenClaims(user.user_id, device_id);
        var binding = CreateTestBinding(user.user_id, device_id, expires_at: DateTime.UtcNow.AddDays(-1));

        _jwt_provider.Setup(p => p.ValidateToken("expired_token")).Returns(claims);
        _binding_repo.Setup(r => r.GetByDeviceIdAsync(user.user_id, device_id)).ReturnsAsync(binding);

        var result = await _service.AutoLoginWithDeviceAsync("expired_token", device_id, "127.0.0.1");

        Assert.False(result.success);
        Assert.Equal("DEVICE_TOKEN_EXPIRED", result.error_code);
    }

    [Fact]
    public async Task AutoLoginWithDeviceAsync_BindingInactive_ReturnsDeviceTokenInvalid()
    {
        var user = CreateTestUser();
        var device_id = "device-001";
        var claims = CreateDeviceTokenClaims(user.user_id, device_id);
        var binding = CreateTestBinding(user.user_id, device_id, active: false);

        _jwt_provider.Setup(p => p.ValidateToken("valid_token")).Returns(claims);
        _binding_repo.Setup(r => r.GetByDeviceIdAsync(user.user_id, device_id)).ReturnsAsync(binding);

        var result = await _service.AutoLoginWithDeviceAsync("valid_token", device_id, "127.0.0.1");

        Assert.False(result.success);
        Assert.Equal("DEVICE_TOKEN_INVALID", result.error_code);
    }

    [Fact]
    public async Task BindDeviceAsync_NormalBinding_ReturnsSuccess()
    {
        var user_id = Guid.NewGuid();
        var device_id = "device-new";

        _binding_repo.Setup(r => r.CountActiveBindingsByUserIdAsync(user_id)).ReturnsAsync(0);
        _binding_repo.Setup(r => r.GetByDeviceIdAsync(user_id, device_id)).ReturnsAsync((DeviceBinding?)null);
        _jwt_provider.Setup(p => p.GenerateAutoLoginToken(user_id, device_id)).Returns("auto_login_token");
        _binding_repo.Setup(r => r.CreateAsync(It.IsAny<DeviceBinding>()))
            .ReturnsAsync((DeviceBinding b) => b);

        var result = await _service.BindDeviceAsync(user_id, device_id, "New Device", "127.0.0.1");

        Assert.True(result.success);
        Assert.Equal("auto_login_token", result.device_token);
        Assert.True(result.device_token_expires_at > DateTime.UtcNow);
        _binding_repo.Verify(r => r.CreateAsync(It.IsAny<DeviceBinding>()), Times.Once);
        _audit_svc.Verify(a => a.LogAsync(It.Is<AuditLogEntry>(e =>
            e.action == AuditAction.DeviceBind && e.user_id == user_id)), Times.Once);
    }

    [Fact]
    public async Task BindDeviceAsync_ExceedsMaxDevices_ReturnsDeviceBindLimit()
    {
        var user_id = Guid.NewGuid();

        _binding_repo.Setup(r => r.CountActiveBindingsByUserIdAsync(user_id)).ReturnsAsync(5);

        var result = await _service.BindDeviceAsync(user_id, "device-006", "Device 6", "127.0.0.1");

        Assert.False(result.success);
        Assert.Equal("DEVICE_BIND_LIMIT", result.error_code);
    }

    [Fact]
    public async Task BindDeviceAsync_AlreadyBound_ReturnsDeviceAlreadyBound()
    {
        var user_id = Guid.NewGuid();
        var device_id = "device-001";
        var existing_binding = CreateTestBinding(user_id, device_id, active: true);

        _binding_repo.Setup(r => r.CountActiveBindingsByUserIdAsync(user_id)).ReturnsAsync(1);
        _binding_repo.Setup(r => r.GetByDeviceIdAsync(user_id, device_id)).ReturnsAsync(existing_binding);

        var result = await _service.BindDeviceAsync(user_id, device_id, "Test Device", "127.0.0.1");

        Assert.False(result.success);
        Assert.Equal("DEVICE_ALREADY_BOUND", result.error_code);
    }

    [Fact]
    public async Task UnbindDeviceAsync_NormalUnbind_CallsDeactivateAndLog()
    {
        var binding_id = Guid.NewGuid();
        var user_id = Guid.NewGuid();

        _binding_repo.Setup(r => r.DeactivateAsync(binding_id)).Returns(Task.CompletedTask);
        _audit_svc.Setup(a => a.LogAsync(It.IsAny<AuditLogEntry>())).Returns(Task.CompletedTask);

        await _service.UnbindDeviceAsync(binding_id, user_id, "127.0.0.1");

        _binding_repo.Verify(r => r.DeactivateAsync(binding_id), Times.Once);
        _audit_svc.Verify(a => a.LogAsync(It.Is<AuditLogEntry>(e =>
            e.action == AuditAction.DeviceUnbind && e.resource_id == binding_id && e.user_id == user_id)), Times.Once);
    }

    [Fact]
    public async Task GetUserDevicesAsync_ReturnsDeviceList()
    {
        var user_id = Guid.NewGuid();
        var bindings = new List<DeviceBinding>
        {
            CreateTestBinding(user_id, "device-001"),
            CreateTestBinding(user_id, "device-002")
        };

        _binding_repo.Setup(r => r.GetByUserIdAsync(user_id)).ReturnsAsync(bindings);

        var result = await _service.GetUserDevicesAsync(user_id);

        Assert.Equal(2, result.Count);
        Assert.Equal("device-001", result[0].device_id);
        Assert.Equal("device-002", result[1].device_id);
    }
}
