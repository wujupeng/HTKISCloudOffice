using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Application.Services;
using HTKISCloudOffice.Domain.Entities;
using HTKISCloudOffice.Domain.Enums;
using HTKISCloudOffice.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;

namespace HTKISCloudOffice.UnitTests;

public class PermissionServiceTests
{
    private readonly Mock<IRoleRepository> _role_repo;
    private readonly Mock<IUserRepository> _user_repo;
    private readonly Mock<ICacheProvider> _cache;
    private readonly Mock<IAuditService> _audit_svc;
    private readonly Mock<ILogger<PermissionService>> _logger;
    private readonly PermissionService _service;

    public PermissionServiceTests()
    {
        _role_repo = new Mock<IRoleRepository>();
        _user_repo = new Mock<IUserRepository>();
        _cache = new Mock<ICacheProvider>();
        _audit_svc = new Mock<IAuditService>();
        _logger = new Mock<ILogger<PermissionService>>();
        _service = new PermissionService(_role_repo.Object, _user_repo.Object,
            _cache.Object, _audit_svc.Object, _logger.Object);
    }

    [Fact]
    public async Task GetUserRolesAsync_ReturnsCachedRoles_WhenCacheHit()
    {
        var user_id = Guid.NewGuid().ToString();
        var cached_roles = new List<RoleDto>
        {
            new() { role_id = Guid.NewGuid().ToString(), role_name = "all_staff" }
        };

        _cache.Setup(c => c.GetAsync<List<RoleDto>>($"perm:user:{user_id}"))
            .ReturnsAsync(cached_roles);

        var result = await _service.GetUserRolesAsync(user_id);

        Assert.Single(result);
        Assert.Equal("all_staff", result[0].role_name);
        _role_repo.Verify(r => r.GetByUserIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GetUserRolesAsync_FetchesFromRepo_WhenCacheMiss()
    {
        var user_id = Guid.NewGuid();
        var role = new Role
        {
            role_id = Guid.NewGuid(),
            role_name = "all_staff",
            permissions = new List<PermissionEntry>()
        };

        _cache.Setup(c => c.GetAsync<List<RoleDto>>($"perm:user:{user_id}"))
            .ReturnsAsync((List<RoleDto>?)null);
        _role_repo.Setup(r => r.GetByUserIdAsync(user_id))
            .ReturnsAsync(new List<Role> { role });

        var result = await _service.GetUserRolesAsync(user_id.ToString());

        Assert.Single(result);
        Assert.Equal("all_staff", result[0].role_name);
        _cache.Verify(c => c.SetAsync($"perm:user:{user_id}", It.IsAny<List<RoleDto>>(), It.IsAny<TimeSpan?>()), Times.Once);
    }

    [Fact]
    public async Task CheckAppAccessAsync_OnException_ReturnsFalse()
    {
        var user_id = Guid.NewGuid().ToString();
        _cache.Setup(c => c.GetAsync<List<RoleDto>>($"perm:user:{user_id}"))
            .ThrowsAsync(new Exception("Cache error"));

        var result = await _service.CheckAppAccessAsync(user_id, Guid.NewGuid().ToString());

        Assert.False(result);
    }

    [Fact]
    public async Task CheckDriveAccessAsync_WithMatchingPermission_ReturnsAccessMode()
    {
        var user_id = Guid.NewGuid().ToString();
        var drive_id = Guid.NewGuid().ToString();
        var roles = new List<RoleDto>
        {
            new()
            {
                role_id = Guid.NewGuid().ToString(),
                permissions = new List<PermissionEntryDto>
                {
                    new()
                    {
                        resource_type = ResourceType.SharedDrive,
                        resource_id = drive_id,
                        access_mode = AccessMode.ReadWrite
                    }
                }
            }
        };

        _cache.Setup(c => c.GetAsync<List<RoleDto>>($"perm:user:{user_id}"))
            .ReturnsAsync(roles);

        var result = await _service.CheckDriveAccessAsync(user_id, drive_id);

        Assert.Equal(AccessMode.ReadWrite, result);
    }

    [Fact]
    public async Task CheckDriveAccessAsync_WithNoPermission_ReturnsNull()
    {
        var user_id = Guid.NewGuid().ToString();
        var roles = new List<RoleDto>
        {
            new() { role_id = Guid.NewGuid().ToString(), permissions = new List<PermissionEntryDto>() }
        };

        _cache.Setup(c => c.GetAsync<List<RoleDto>>($"perm:user:{user_id}"))
            .ReturnsAsync(roles);

        var result = await _service.CheckDriveAccessAsync(user_id, Guid.NewGuid().ToString());

        Assert.Null(result);
    }

    [Fact]
    public async Task AssignRolesAsync_LogsAuditAndInvalidatesCache()
    {
        var user_id = Guid.NewGuid();
        var role_ids = new List<string> { Guid.NewGuid().ToString() };
        var operator_id = Guid.NewGuid().ToString();

        _user_repo.Setup(r => r.GetByIdAsync(user_id)).ReturnsAsync(new User());

        await _service.AssignRolesAsync(user_id.ToString(), role_ids, operator_id);

        _audit_svc.Verify(a => a.LogPermissionChangeAsync(operator_id, user_id.ToString(),
            It.IsAny<string>(), ""), Times.Once);
        _cache.Verify(c => c.RemoveAsync($"perm:user:{user_id}"), Times.Once);
    }

    [Fact]
    public async Task RevokeRolesAsync_PreventsRevokingLastSuperAdmin()
    {
        var user_id = Guid.NewGuid();
        var super_admin_role = new Role
        {
            role_id = Guid.NewGuid(),
            role_name = "super_admin"
        };
        var user = new User
        {
            user_id = user_id,
            user_roles = new List<UserRole>
            {
                new() { Role = super_admin_role }
            }
        };

        _role_repo.Setup(r => r.GetByUserIdAsync(user_id))
            .ReturnsAsync(new List<Role> { super_admin_role });
        _user_repo.Setup(r => r.ListAsync(1, 1000, It.IsAny<string?>(), It.IsAny<bool?>()))
            .ReturnsAsync((new List<User> { user }, 1));

        await _service.RevokeRolesAsync(user_id.ToString(),
            new List<string> { super_admin_role.role_id.ToString() }, Guid.NewGuid().ToString());

        _audit_svc.Verify(a => a.LogPermissionChangeAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetAllRolesAsync_ReturnsAllRoles()
    {
        var role = new Role
        {
            role_id = Guid.NewGuid(),
            role_name = "all_staff",
            permissions = new List<PermissionEntry>()
        };

        _role_repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Role> { role });

        var result = await _service.GetAllRolesAsync();

        Assert.Single(result);
        Assert.Equal("all_staff", result[0].role_name);
    }

    [Fact]
    public async Task CreateRoleAsync_CreatesRoleAndLogsAudit()
    {
        var request = new CreateRoleRequest
        {
            role_name = "finance",
            description = "Finance department",
            permissions = new List<PermissionEntryDto>()
        };
        var operator_id = Guid.NewGuid().ToString();

        _role_repo.Setup(r => r.CreateAsync(It.IsAny<Role>()))
            .ReturnsAsync((Role r) => r);

        var result = await _service.CreateRoleAsync(request, operator_id);

        Assert.Equal("finance", result.role_name);
        _audit_svc.Verify(a => a.LogPermissionChangeAsync(operator_id, "",
            It.IsAny<string>(), ""), Times.Once);
    }

    [Fact]
    public async Task InvalidateUserPermissionCacheAsync_RemovesCache()
    {
        var user_id = Guid.NewGuid().ToString();

        await _service.InvalidateUserPermissionCacheAsync(user_id);

        _cache.Verify(c => c.RemoveAsync($"perm:user:{user_id}"), Times.Once);
    }
}