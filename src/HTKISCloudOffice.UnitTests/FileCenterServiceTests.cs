using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Application.Services;
using HTKISCloudOffice.Domain.Entities;
using HTKISCloudOffice.Domain.Enums;
using DriveType = HTKISCloudOffice.Domain.Enums.DriveType;
using System.Reflection;
using HTKISCloudOffice.Domain.ValueObjects;
using Moq;

namespace HTKISCloudOffice.UnitTests;

public class FileCenterServiceTests
{
    private readonly Mock<ISharedDriveRepository> _drive_repo;
    private readonly Mock<IPermissionService> _perm_svc;
    private readonly Mock<ISambaFileClient> _samba_client;
    private readonly Mock<IFilePreviewService> _preview_svc;
    private readonly Mock<IAuditService> _audit_svc;
    private readonly FileCenterService _service;

    public FileCenterServiceTests()
    {
        _drive_repo = new Mock<ISharedDriveRepository>();
        _perm_svc = new Mock<IPermissionService>();
        _samba_client = new Mock<ISambaFileClient>();
        _preview_svc = new Mock<IFilePreviewService>();
        _audit_svc = new Mock<IAuditService>();
        _service = new FileCenterService(
            _drive_repo.Object,
            _perm_svc.Object,
            _samba_client.Object,
            _preview_svc.Object,
            _audit_svc.Object);
    }
    private static SharedDrive CreateTestDrive(Guid? role_id = null, AccessMode access_mode = AccessMode.ReadWrite)
    {
        var rid = role_id ?? Guid.NewGuid();
        return new SharedDrive
        {
            drive_id = Guid.NewGuid(),
            drive_name = "Test Drive",
            drive_type = DriveType.Public,
            samba_path = "/data/shares/test",
            drive_letter = "Z:",
            allowed_permissions = new List<DrivePermission>
            {
                new() { role_id = rid.ToString(), access_mode = access_mode }
            },
            quota_mb = 10240
        };
    }

    private static RoleDto CreateTestRoleDto(Guid? role_id = null)
    {
        return new RoleDto { role_id = (role_id ?? Guid.NewGuid()).ToString(), role_name = "test_role" };
    }

    private void SetupDriveAccess(SharedDrive drive, RoleDto role)
    {
        _perm_svc.Setup(p => p.GetUserRolesAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<RoleDto> { role });
        _drive_repo.Setup(r => r.GetByRoleIdsAsync(It.IsAny<List<Guid>>()))
            .ReturnsAsync(new List<SharedDrive> { drive });
    }

    private void SetupDriveWriteAccess(SharedDrive drive, RoleDto role)
    {
        _perm_svc.Setup(p => p.GetUserRolesAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<RoleDto> { role });
        _drive_repo.Setup(r => r.GetByRoleIdsAsync(It.IsAny<List<Guid>>()))
            .ReturnsAsync(new List<SharedDrive> { drive });
    }

