using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Application.Services;
using HTKISCloudOffice.Domain.Entities;
using HTKISCloudOffice.Domain.Enums;
using HTKISCloudOffice.Domain.ValueObjects;
using Moq;
using DriveType = HTKISCloudOffice.Domain.Enums.DriveType;

namespace HTKISCloudOffice.UnitTests;

public class FileShareServiceTests
{
    private readonly Mock<ISharedDriveRepository> _drive_repo;
    private readonly Mock<IPermissionService> _perm_svc;
    private readonly Mock<ISambaConfigManager> _samba_mgr;
    private readonly Mock<IAuditService> _audit_svc;
    private readonly FileShareService _service;

    public FileShareServiceTests()
    {
        _drive_repo = new Mock<ISharedDriveRepository>();
        _perm_svc = new Mock<IPermissionService>();
        _samba_mgr = new Mock<ISambaConfigManager>();
        _audit_svc = new Mock<IAuditService>();
        _service = new FileShareService(_drive_repo.Object, _perm_svc.Object,
            _samba_mgr.Object, _audit_svc.Object);
    }

    private static SharedDrive CreateTestDrive()
    {
        var role_id = Guid.NewGuid();
        return new SharedDrive
        {
            drive_id = Guid.NewGuid(),
            drive_name = "公共盘",
            drive_type = DriveType.Public,
            samba_path = "/data/shares/public",
            drive_letter = "Z:",
            allowed_permissions = new List<DrivePermission>
            {
                new() { role_id = role_id.ToString(), access_mode = AccessMode.ReadWrite }
            },
            quota_mb = 10240
        };
    }

    [Fact]
    public async Task GetSharedDrivesForUserAsync_ReturnsMappedDtos()
    {
        var user_id = Guid.NewGuid().ToString();
        var role_id = Guid.NewGuid();
        var drive = CreateTestDrive();
        drive.allowed_permissions[0].role_id = role_id.ToString();

        _perm_svc.Setup(p => p.GetUserRolesAsync(user_id))
            .ReturnsAsync(new List<RoleDto> { new() { role_id = role_id.ToString() } });
        _drive_repo.Setup(r => r.GetByRoleIdsAsync(It.IsAny<List<Guid>>()))
            .ReturnsAsync(new List<SharedDrive> { drive });

        var result = await _service.GetSharedDrivesForUserAsync(user_id);

        Assert.Single(result);
        Assert.Equal("公共盘", result[0].drive_name);
        Assert.Equal("Z:", result[0].drive_letter);
        Assert.Contains("samba.htkis.local", result[0].samba_path);
    }

    [Fact]
    public async Task GetDriveMappingAsync_ReturnsMappings()
    {
        var user_id = Guid.NewGuid().ToString();
        var role_id = Guid.NewGuid();
        var drive = CreateTestDrive();
        drive.allowed_permissions[0].role_id = role_id.ToString();

        _perm_svc.Setup(p => p.GetUserRolesAsync(user_id))
            .ReturnsAsync(new List<RoleDto> { new() { role_id = role_id.ToString() } });
        _drive_repo.Setup(r => r.GetByRoleIdsAsync(It.IsAny<List<Guid>>()))
            .ReturnsAsync(new List<SharedDrive> { drive });

        var result = await _service.GetDriveMappingAsync(user_id);

        Assert.Single(result.mappings);
        Assert.Equal("Z:", result.mappings[0].drive_letter);
    }

    [Fact]
    public async Task CreateSharedDriveAsync_CreatesDriveAndSambaShare()
    {
        var role_id = Guid.NewGuid();
        var request = new CreateSharedDriveRequest
        {
            drive_name = "财务盘",
            drive_type = DriveType.Department,
            samba_path = "/data/shares/finance",
            drive_letter = "Y:",
            permissions = new List<DrivePermissionDto>
            {
                new() { role_id = role_id.ToString(), access_mode = AccessMode.ReadWrite }
            },
            quota_mb = 5120
        };

        _drive_repo.Setup(r => r.CreateAsync(It.IsAny<SharedDrive>()))
            .ReturnsAsync((SharedDrive d) => d);
        _samba_mgr.Setup(s => s.CreateShareAsync("finance", "/data/shares/finance",
            It.IsAny<List<string>>())).Returns(Task.CompletedTask);

        var result = await _service.CreateSharedDriveAsync(request);

        Assert.Equal("财务盘", result.drive_name);
        Assert.Equal("Y:", result.drive_letter);
        _drive_repo.Verify(r => r.CreateAsync(It.IsAny<SharedDrive>()), Times.Once);
        _samba_mgr.Verify(s => s.CreateShareAsync("finance", "/data/shares/finance",
            It.IsAny<List<string>>()), Times.Once);
    }

    [Fact]
    public async Task UpdateDrivePermissionsAsync_UpdatesDriveAndSamba()
    {
        var drive = CreateTestDrive();
        var new_perms = new List<DrivePermissionDto>
        {
            new() { role_id = Guid.NewGuid().ToString(), access_mode = AccessMode.ReadOnly }
        };

        _drive_repo.Setup(r => r.GetByIdAsync(drive.drive_id)).ReturnsAsync(drive);
        _drive_repo.Setup(r => r.UpdateAsync(It.IsAny<SharedDrive>())).Returns(Task.CompletedTask);
        _samba_mgr.Setup(s => s.UpdateSharePermissionsAsync("public", It.IsAny<List<string>>()))
            .Returns(Task.CompletedTask);

        await _service.UpdateDrivePermissionsAsync(drive.drive_id.ToString(), new_perms);

        _drive_repo.Verify(r => r.UpdateAsync(It.IsAny<SharedDrive>()), Times.Once);
        _samba_mgr.Verify(s => s.UpdateSharePermissionsAsync("public", It.IsAny<List<string>>()), Times.Once);
    }

    [Fact]
    public async Task UpdateDrivePermissionsAsync_WithNonExistentDrive_DoesNothing()
    {
        var drive_id = Guid.NewGuid();
        _drive_repo.Setup(r => r.GetByIdAsync(drive_id)).ReturnsAsync((SharedDrive?)null);

        await _service.UpdateDrivePermissionsAsync(drive_id.ToString(), new List<DrivePermissionDto>());

        _drive_repo.Verify(r => r.UpdateAsync(It.IsAny<SharedDrive>()), Times.Never);
        _samba_mgr.Verify(s => s.UpdateSharePermissionsAsync(It.IsAny<string>(), It.IsAny<List<string>>()), Times.Never);
    }
}