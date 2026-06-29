using System.Security.Claims;
using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Domain.Entities;
using HTKISCloudOffice.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace HTKISCloudOffice.Application.Services;

public class DeviceAuthService : IDeviceAuthService
{
    private readonly IDeviceBindingRepository _binding_repo;
    private readonly IUserRepository _user_repo;
    private readonly IJwtTokenProvider _jwt_provider;
    private readonly IAuditService _audit_svc;
    private readonly ILogger<DeviceAuthService> _logger;

    private const int MaxDeviceBindings = 5;
    private const int DeviceTokenExpirationDays = 30;

    public DeviceAuthService(
        IDeviceBindingRepository binding_repo,
        IUserRepository user_repo,
        IJwtTokenProvider jwt_provider,
        IAuditService audit_svc,
        ILogger<DeviceAuthService> logger)
    {
        _binding_repo = binding_repo;
        _user_repo = user_repo;
        _jwt_provider = jwt_provider;
        _audit_svc = audit_svc;
        _logger = logger;
    }

    public async Task<AuthResult> AutoLoginWithDeviceAsync(string device_token, string device_id, string ip_address)
    {
        try
        {
            var principal = _jwt_provider.ValidateToken(device_token);
            var user_id_claim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                ?? principal.FindFirst("sub")?.Value;

            if (user_id_claim == null || !Guid.TryParse(user_id_claim, out var token_user_id))
            {
                await _audit_svc.LogAsync(new AuditLogEntry
                {
                    user_id = Guid.Empty,
                    action = AuditAction.Login,
                    resource_type = "device_binding",
                    resource_id = Guid.Empty,
                    detail = $"Device token signature validation failed from {ip_address}",
                    ip_address = ip_address
                });
                return AuthResult.Fail("DEVICE_TOKEN_INVALID", "设备凭证无效");
            }

            var token_type = principal.FindFirst("token_type")?.Value;
            if (token_type != "device_binding")
            {
                return AuthResult.Fail("DEVICE_TOKEN_INVALID", "设备凭证类型不匹配");
            }

            var token_device_id = principal.FindFirst("device_id")?.Value;
            if (token_device_id != device_id)
            {
                return AuthResult.Fail("DEVICE_MISMATCH", "设备不匹配");
            }

            var binding = await _binding_repo.GetByDeviceIdAsync(token_user_id, device_id);
            if (binding == null || !binding.is_active)
            {
                return AuthResult.Fail("DEVICE_TOKEN_INVALID", "设备绑定不存在或已失效");
            }

            if (binding.device_token_expires_at < DateTime.UtcNow)
            {
                return AuthResult.Fail("DEVICE_TOKEN_EXPIRED", "设备凭证已过期");
            }

            var user = await _user_repo.GetByIdAsync(token_user_id);
            if (user == null || !user.is_active)
            {
                return AuthResult.Fail("AUTH_FAILED", "用户不存在或已禁用");
            }

            var roles = user.user_roles.Select(ur => ur.Role.role_name).ToList();
            var jwt_token = _jwt_provider.GenerateToken(user.user_id, user.username, roles);
            var expires_at = _jwt_provider.GetTokenExpiration(jwt_token);

            await _binding_repo.UpdateLastLoginAsync(binding.binding_id, DateTime.UtcNow, ip_address);

            await _audit_svc.LogLoginAsync(user.user_id, ip_address, true);

            _logger.LogInformation("Device auto-login for user {UserId} from device {DeviceId}", user.user_id, device_id);

            return AuthResult.Ok(jwt_token, string.Empty, expires_at, new UserDto
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
            _logger.LogWarning(ex, "Device token validation failed from {IpAddress}", ip_address);
            await _audit_svc.LogAsync(new AuditLogEntry
            {
                user_id = Guid.Empty,
                action = AuditAction.Login,
                resource_type = "device_binding",
                resource_id = Guid.Empty,
                detail = $"Device token signature validation failed from {ip_address}",
                ip_address = ip_address
            });
            return AuthResult.Fail("DEVICE_TOKEN_INVALID", "设备凭证签名验证失败");
        }
    }

    public async Task<DeviceBindResult> BindDeviceAsync(Guid user_id, string device_id, string device_name, string ip_address)
    {
        var active_count = await _binding_repo.CountActiveBindingsByUserIdAsync(user_id);
        if (active_count >= MaxDeviceBindings)
        {
            return DeviceBindResult.Fail("DEVICE_BIND_LIMIT", $"每用户最多绑定{MaxDeviceBindings}台设备");
        }

        var existing = await _binding_repo.GetByDeviceIdAsync(user_id, device_id);
        if (existing != null && existing.is_active)
        {
            return DeviceBindResult.Fail("DEVICE_ALREADY_BOUND", "该设备已绑定");
        }

        var device_token = _jwt_provider.GenerateAutoLoginToken(user_id, device_id);
        var expires_at = DateTime.UtcNow.AddDays(DeviceTokenExpirationDays);

        var binding = new DeviceBinding
        {
            user_id = user_id,
            device_id = device_id,
            device_name = device_name,
            device_token = device_token,
            device_token_expires_at = expires_at,
            last_login_at = DateTime.UtcNow,
            last_login_ip = ip_address,
            is_active = true
        };

        await _binding_repo.CreateAsync(binding);

        await _audit_svc.LogAsync(new AuditLogEntry
        {
            user_id = user_id,
            action = AuditAction.DeviceBind,
            resource_type = "device_binding",
            resource_id = binding.binding_id,
            detail = $"Bound device {device_name} ({device_id})",
            ip_address = ip_address
        });

        return DeviceBindResult.Ok(binding.binding_id, device_token, expires_at);
    }

    public async Task UnbindDeviceAsync(Guid binding_id, Guid user_id, string ip_address)
    {
        await _binding_repo.DeactivateAsync(binding_id);

        await _audit_svc.LogAsync(new AuditLogEntry
        {
            user_id = user_id,
            action = AuditAction.DeviceUnbind,
            resource_type = "device_binding",
            resource_id = binding_id,
            ip_address = ip_address
        });
    }

    public async Task<List<DeviceBindingDto>> GetUserDevicesAsync(Guid user_id)
    {
        var bindings = await _binding_repo.GetByUserIdAsync(user_id);
        return bindings.Select(b => new DeviceBindingDto
        {
            binding_id = b.binding_id,
            user_id = b.user_id,
            device_id = b.device_id,
            device_name = b.device_name,
            device_token_expires_at = b.device_token_expires_at,
            last_login_at = b.last_login_at,
            last_login_ip = b.last_login_ip,
            is_active = b.is_active,
            created_at = b.created_at
        }).ToList();
    }

    public async Task<TokenRefreshResult> RefreshTokenAsync(string token)
    {
        try
        {
            var principal = _jwt_provider.ValidateToken(token);
            var user_id_claim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                ?? principal.FindFirst("sub")?.Value;

            if (user_id_claim == null || !Guid.TryParse(user_id_claim, out var user_id))
            {
                return TokenRefreshResult.Fail("TOKEN_INVALID", "Token无效");
            }

            var user = await _user_repo.GetByIdAsync(user_id);
            if (user == null || !user.is_active)
            {
                return TokenRefreshResult.Fail("USER_INACTIVE", "用户不存在或已禁用");
            }

            var roles = user.user_roles.Select(ur => ur.Role.role_name).ToList();
            var new_token = _jwt_provider.GenerateToken(user.user_id, user.username, roles);
            var expires_at = _jwt_provider.GetTokenExpiration(new_token);

            return TokenRefreshResult.Ok(new_token, expires_at);
        }
        catch (Microsoft.IdentityModel.Tokens.SecurityTokenException)
        {
            return TokenRefreshResult.Fail("TOKEN_INVALID", "Token验证失败");
        }
    }

    public async Task<PagedResult<DeviceBindingDto>> GetAllDeviceBindingsAsync(DeviceBindingFilter filter)
    {
        var (items, total) = await _binding_repo.GetAllAsync(
            filter.page, filter.page_size, filter.user_id, filter.is_active);

        var user_ids = items.Select(b => b.user_id).Distinct().ToList();
        var users = new Dictionary<Guid, Domain.Entities.User>();
        foreach (var uid in user_ids)
        {
            var u = await _user_repo.GetByIdAsync(uid);
            if (u != null) users[uid] = u;
        }

        return new PagedResult<DeviceBindingDto>
        {
            items = items.Select(b => new DeviceBindingDto
            {
                binding_id = b.binding_id,
                user_id = b.user_id,
                device_id = b.device_id,
                device_name = b.device_name,
                device_token_expires_at = b.device_token_expires_at,
                last_login_at = b.last_login_at,
                last_login_ip = b.last_login_ip,
                is_active = b.is_active,
                created_at = b.created_at,
                username = users.GetValueOrDefault(b.user_id)?.username,
                display_name = users.GetValueOrDefault(b.user_id)?.display_name
            }).ToList(),
            total = total,
            page = filter.page,
            page_size = filter.page_size
        };
    }

    public async Task<DeviceTokenValidationResult> ValidateDeviceTokenAsync(string device_token, string device_id)
    {
        try
        {
            var principal = _jwt_provider.ValidateToken(device_token);
            var user_id_claim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                ?? principal.FindFirst("sub")?.Value;

            if (user_id_claim == null || !Guid.TryParse(user_id_claim, out var user_id))
            {
                return DeviceTokenValidationResult.Fail("DEVICE_TOKEN_INVALID", "设备凭证无效");
            }

            var token_type = principal.FindFirst("token_type")?.Value;
            if (token_type != "device_binding")
            {
                return DeviceTokenValidationResult.Fail("DEVICE_TOKEN_INVALID", "凭证类型不匹配");
            }

            var token_device_id = principal.FindFirst("device_id")?.Value;
            if (token_device_id != device_id)
            {
                return DeviceTokenValidationResult.Fail("DEVICE_MISMATCH", "设备不匹配");
            }

            return DeviceTokenValidationResult.Ok(user_id, device_id);
        }
        catch (Microsoft.IdentityModel.Tokens.SecurityTokenException)
        {
            return DeviceTokenValidationResult.Fail("DEVICE_TOKEN_INVALID", "设备凭证签名验证失败");
        }
    }
}