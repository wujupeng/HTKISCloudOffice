using System.Security.Claims;
using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Domain.Enums;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace HTKISCloudOffice.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _user_repo;
    private readonly IRefreshTokenRepository _refresh_token_repo;
    private readonly IAuditService _audit_svc;
    private readonly IJwtTokenProvider _jwt_provider;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository user_repo,
        IRefreshTokenRepository refresh_token_repo,
        IAuditService audit_svc,
        IJwtTokenProvider jwt_provider,
        ILogger<AuthService> logger)
    {
        _user_repo = user_repo;
        _refresh_token_repo = refresh_token_repo;
        _audit_svc = audit_svc;
        _jwt_provider = jwt_provider;
        _logger = logger;
    }

    public async Task<AuthResult> LoginAsync(string username, string password, string ip_address)
    {
        var user = await _user_repo.GetByUsernameAsync(username);

        if (user == null || !user.is_active)
        {
            await _audit_svc.LogLoginAsync(Guid.Empty, ip_address, false);
            return AuthResult.Fail("AUTH_FAILED", "用户名或密码错误");
        }

        if (!VerifyPassword(password, user.password_hash))
        {
            await _audit_svc.LogLoginAsync(user.user_id, ip_address, false);
            return AuthResult.Fail("AUTH_FAILED", "用户名或密码错误");
        }

        var roles = user.user_roles.Select(ur => ur.Role.role_name).ToList();
        var token = _jwt_provider.GenerateToken(user.user_id, user.username, roles);
        var auto_login_token = _jwt_provider.GenerateAutoLoginToken(user.user_id, Guid.NewGuid().ToString());
        var expires_at = _jwt_provider.GetTokenExpiration(token);

        await _user_repo.UpdateAutoLoginTokenAsync(user.user_id, auto_login_token,
            DateTime.UtcNow.AddDays(30));
        await _user_repo.UpdateLastLoginAsync(user.user_id, DateTime.UtcNow);

        await _audit_svc.LogLoginAsync(user.user_id, ip_address, true);

        _logger.LogInformation("User {Username} logged in from {IpAddress}", username, ip_address);

        return AuthResult.Ok(token, auto_login_token, expires_at, new UserDto
        {
            user_id = user.user_id,
            username = user.username,
            display_name = user.display_name,
            department = user.department,
            is_active = user.is_active,
            roles = roles,
            bound_vm_id = user.bound_vm_id
        });
    }

    public async Task<AuthResult> AutoLoginAsync(string auto_login_token, string ip_address)
    {
        try
        {
            var principal = _jwt_provider.ValidateToken(auto_login_token);
            var user_id_claim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                ?? principal.FindFirst("sub")?.Value;

            if (user_id_claim == null || !Guid.TryParse(user_id_claim, out var user_id))
            {
                return AuthResult.Fail("AUTO_LOGIN_TOKEN_INVALID", "登录凭证无效，请重新输入用户名密码");
            }

            var user = await _user_repo.GetByIdAsync(user_id);
            if (user == null || !user.is_active || user.auto_login_token != auto_login_token)
            {
                return AuthResult.Fail("AUTO_LOGIN_TOKEN_INVALID", "登录凭证无效，请重新输入用户名密码");
            }

            if (user.auto_login_token_expires_at < DateTime.UtcNow)
            {
                return AuthResult.Fail("AUTO_LOGIN_TOKEN_INVALID", "登录凭证已过期，请重新输入用户名密码");
            }

            var roles = user.user_roles.Select(ur => ur.Role.role_name).ToList();
            var token = _jwt_provider.GenerateToken(user.user_id, user.username, roles);
            var new_auto_login_token = _jwt_provider.GenerateAutoLoginToken(user.user_id, Guid.NewGuid().ToString());
            var expires_at = _jwt_provider.GetTokenExpiration(token);

            await _user_repo.UpdateAutoLoginTokenAsync(user.user_id, new_auto_login_token,
                DateTime.UtcNow.AddDays(30));

            await _audit_svc.LogLoginAsync(user.user_id, ip_address, true);

            return AuthResult.Ok(token, new_auto_login_token, expires_at, new UserDto
            {
                user_id = user.user_id,
                username = user.username,
                display_name = user.display_name,
                department = user.department,
                is_active = user.is_active,
                roles = roles,
                bound_vm_id = user.bound_vm_id
            });
        }
        catch (Microsoft.IdentityModel.Tokens.SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "Auto-login token validation failed from {IpAddress}", ip_address);
            await _audit_svc.LogLoginAsync(Guid.Empty, ip_address, false);
            return AuthResult.Fail("AUTO_LOGIN_TOKEN_INVALID", "登录凭证无效，请重新输入用户名密码");
        }
    }

    public Task<ClaimsPrincipal> ValidateTokenAsync(string token)
    {
        return Task.FromResult(_jwt_provider.ValidateToken(token));
    }

    public async Task RevokeSessionAsync(string user_id)
    {
        if (Guid.TryParse(user_id, out var uid))
        {
            await _refresh_token_repo.RevokeAllForUserAsync(uid);
        }
    }

    public async Task<string> RefreshAutoLoginTokenAsync(string user_id)
    {
        if (!Guid.TryParse(user_id, out var uid))
            throw new ArgumentException("Invalid user_id");

        var new_token = _jwt_provider.GenerateAutoLoginToken(uid, Guid.NewGuid().ToString());
        await _user_repo.UpdateAutoLoginTokenAsync(uid, new_token, DateTime.UtcNow.AddDays(30));
        return new_token;
    }

    private static bool VerifyPassword(string password, string hash)
    {
        try
        {
            var hash_bytes = Convert.FromBase64String(hash);
            using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
            {
                Salt = hash_bytes.Take(16).ToArray(),
                DegreeOfParallelism = 4,
                MemorySize = 65536,
                Iterations = 3
            };
            var computed = argon2.GetBytes(32);
            return computed.SequenceEqual(hash_bytes.Skip(16).Take(32).ToArray());
        }
        catch
        {
            return false;
        }
    }
}