    private void SetupNoDriveAccess()
    {
        _perm_svc.Setup(p => p.GetUserRolesAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<RoleDto>());
        _drive_repo.Setup(r => r.GetByRoleIdsAsync(It.IsAny<List<Guid>>()))
            .ReturnsAsync(new List<SharedDrive>());
    }
    [Fact]
    public async Task UploadFileAsync_NormalUpload_ReturnsSuccess()
    {
        var role = CreateTestRoleDto();
        var drive = CreateTestDrive(Guid.Parse(role.role_id));
        _drive_repo.Setup(r => r.GetByIdAsync(drive.drive_id)).ReturnsAsync(drive);
        SetupDriveWriteAccess(drive, role);
        var file_info = new SambaFileInfo { name = "test.txt", path = "test/test.txt", is_directory = false, size = 100, last_modified = DateTime.UtcNow };
        _samba_client.Setup(s => s.UploadFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>()))
            .ReturnsAsync(file_info);
        var user_id = Guid.NewGuid().ToString();
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("test content"));
        var result = await _service.UploadFileAsync(user_id, drive.drive_id.ToString(), "/", "test.txt", stream, 100, "127.0.0.1");
        Assert.True(result.success);
        Assert.NotNull(result.file);
        Assert.Equal("test.txt", result.file!.name);
        _audit_svc.Verify(a => a.LogAsync(It.Is<AuditLogEntry>(e => e.action == AuditAction.FileUpload)), Times.Once);
    }

    [Fact]
    public async Task UploadFileAsync_ExeFile_ReturnsBlockedFileType()
    {
        var user_id = Guid.NewGuid().ToString();
        var drive_id = Guid.NewGuid().ToString();
        using var stream = new MemoryStream();
        var result = await _service.UploadFileAsync(user_id, drive_id, "/", "malware.exe", stream, 100, "127.0.0.1");
        Assert.False(result.success);
        Assert.Equal("BLOCKED_FILE_TYPE", result.error_code);
    }

    [Fact]
    public async Task ListFilesAsync_PathTraversal_Blocked()
    {
        var user_id = Guid.NewGuid().ToString();
        var role_id = Guid.NewGuid();
        var drive = CreateTestDrive(role_id);

        _drive_repo.Setup(r => r.GetByIdAsync(drive.drive_id)).ReturnsAsync(drive);
        _perm_svc.Setup(p => p.GetUserRolesAsync(user_id))
            .ReturnsAsync(new List<RoleDto> { new() { role_id = role_id.ToString() } });
        _drive_repo.Setup(r => r.GetByRoleIdsAsync(It.IsAny<List<Guid>>()))
            .ReturnsAsync(new List<SharedDrive> { drive });
        _samba_client.Setup(s => s.ListDirectoryAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<SambaFileInfo>());

        try
        {
            await _service.ListFilesAsync(user_id, drive.drive_id.ToString(), "../etc/passwd");
            Assert.True(false, "Expected exception was not thrown");
        }
        catch (InvalidOperationException)
        {
            Assert.True(true);
        }
    }

    [Fact]
    public async Task UploadFileAsync_FileTooLarge_ReturnsFileTooLarge()
    {
        var user_id = Guid.NewGuid().ToString();
        var drive_id = Guid.NewGuid().ToString();
        var large_size = 201 * 1024 * 1024;
        using var stream = new MemoryStream();
        var result = await _service.UploadFileAsync(user_id, drive_id, "/", "big.txt", stream, large_size, "127.0.0.1");
        Assert.False(result.success);
        Assert.Equal("FILE_TOO_LARGE", result.error_code);
    }

    [Fact]
    public async Task UploadFileAsync_NoWritePermission_ReturnsAccessDenied()
    {
        var role = CreateTestRoleDto();
        var drive = CreateTestDrive(Guid.Parse(role.role_id), AccessMode.ReadOnly);
        _drive_repo.Setup(r => r.GetByIdAsync(drive.drive_id)).ReturnsAsync(drive);
        SetupDriveWriteAccess(drive, role);
        var user_id = Guid.NewGuid().ToString();
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("test"));
        var result = await _service.UploadFileAsync(user_id, drive.drive_id.ToString(), "/", "test.txt", stream, 10, "127.0.0.1");
        Assert.False(result.success);
        Assert.Equal("ACCESS_DENIED", result.error_code);
    }
    [Fact]
    public async Task DownloadFileAsync_NormalDownload_ReturnsSuccess()
    {
        var role = CreateTestRoleDto();
        var drive = CreateTestDrive(Guid.Parse(role.role_id));
        _drive_repo.Setup(r => r.GetByIdAsync(drive.drive_id)).ReturnsAsync(drive);
        SetupDriveAccess(drive, role);
        var file_stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("file content"));
        _samba_client.Setup(s => s.DownloadFileAsync(It.IsAny<string>())).ReturnsAsync(file_stream);
        var user_id = Guid.NewGuid().ToString();
        var result = await _service.DownloadFileAsync(user_id, drive.drive_id.ToString(), "/test.txt");
        Assert.True(result.success);
        Assert.Equal("test.txt", result.file_name);
        Assert.NotNull(result.content_stream);
    }

    [Fact]
    public async Task DownloadFileAsync_NoReadPermission_ReturnsAccessDenied()
    {
        var drive = CreateTestDrive();
        _drive_repo.Setup(r => r.GetByIdAsync(drive.drive_id)).ReturnsAsync(drive);
        SetupNoDriveAccess();
        var user_id = Guid.NewGuid().ToString();
        var result = await _service.DownloadFileAsync(user_id, drive.drive_id.ToString(), "/test.txt");
        Assert.False(result.success);
        Assert.Equal("ACCESS_DENIED", result.error_code);
    }

    [Fact]
    public async Task PreviewFileAsync_NormalPreview_ReturnsSuccess()
    {
        var role = CreateTestRoleDto();
        var drive = CreateTestDrive(Guid.Parse(role.role_id));
        _drive_repo.Setup(r => r.GetByIdAsync(drive.drive_id)).ReturnsAsync(drive);
        SetupDriveAccess(drive, role);
        var preview_result = FilePreviewResult.Text("preview text");
        _preview_svc.Setup(p => p.PreviewAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(preview_result);
        var user_id = Guid.NewGuid().ToString();
        var result = await _service.PreviewFileAsync(user_id, drive.drive_id.ToString(), "/test.txt");
        Assert.True(result.success);
        Assert.Equal("text", result.preview_type);
    }

    [Fact]
    public async Task DeleteFileAsync_NormalDelete_ReturnsSuccessAndLogsAudit()
    {
        var role = CreateTestRoleDto();
        var drive = CreateTestDrive(Guid.Parse(role.role_id));
        _drive_repo.Setup(r => r.GetByIdAsync(drive.drive_id)).ReturnsAsync(drive);
        SetupDriveWriteAccess(drive, role);
        _samba_client.Setup(s => s.DeleteFileAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _audit_svc.Setup(a => a.LogAsync(It.IsAny<AuditLogEntry>())).Returns(Task.CompletedTask);
        var user_id = Guid.NewGuid().ToString();
        var result = await _service.DeleteFileAsync(user_id, drive.drive_id.ToString(), "/test.txt", "127.0.0.1");
        Assert.True(result.success);
        _samba_client.Verify(s => s.DeleteFileAsync(It.IsAny<string>()), Times.Once);
        _audit_svc.Verify(a => a.LogAsync(It.Is<AuditLogEntry>(e => e.action == AuditAction.FileDelete)), Times.Once);
    }

    [Fact]
    public async Task DeleteFileAsync_NoWritePermission_ReturnsAccessDenied()
    {
        var role = CreateTestRoleDto();
        var drive = CreateTestDrive(Guid.Parse(role.role_id), AccessMode.ReadOnly);
        _drive_repo.Setup(r => r.GetByIdAsync(drive.drive_id)).ReturnsAsync(drive);
        SetupDriveWriteAccess(drive, role);
        var user_id = Guid.NewGuid().ToString();
        var result = await _service.DeleteFileAsync(user_id, drive.drive_id.ToString(), "/test.txt", "127.0.0.1");
        Assert.False(result.success);
        Assert.Equal("ACCESS_DENIED", result.error_code);
    }
    [Fact]
    public async Task ListFilesAsync_NormalList_ReturnsSuccess()
    {
        var role = CreateTestRoleDto();
        var drive = CreateTestDrive(Guid.Parse(role.role_id));
        _drive_repo.Setup(r => r.GetByIdAsync(drive.drive_id)).ReturnsAsync(drive);
        SetupDriveAccess(drive, role);
        var items = new List<SambaFileInfo>
        {
            new() { name = "file1.txt", path = "test/file1.txt", is_directory = false, size = 100, last_modified = DateTime.UtcNow },
            new() { name = "folder1", path = "test/folder1", is_directory = true, size = 0, last_modified = DateTime.UtcNow }
        };
        _samba_client.Setup(s => s.ListDirectoryAsync(It.IsAny<string>())).ReturnsAsync(items);
        var user_id = Guid.NewGuid().ToString();
        var result = await _service.ListFilesAsync(user_id, drive.drive_id.ToString(), "/");
        Assert.True(result.success);
        Assert.Equal(2, result.files.Count);
    }

    [Fact]
    public async Task ListFilesAsync_NoAccessPermission_ReturnsAccessDenied()
    {
        var drive = CreateTestDrive();
        _drive_repo.Setup(r => r.GetByIdAsync(drive.drive_id)).ReturnsAsync(drive);
        SetupNoDriveAccess();
        var user_id = Guid.NewGuid().ToString();
        var result = await _service.ListFilesAsync(user_id, drive.drive_id.ToString(), "/");
        Assert.False(result.success);
        Assert.Equal("ACCESS_DENIED", result.error_code);
    }

}
