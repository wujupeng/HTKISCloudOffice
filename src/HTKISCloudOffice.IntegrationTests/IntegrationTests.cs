using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Domain.Entities;
using HTKISCloudOffice.Domain.Enums;
using HTKISCloudOffice.Domain.ValueObjects;
using HTKISCloudOffice.Infrastructure.Data;
using HTKISCloudOffice.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DriveType = HTKISCloudOffice.Domain.Enums.DriveType;

namespace HTKISCloudOffice.IntegrationTests;

public class RepositoryIntegrationTests
{
    private readonly IServiceProvider _services;

    public RepositoryIntegrationTests()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<IVmConfigRepository, VmConfigRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<ISharedDriveRepository, SharedDriveRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        _services = services.BuildServiceProvider();
    }

    private AppDbContext GetDb() => _services.GetRequiredService<AppDbContext>();

    [Fact]
    public async Task UserRepository_CreateAndGetByUsername_Works()
    {
        var repo = new UserRepository(GetDb());
        var user = new User
        {
            username = "testuser",
            password_hash = "hash123",
            display_name = "Test User",
            department = "IT"
        };

        var created = await repo.CreateAsync(user);
        var found = await repo.GetByUsernameAsync("testuser");

        Assert.NotNull(found);
        Assert.Equal("testuser", found.username);
        Assert.Equal("Test User", found.display_name);
    }

    [Fact]
    public async Task UserRepository_GetByUsername_ReturnsNullForNonExistent()
    {
        var repo = new UserRepository(GetDb());
        var result = await repo.GetByUsernameAsync("nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public async Task RoleRepository_CreateAndGetAll_Works()
    {
        var repo = new RoleRepository(GetDb());
        var role = new Role
        {
            role_name = "all_staff",
            description = "All Staff",
            permissions = new List<PermissionEntry>()
        };

        await repo.CreateAsync(role);
        var all = await repo.GetAllAsync();

        Assert.Single(all);
        Assert.Equal("all_staff", all[0].role_name);
    }

    [Fact]
    public async Task ApplicationRepository_CreateAndGetById_Works()
    {
        var repo = new ApplicationRepository(GetDb());
        var app = new Domain.Entities.Application
        {
            app_name = "WPS Office",
            app_type = AppType.CloudDesktop,
            icon_url = "/icons/wps.png",
            category = AppCategory.Office,
            launch_params = "{}"
        };

        var created = await repo.CreateAsync(app);
        var found = await repo.GetByIdAsync(created.app_id);

        Assert.NotNull(found);
        Assert.Equal("WPS Office", found.app_name);
    }

    [Fact]
    public async Task VmConfigRepository_CreateAndGetByUserId_Works()
    {
        var db = GetDb();
        var repo = new VmConfigRepository(db);

        var user = new User
        {
            username = "vmuser",
            password_hash = "hash",
            display_name = "VM User",
            bound_vm_id = "vm_001"
        };
        db.users.Add(user);

        var vm = new VmConfig
        {
            vm_id = "vm_001",
            vm_name = "VM-01",
            hostname = "192.168.1.100",
            rdp_port = 3389,
            rdp_username = "admin",
            rdp_password_encrypted = "encrypted"
        };
        await repo.CreateAsync(vm);
        await db.SaveChangesAsync();

        var found = await repo.GetByUserIdAsync(user.user_id);
        Assert.NotNull(found);
        Assert.Equal("vm_001", found.vm_id);
    }

    [Fact]
    public async Task AuditLogRepository_LogAndQuery_Works()
    {
        var repo = new AuditLogRepository(GetDb());
        var log = new AuditLog
        {
            user_id = Guid.NewGuid(),
            action = AuditAction.Login,
            resource_type = "user",
            resource_id = Guid.NewGuid(),
            ip_address = "127.0.0.1"
        };

        await repo.LogAsync(log);
        var (items, total) = await repo.QueryAsync(action: AuditAction.Login);

        Assert.Equal(1, total);
        Assert.Single(items);
        Assert.Equal(AuditAction.Login, items[0].action);
    }

    [Fact]
    public async Task SharedDriveRepository_CreateAndGetById_Works()
    {
        var repo = new SharedDriveRepository(GetDb());
        var drive = new SharedDrive
        {
            drive_name = "公共盘",
            drive_type = DriveType.Public,
            samba_path = "/data/shares/public",
            drive_letter = "Z:",
            allowed_permissions = new List<DrivePermission>()
        };

        var created = await repo.CreateAsync(drive);
        var found = await repo.GetByIdAsync(created.drive_id);

        Assert.NotNull(found);
        Assert.Equal("公共盘", found.drive_name);
        Assert.Equal("Z:", found.drive_letter);
    }

    [Fact]
    public async Task RefreshTokenRepository_CreateAndRevoke_Works()
    {
        var db = GetDb();
        var user = new User
        {
            username = "tokenuser",
            password_hash = "hash",
            display_name = "Token User"
        };
        db.users.Add(user);
        await db.SaveChangesAsync();

        var repo = new RefreshTokenRepository(db);
        var token = new RefreshToken
        {
            user_id = user.user_id,
            token_hash = "hash_" + Guid.NewGuid(),
            expires_at = DateTime.UtcNow.AddDays(7)
        };

        await repo.CreateAsync(token);
        await repo.RevokeAllForUserAsync(user.user_id);

        var found = await repo.GetByTokenHashAsync(token.token_hash);
        Assert.True(found?.is_revoked == true, "Token should be revoked");
    }
}
