using System.Security.Claims;
using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Application.Services;
using HTKISCloudOffice.Domain.Entities;
using HTKISCloudOffice.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;

namespace HTKISCloudOffice.UnitTests;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _user_repo;
    private readonly Mock<IRefreshTokenRepository> _refresh_token_repo;
    private readonly Mock<IAuditService> _audit_svc;
    private readonly Mock<IJwtTokenProvider> _jwt_provider;
    private readonly Mock<ILogger<AuthService>> _logger;
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _user_repo = new Mock<IUserRepository>();
        _refresh_token_repo = new Mock<IRefreshTokenRepository>();
        _audit_svc = new Mock<IAuditService>();
        _jwt_provider = new Mock<IJwtTokenProvider>();
        _logger = new Mock<ILogger<AuthService>>();
        _service = new AuthService(_user_repo.Object, _refresh_token_repo.Object,
            _audit_svc.Object, _jwt_provider.Object, _logger.Object);
    }

    private User CreateTestUser(bool active = true, string? auto_login_token = null)
    {
        return new User
        {
            user_id = Guid.NewGuid(),
            username = "testuser",
            display_name = "Test User",
            department = "IT",
            is_active = active,
            password_hash = CreateArgon2Hash("password123"),
            auto_login_token = auto_login_token,
            auto_login_token_expires_at = DateTime.UtcNow.AddDays(30),
            user_roles = new List<UserRole>
            {
                new()
                {
                    Role = new Role { role_id = Guid.NewGuid(), role_name = "all_staff" }
                }
            }
        };
    }

    private static string CreateArgon2Hash(string password)
    {
        using var argon2 = new Konscious.Security.Cryptography.Argon2id(
            System.Text.Encoding.UTF8.GetBytes(password))
        {
            Salt = new byte[16],
            DegreeOfParallelism = 4,
            MemorySize = 65536,
            Iterations = 3
        };
        var hash = argon2.GetBytes(32);
        var salt = new byte[16];
        var result = new byte[48];
        Array.Copy(salt, result, 16);
        Array.Copy(hash, 0, result, 16, 32);
        return Convert.ToBase64String(result);
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsSuccess()
    {
        var user = CreateTestUser();
        _user_repo.Setup(r => r.GetByUsernameAsync("testuser")).ReturnsAsync(user);
        _jwt_provider.Setup(p => p.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<int?>()))
            .Returns("access_token");
        _jwt_provider.Setup(p => p.GenerateAutoLoginToken(It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns("auto_login_token");
        _jwt_provider.Setup(p => p.GetTokenExpiration(It.IsAny<string>()))
            .Returns(DateTime.UtcNow.AddHours(8));

        var result = await _service.LoginAsync("testuser", "password123", "127.0.0.1");

        Assert.True(result.success);
        Assert.Equal("access_token", result.token);
        Assert.Equal("auto_login_token", result.auto_login_token);
        Assert.NotNull(result.user);
        Assert.Equal("testuser", result.user.username);
    }

    [Fact]
    public async Task LoginAsync_WithInvalidUsername_ReturnsAuthFailed()
    {
        _user_repo.Setup(r => r.GetByUsernameAsync("nonexistent")).ReturnsAsync((User?)null);

        var result = await _service.LoginAsync("nonexistent", "password123", "127.0.0.1");

        Assert.False(result.success);
        Assert.Equal("AUTH_FAILED", result.error_code);
    }

    [Fact]
    public async Task LoginAsync_WithInactiveUser_ReturnsAuthFailed()
    {
        var user = CreateTestUser(active: false);
        _user_repo.Setup(r => r.GetByUsernameAsync("testuser")).ReturnsAsync(user);

        var result = await _service.LoginAsync("testuser", "password123", "127.0.0.1");

        Assert.False(result.success);
        Assert.Equal("AUTH_FAILED", result.error_code);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ReturnsAuthFailed()
    {
        var user = CreateTestUser();
        _user_repo.Setup(r => r.GetByUsernameAsync("testuser")).ReturnsAsync(user);

        var result = await _service.LoginAsync("testuser", "wrong_password", "127.0.0.1");

        Assert.False(result.success);
        Assert.Equal("AUTH_FAILED", result.error_code);
    }

    [Fact]
    public async Task LoginAsync_OnFailure_LogsAudit()
    {
        _user_repo.Setup(r => r.GetByUsernameAsync("testuser")).ReturnsAsync((User?)null);

        await _service.LoginAsync("testuser", "wrong", "127.0.0.1");

        _audit_svc.Verify(a => a.LogLoginAsync(Guid.Empty, "127.0.0.1", false), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_OnSuccess_LogsAudit()
    {
        var user = CreateTestUser();
        _user_repo.Setup(r => r.GetByUsernameAsync("testuser")).ReturnsAsync(user);
        _jwt_provider.Setup(p => p.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<int?>()))
            .Returns("token");
        _jwt_provider.Setup(p => p.GenerateAutoLoginToken(It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns("alt");
        _jwt_provider.Setup(p => p.GetTokenExpiration(It.IsAny<string>()))
            .Returns(DateTime.UtcNow.AddHours(8));

        await _service.LoginAsync("testuser", "password123", "127.0.0.1");

        _audit_svc.Verify(a => a.LogLoginAsync(user.user_id, "127.0.0.1", true), Times.Once);
    }

    [Fact]
    public async Task AutoLoginAsync_WithValidToken_ReturnsSuccess()
    {
        var user = CreateTestUser(auto_login_token: "valid_token");
        var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.user_id.ToString())
        }));

        _jwt_provider.Setup(p => p.ValidateToken("valid_token")).Returns(claims);
        _user_repo.Setup(r => r.GetByIdAsync(user.user_id)).ReturnsAsync(user);
        _jwt_provider.Setup(p => p.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<int?>()))
            .Returns("new_access_token");
        _jwt_provider.Setup(p => p.GenerateAutoLoginToken(It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns("new_auto_token");
        _jwt_provider.Setup(p => p.GetTokenExpiration(It.IsAny<string>()))
            .Returns(DateTime.UtcNow.AddHours(8));

        var result = await _service.AutoLoginAsync("valid_token", "127.0.0.1");

        Assert.True(result.success);
        Assert.Equal("new_access_token", result.token);
        Assert.Equal("new_auto_token", result.auto_login_token);
    }

    [Fact]
    public async Task AutoLoginAsync_WithExpiredToken_ReturnsInvalid()
    {
        var user = CreateTestUser(auto_login_token: "expired_token");
        user.auto_login_token_expires_at = DateTime.UtcNow.AddDays(-1);

        var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.user_id.ToString())
        }));

        _jwt_provider.Setup(p => p.ValidateToken("expired_token")).Returns(claims);
        _user_repo.Setup(r => r.GetByIdAsync(user.user_id)).ReturnsAsync(user);

        var result = await _service.AutoLoginAsync("expired_token", "127.0.0.1");

        Assert.False(result.success);
        Assert.Equal("AUTO_LOGIN_TOKEN_INVALID", result.error_code);
    }

    [Fact]
    public async Task AutoLoginAsync_WithMismatchedToken_ReturnsInvalid()
    {
        var user = CreateTestUser(auto_login_token: "different_token");

        var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.user_id.ToString())
        }));

        _jwt_provider.Setup(p => p.ValidateToken("wrong_token")).Returns(claims);
        _user_repo.Setup(r => r.GetByIdAsync(user.user_id)).ReturnsAsync(user);

        var result = await _service.AutoLoginAsync("wrong_token", "127.0.0.1");

        Assert.False(result.success);
        Assert.Equal("AUTO_LOGIN_TOKEN_INVALID", result.error_code);
    }

    [Fact]
    public async Task RevokeSessionAsync_RevokesAllTokens()
    {
        var user_id = Guid.NewGuid();

        await _service.RevokeSessionAsync(user_id.ToString());

        _refresh_token_repo.Verify(r => r.RevokeAllForUserAsync(user_id), Times.Once);
    }

    [Fact]
    public async Task RefreshAutoLoginTokenAsync_ReturnsNewToken()
    {
        var user_id = Guid.NewGuid();
        _jwt_provider.Setup(p => p.GenerateAutoLoginToken(user_id, It.IsAny<string>()))
            .Returns("new_auto_token");

        var result = await _service.RefreshAutoLoginTokenAsync(user_id.ToString());

        Assert.Equal("new_auto_token", result);
        _user_repo.Verify(r => r.UpdateAutoLoginTokenAsync(user_id, "new_auto_token", It.IsAny<DateTime>()), Times.Once);
    }
}