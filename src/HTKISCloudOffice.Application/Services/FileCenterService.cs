using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Domain.Enums;

namespace HTKISCloudOffice.Application.Services;

public class FileCenterService : IFileCenterService
{
    private readonly ISharedDriveRepository _drive_repo;
    private readonly IPermissionService _perm_svc;
    private readonly ISambaFileClient _samba_client;
    private readonly IFilePreviewService _preview_svc;
    private readonly IAuditService _audit_svc;

    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".wsf", ".msi", ".com", ".scr"
    };

    private const long MaxFileSizeBytes = 200 * 1024 * 1024;

    public FileCenterService(
        ISharedDriveRepository drive_repo,
        IPermissionService perm_svc,
        ISambaFileClient samba_client,
        IFilePreviewService preview_svc,
        IAuditService audit_svc)
    {
        _drive_repo = drive_repo;
        _perm_svc = perm_svc;
        _samba_client = samba_client;
        _preview_svc = preview_svc;
        _audit_svc = audit_svc;
    }

    public async Task<List<FileDriveDto>> GetDrivesForUserAsync(string user_id)
    {
        var roles = await _perm_svc.GetUserRolesAsync(user_id);
        var role_ids = roles.Select(r => Guid.Parse(r.role_id)).ToList();
        var drives = await _drive_repo.GetByRoleIdsAsync(role_ids);

        return drives.Select(d => new FileDriveDto
        {
            drive_id = d.drive_id.ToString(),
            drive_name = d.drive_name,
            drive_type = d.drive_type,
            samba_path = d.samba_path,
            access_mode = GetAccessModeForUser(d, role_ids)
        }).ToList();
    }

    public async Task<FileListResult> ListFilesAsync(string user_id, string drive_id, string? path)
    {
        if (!Guid.TryParse(drive_id, out var did)) return FileListResult.Fail("INVALID_ID", "无效的盘ID");

        var drive = await _drive_repo.GetByIdAsync(did);
        if (drive == null) return FileListResult.Fail("DRIVE_NOT_FOUND", "目录不存在");

        var has_access = await CheckDriveAccessAsync(user_id, did);
        if (!has_access) return FileListResult.Fail("ACCESS_DENIED", "无权访问此目录");

        var relative_path = BuildRelativePath(drive.samba_path, path);
        var items = await _samba_client.ListDirectoryAsync(relative_path);

        var file_items = items.Select(i => new FileItemDto
        {
            name = i.name,
            path = i.path,
            is_directory = i.is_directory,
            size = i.size,
            last_modified = i.last_modified,
            extension = i.is_directory ? string.Empty : Path.GetExtension(i.name),
            content_type = i.is_directory ? string.Empty : GetContentType(Path.GetExtension(i.name))
        }).ToList();

        return FileListResult.Ok(file_items, path ?? "/");
    }

    public async Task<FileUploadResult> UploadFileAsync(string user_id, string drive_id, string path, string file_name, Stream file_stream, long file_size, string ip_address)
    {
        if (!Guid.TryParse(drive_id, out var did)) return FileUploadResult.Fail("INVALID_ID", "无效的盘ID");

        var ext = Path.GetExtension(file_name);
        if (BlockedExtensions.Contains(ext)) return FileUploadResult.Fail("BLOCKED_FILE_TYPE", "不允许上传可执行文件");
        if (file_size > MaxFileSizeBytes) return FileUploadResult.Fail("FILE_TOO_LARGE", "文件大小超过200MB限制");

        var drive = await _drive_repo.GetByIdAsync(did);
        if (drive == null) return FileUploadResult.Fail("DRIVE_NOT_FOUND", "目录不存在");

        var has_access = await CheckDriveWriteAccessAsync(user_id, did);
        if (!has_access) return FileUploadResult.Fail("ACCESS_DENIED", "无权写入此目录");

        var relative_path = BuildRelativePath(drive.samba_path, path);
        var result = await _samba_client.UploadFileAsync(relative_path, SanitizeFileName(file_name), file_stream);

        if (result != null)
        {
            await _audit_svc.LogAsync(new AuditLogEntry
            {
                user_id = Guid.Parse(user_id),
                action = AuditAction.FileUpload,
                resource_type = "SharedDrive",
                resource_id = Guid.Parse(drive_id),
                detail = $"{{\"file_name\":\"{file_name}\",\"path\":\"{path}\"}}",
                ip_address = ip_address
            });
            return FileUploadResult.Ok(new FileItemDto
            {
                name = result.name,
                path = result.path,
                is_directory = result.is_directory,
                size = result.size,
                last_modified = result.last_modified,
                extension = Path.GetExtension(result.name),
                content_type = GetContentType(Path.GetExtension(result.name))
            });
        }

        return FileUploadResult.Fail("UPLOAD_FAILED", "上传失败");
    }

    public async Task<FileDownloadResult> DownloadFileAsync(string user_id, string drive_id, string file_path)
    {
        if (!Guid.TryParse(drive_id, out var did)) return FileDownloadResult.Fail("INVALID_ID", "无效的盘ID");

        var drive = await _drive_repo.GetByIdAsync(did);
        if (drive == null) return FileDownloadResult.Fail("DRIVE_NOT_FOUND", "目录不存在");

        var has_access = await CheckDriveAccessAsync(user_id, did);
        if (!has_access) return FileDownloadResult.Fail("ACCESS_DENIED", "无权访问此目录");

        var relative_path = BuildRelativePath(drive.samba_path, file_path);
        var stream = await _samba_client.DownloadFileAsync(relative_path);
        var file_name = Path.GetFileName(file_path);

        return FileDownloadResult.Ok(stream, file_name, GetContentType(Path.GetExtension(file_name)), stream.Length);
    }

    public async Task<FilePreviewResult> PreviewFileAsync(string user_id, string drive_id, string file_path)
    {
        if (!Guid.TryParse(drive_id, out var did)) return FilePreviewResult.Fail("INVALID_ID", "无效的盘ID");

        var drive = await _drive_repo.GetByIdAsync(did);
        if (drive == null) return FilePreviewResult.Fail("DRIVE_NOT_FOUND", "目录不存在");

        var has_access = await CheckDriveAccessAsync(user_id, did);
        if (!has_access) return FilePreviewResult.Fail("ACCESS_DENIED", "无权访问此目录");

        var relative_path = BuildRelativePath(drive.samba_path, file_path);
        var ext = Path.GetExtension(file_path);
        var content_type = GetContentType(ext);
        return await _preview_svc.PreviewAsync(relative_path, content_type);
    }

    public async Task<FileOperationResult> DeleteFileAsync(string user_id, string drive_id, string file_path, string ip_address)
    {
        if (!Guid.TryParse(drive_id, out var did)) return FileOperationResult.Fail("INVALID_ID", "无效的盘ID");

        var drive = await _drive_repo.GetByIdAsync(did);
        if (drive == null) return FileOperationResult.Fail("DRIVE_NOT_FOUND", "目录不存在");

        var has_access = await CheckDriveWriteAccessAsync(user_id, did);
        if (!has_access) return FileOperationResult.Fail("ACCESS_DENIED", "无权删除此目录中的文件");

        var relative_path = BuildRelativePath(drive.samba_path, file_path);
        await _samba_client.DeleteFileAsync(relative_path);

        await _audit_svc.LogAsync(new AuditLogEntry
        {
            user_id = Guid.Parse(user_id),
            action = AuditAction.FileDelete,
            resource_type = "SharedDrive",
            resource_id = Guid.Parse(drive_id),
            detail = $"{{\"file_path\":\"{file_path}\"}}",
            ip_address = ip_address
        });

        return FileOperationResult.Ok();
    }

    public async Task<DirectoryCreateResult> CreateDirectoryAsync(string user_id, string drive_id, string path, string dir_name)
    {
        if (!Guid.TryParse(drive_id, out var did)) return DirectoryCreateResult.Fail("INVALID_ID", "无效的盘ID");

        var drive = await _drive_repo.GetByIdAsync(did);
        if (drive == null) return DirectoryCreateResult.Fail("DRIVE_NOT_FOUND", "目录不存在");

        var has_access = await CheckDriveWriteAccessAsync(user_id, did);
        if (!has_access) return DirectoryCreateResult.Fail("ACCESS_DENIED", "无权在此目录中创建文件夹");

        var relative_path = BuildRelativePath(drive.samba_path, path);
        var result = await _samba_client.CreateDirectoryAsync(relative_path, SanitizeFileName(dir_name));

        return DirectoryCreateResult.Ok(new FileItemDto
        {
            name = result.name,
            path = result.path,
            is_directory = result.is_directory,
            size = result.size,
            last_modified = result.last_modified
        });
    }

    private async Task<bool> CheckDriveAccessAsync(string user_id, Guid drive_id)
    {
        var roles = await _perm_svc.GetUserRolesAsync(user_id);
        var role_ids = roles.Select(r => Guid.Parse(r.role_id)).ToList();
        var drives = await _drive_repo.GetByRoleIdsAsync(role_ids);
        return drives.Any(d => d.drive_id == drive_id);
    }

    private async Task<bool> CheckDriveWriteAccessAsync(string user_id, Guid drive_id)
    {
        var roles = await _perm_svc.GetUserRolesAsync(user_id);
        var role_ids = roles.Select(r => Guid.Parse(r.role_id)).ToList();
        var drives = await _drive_repo.GetByRoleIdsAsync(role_ids);
        return drives.Any(d => d.drive_id == drive_id && GetAccessModeForUser(d, role_ids) != AccessMode.ReadOnly);
    }

    private static AccessMode GetAccessModeForUser(Domain.Entities.SharedDrive drive, List<Guid> role_ids)
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

    private static string BuildRelativePath(string samba_path, string? relative_path)
    {
        var base_path = samba_path.Split('/').LastOrDefault() ?? samba_path;
        if (string.IsNullOrWhiteSpace(relative_path) || relative_path == "/")
            return base_path;
        if (relative_path.Contains("..")) throw new InvalidOperationException("路径遍历攻击被阻止");
        return $"{base_path}/{relative_path.TrimStart('/')}";
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };
        foreach (var c in invalid) name = name.Replace(c, '_');
        return name;
    }

    private static string GetContentType(string ext) => ext.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        ".webp" => "image/webp",
        ".svg" => "image/svg+xml",
        ".txt" => "text/plain",
        ".csv" => "text/csv",
        ".json" => "application/json",
        ".xml" => "application/xml",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        _ => "application/octet-stream"
    };
}
