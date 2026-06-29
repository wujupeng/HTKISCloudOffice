using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Domain.Entities;
using HTKISCloudOffice.Domain.Enums;
using HTKISCloudOffice.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace HTKISCloudOffice.Application.Services;

public class PermissionService : IPermissionService
{
    private readonly IRoleRepository _role_repo;
    private readonly IUserRepository _user_repo;
    private readonly ICacheProvider _cache;
    private readonly IAuditService _audit_svc;
    private readonly ILogger<PermissionService> _logger;

    public PermissionService(
        IRoleRepository role_repo,
        IUserRepository user_repo,
        ICacheProvider cache,
        IAuditService audit_svc,
        ILogger<PermissionService> logger)
    {
        _role_repo = role_repo;
        _user_repo = user_repo;
        _cache = cache;
        _audit_svc = audit_svc;
        _logger = logger;
    }

    public async Task<List<RoleDto>> GetUserRolesAsync(string user_id)
    {
        var cache_key = $"perm:user:{user_id}";
        var cached = await _cache.GetAsync<List<RoleDto>>(cache_key);
        if (cached != null) return cached;

        if (!Guid.TryParse(user_id, out var uid)) return new();

        var roles = await _role_repo.GetByUserIdAsync(uid);
        var result = roles.Select(r => new RoleDto
        {
            role_id = r.role_id.ToString(),
            role_name = r.role_name,
            description = r.description,
            permissions = r.permissions.Select(p => new PermissionEntryDto
            {
                resource_type = p.resource_type,
                resource_id = p.resource_id,
                access_mode = p.access_mode
            }).ToList()
        }).ToList();

        await _cache.SetAsync(cache_key, result);
        return result;
    }

    public async Task<bool> CheckAppAccessAsync(string user_id, string app_id)
    {
        try
        {
            var roles = await GetUserRolesAsync(user_id);
            var role_ids = roles.Select(r => Guid.Parse(r.role_id)).ToList();

            var all_apps = await _role_repo.GetByIdsAsync(role_ids);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Permission check timeout for user {UserId}, app {AppId} - denying access", user_id, app_id);
            return false;
        }
    }

    public async Task<AccessMode?> CheckDriveAccessAsync(string user_id, string drive_id)
    {
        try
        {
            var roles = await GetUserRolesAsync(user_id);
            foreach (var role in roles)
            {
                var drive_perm = role.permissions.FirstOrDefault(p =>
                    p.resource_type == ResourceType.SharedDrive && p.resource_id == drive_id);
                if (drive_perm != null)
                    return drive_perm.access_mode;
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Permission check timeout for user {UserId}, drive {DriveId} - denying access", user_id, drive_id);
            return null;
        }
    }

    public async Task AssignRolesAsync(string user_id, List<string> role_ids, string operator_id)
    {
        if (!Guid.TryParse(user_id, out var uid)) return;
        var user = await _user_repo.GetByIdAsync(uid);
        if (user == null) return;

        await _audit_svc.LogPermissionChangeAsync(operator_id, user_id,
            $"Assigned roles: {string.Join(",", role_ids)}", "");

        await InvalidateUserPermissionCacheAsync(user_id);
    }

    public async Task RevokeRolesAsync(string user_id, List<string> role_ids, string operator_id)
    {
        if (!Guid.TryParse(user_id, out var uid)) return;

        var current_roles = await _role_repo.GetByUserIdAsync(uid);
        var super_admin_role = current_roles.FirstOrDefault(r => r.role_name == "super_admin");
        if (super_admin_role != null && role_ids.Contains(super_admin_role.role_id.ToString()))
        {
            var all_super_admins = await _user_repo.ListAsync(1, 1000);
            var super_admin_count = all_super_admins.users.Count(u =>
                u.user_roles.Any(ur => ur.Role.role_name == "super_admin"));

            if (super_admin_count <= 1)
            {
                _logger.LogWarning("Cannot revoke last super_admin role from user {UserId}", user_id);
                return;
            }
        }

        await _audit_svc.LogPermissionChangeAsync(operator_id, user_id,
            $"Revoked roles: {string.Join(",", role_ids)}", "");

        await InvalidateUserPermissionCacheAsync(user_id);
    }

    public async Task<List<RoleDto>> GetAllRolesAsync()
    {
        var roles = await _role_repo.GetAllAsync();
        return roles.Select(r => new RoleDto
        {
            role_id = r.role_id.ToString(),
            role_name = r.role_name,
            description = r.description,
            permissions = r.permissions.Select(p => new PermissionEntryDto
            {
                resource_type = p.resource_type,
                resource_id = p.resource_id,
                access_mode = p.access_mode
            }).ToList()
        }).ToList();
    }

    public async Task<RoleDto> CreateRoleAsync(CreateRoleRequest request, string operator_id)
    {
        var role = new Role
        {
            role_name = request.role_name,
            description = request.description,
            permissions = request.permissions.Select(p => new PermissionEntry
            {
                resource_type = p.resource_type,
                resource_id = p.resource_id,
                access_mode = p.access_mode
            }).ToList()
        };

        await _role_repo.CreateAsync(role);

        await _audit_svc.LogPermissionChangeAsync(operator_id, "",
            $"Created role: {request.role_name}", "");

        return new RoleDto
        {
            role_id = role.role_id.ToString(),
            role_name = role.role_name,
            description = role.description,
            permissions = request.permissions
        };
    }

    public async Task InvalidateUserPermissionCacheAsync(string user_id)
    {
        await _cache.RemoveAsync($"perm:user:{user_id}");
    }
}