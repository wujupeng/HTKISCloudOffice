using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Domain.Entities;
using HTKISCloudOffice.Domain.Enums;
using HTKISCloudOffice.Domain.ValueObjects;

namespace HTKISCloudOffice.Application.Services;

public class FileShareService : IFileShareService
{
    private readonly ISharedDriveRepository _drive_repo;
    private readonly IPermissionService _perm_svc;
    private readonly ISambaConfigManager _samba_mgr;
    private readonly IAuditService _audit_svc;

    public FileShareService(
        ISharedDriveRepository drive_repo,
        IPermissionService perm_svc,
        ISambaConfigManager samba_mgr,
        IAuditService audit_svc)
    {
        _drive_repo = drive_repo;
        _perm_svc = perm_svc;
        _samba_mgr = samba_mgr;
        _audit_svc = audit_svc;
    }

    public async Task<List<SharedDriveDto>> GetSharedDrivesForUserAsync(string user_id)
    {
        var roles = await _perm_svc.GetUserRolesAsync(user_id);
        var role_ids = roles.Select(r => Guid.Parse(r.role_id)).ToList();
        var drives = await _drive_repo.GetByRoleIdsAsync(role_ids);

        return drives.Select(d => new SharedDriveDto
        {
            drive_id = d.drive_id.ToString(),
            drive_name = d.drive_name,
            drive_type = d.drive_type,
            drive_letter = d.drive_letter,
            samba_path = $"\\\\samba.htkis.local\\{d.samba_path.Split('/').Last()}",
            access_mode = GetAccessModeForUser(d, role_ids)
        }).ToList();
    }

    public async Task<DriveMappingResult> GetDriveMappingAsync(string user_id)
    {
        var drives = await GetSharedDrivesForUserAsync(user_id);
        return new DriveMappingResult
        {
            mappings = drives.Select(d => new DriveMappingDto
            {
                drive_letter = d.drive_letter,
                drive_name = d.drive_name,
                samba_path = d.samba_path,
                access_mode = d.access_mode
            }).ToList()
        };
    }

    public async Task<SharedDriveDto> CreateSharedDriveAsync(CreateSharedDriveRequest request)
    {
        var drive = new SharedDrive
        {
            drive_name = request.drive_name,
            drive_type = request.drive_type,
            samba_path = request.samba_path,
            drive_letter = request.drive_letter,
            allowed_permissions = request.permissions.Select(p => new DrivePermission
            {
                role_id = p.role_id,
                access_mode = p.access_mode
            }).ToList(),
            quota_mb = request.quota_mb
        };

        await _drive_repo.CreateAsync(drive);

        var allowed_users = request.permissions.Select(p => p.role_id).ToList();
        await _samba_mgr.CreateShareAsync(
            request.samba_path.Split('/').Last(),
            request.samba_path,
            allowed_users);

        return new SharedDriveDto
        {
            drive_id = drive.drive_id.ToString(),
            drive_name = drive.drive_name,
            drive_type = drive.drive_type,
            drive_letter = drive.drive_letter,
            samba_path = $"\\\\samba.htkis.local\\{request.samba_path.Split('/').Last()}",
            access_mode = AccessMode.ReadWrite
        };
    }

    public async Task UpdateDrivePermissionsAsync(string drive_id, List<DrivePermissionDto> permissions)
    {
        var drive = await _drive_repo.GetByIdAsync(Guid.Parse(drive_id));
        if (drive == null) return;

        drive.allowed_permissions = permissions.Select(p => new DrivePermission
        {
            role_id = p.role_id,
            access_mode = p.access_mode
        }).ToList();

        await _drive_repo.UpdateAsync(drive);

        var share_name = drive.samba_path.Split('/').Last();
        var allowed_users = permissions.Select(p => p.role_id).ToList();
        await _samba_mgr.UpdateSharePermissionsAsync(share_name, allowed_users);
    }

    public async Task<QuotaInfo> CheckDiskQuotaAsync(string drive_id)
    {
        var drive = await _drive_repo.GetByIdAsync(Guid.Parse(drive_id));
        if (drive == null)
            return new QuotaInfo { drive_id = drive_id };

        var dir_info = new DirectoryInfo(drive.samba_path);
        long used_mb = 0;
        if (dir_info.Exists)
        {
            used_mb = dir_info.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) / (1024 * 1024);
        }

        return new QuotaInfo
        {
            drive_id = drive_id,
            quota_mb = drive.quota_mb,
            used_mb = used_mb,
            available_mb = drive.quota_mb > 0 ? drive.quota_mb - used_mb : 0
        };
    }

    private static AccessMode GetAccessModeForUser(SharedDrive drive, List<Guid> role_ids)
    {
        foreach (var perm in drive.allowed_permissions)
        {
            if (Guid.TryParse(perm.role_id, out var rid) && role_ids.Contains(rid))
            {
                if (perm.access_mode == AccessMode.ReadWrite) return AccessMode.ReadWrite;
            }
        }
        return AccessMode.ReadOnly;
    }
}