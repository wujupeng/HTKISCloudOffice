using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Domain.Enums;

namespace HTKISCloudOffice.Application.Interfaces;

public interface IPermissionService
{
    Task<List<RoleDto>> GetUserRolesAsync(string user_id);
    Task<bool> CheckAppAccessAsync(string user_id, string app_id);
    Task<AccessMode?> CheckDriveAccessAsync(string user_id, string drive_id);
    Task AssignRolesAsync(string user_id, List<string> role_ids, string operator_id);
    Task RevokeRolesAsync(string user_id, List<string> role_ids, string operator_id);
    Task<List<RoleDto>> GetAllRolesAsync();
    Task<RoleDto> CreateRoleAsync(CreateRoleRequest request, string operator_id);
    Task InvalidateUserPermissionCacheAsync(string user_id);
}

public interface ICacheProvider
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null);
    Task RemoveAsync(string key);
    Task RemoveByPrefixAsync(string prefix);
}