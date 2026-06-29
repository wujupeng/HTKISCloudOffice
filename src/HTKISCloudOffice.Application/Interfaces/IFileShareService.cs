using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Domain.Enums;

namespace HTKISCloudOffice.Application.Interfaces;

public interface IFileShareService
{
    Task<List<SharedDriveDto>> GetSharedDrivesForUserAsync(string user_id);
    Task<DriveMappingResult> GetDriveMappingAsync(string user_id);
    Task<SharedDriveDto> CreateSharedDriveAsync(CreateSharedDriveRequest request);
    Task UpdateDrivePermissionsAsync(string drive_id, List<DrivePermissionDto> permissions);
    Task<QuotaInfo> CheckDiskQuotaAsync(string drive_id);
}

public interface ISambaConfigManager
{
    Task CreateShareAsync(string share_name, string path, List<string> allowed_users);
    Task UpdateSharePermissionsAsync(string share_name, List<string> allowed_users);
    Task RemoveShareAsync(string share_name);
    Task ReloadConfigAsync();
    Task<bool> ValidateShareAsync(string share_name);
}