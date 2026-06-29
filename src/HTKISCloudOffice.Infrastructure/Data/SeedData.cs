using AppEntity = HTKISCloudOffice.Domain.Entities.Application;
using HTKISCloudOffice.Domain.Entities;
using DriveType = HTKISCloudOffice.Domain.Enums.DriveType;
using HTKISCloudOffice.Domain.Enums;
using HTKISCloudOffice.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace HTKISCloudOffice.Infrastructure.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (context.Database.IsRelational())
        {
            await context.Database.MigrateAsync();
        }

        if (await context.roles.AnyAsync()) return;

        var superAdminRole = new Role
        {
            role_id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            role_name = "super_admin",
            description = "超级管理员",
            permissions = new List<PermissionEntry>()
        };

        var allStaffRole = new Role
        {
            role_id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
            role_name = "all_staff",
            description = "全体员工",
            permissions = new List<PermissionEntry>
            {
                new() { resource_type = ResourceType.Application, resource_id = Guid.Empty.ToString(), access_mode = AccessMode.ReadOnly }
            }
        };

        await context.roles.AddRangeAsync(superAdminRole, allStaffRole);

        var adminUser = new User
        {
            user_id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            username = "admin",
            password_hash = HashPassword("Admin@2026"),
            display_name = "系统管理员",
            department = "IT",
            is_active = true
        };

        await context.users.AddAsync(adminUser);

        await context.user_roles.AddAsync(new UserRole
        {
            user_id = adminUser.user_id,
            role_id = superAdminRole.role_id,
            assigned_at = DateTime.UtcNow
        });

        await context.user_roles.AddAsync(new UserRole
        {
            user_id = adminUser.user_id,
            role_id = allStaffRole.role_id,
            assigned_at = DateTime.UtcNow
        });

        var wpsApp = new AppEntity
        {
            app_id = Guid.Parse("00000000-0000-0000-0000-000000000010"),
            app_name = "WPS办公",
            app_type = AppType.CloudDesktop,
            icon_url = "/icons/wps.png",
            category = AppCategory.Office,
            launch_params = """{"vm_id":"vm-win11-001","app_direct_launch":true,"app_program_path":"C:\\Program Files\\WPS Office\\wps.exe","resolution":"1920x1080","color_depth":32}""",
            sort_order = 1
        };

        var fileCenterApp = new AppEntity
        {
            app_id = Guid.Parse("00000000-0000-0000-0000-000000000011"),
            app_name = "文件中心",
            app_type = AppType.FileManager,
            icon_url = "/icons/file-center.png",
            category = AppCategory.File,
            launch_params = """{}""",
            sort_order = 2
        };

        var erpApp = new AppEntity
        {
            app_id = Guid.Parse("00000000-0000-0000-0000-000000000012"),
            app_name = "ERP系统",
            app_type = AppType.WebLink,
            icon_url = "/icons/erp.png",
            category = AppCategory.Business,
            launch_params = """{"url":"https://erp.htkis.local","open_in_new_tab":true}""",
            sort_order = 3
        };

        var mesApp = new AppEntity
        {
            app_id = Guid.Parse("00000000-0000-0000-0000-000000000013"),
            app_name = "MES系统",
            app_type = AppType.WebLink,
            icon_url = "/icons/mes.png",
            category = AppCategory.Business,
            launch_params = """{"url":"https://mes.htkis.local","open_in_new_tab":true}""",
            sort_order = 4
        };

        await context.applications.AddRangeAsync(wpsApp, fileCenterApp, erpApp, mesApp);

        foreach (var app in new[] { wpsApp, fileCenterApp, erpApp, mesApp })
        {
            await context.app_allowed_roles.AddAsync(new AppAllowedRole
            {
                app_id = app.app_id,
                role_id = allStaffRole.role_id
            });
        }

        var publicDrive = new SharedDrive
        {
            drive_id = Guid.Parse("00000000-0000-0000-0000-000000000020"),
            drive_name = "公共盘",
            drive_type = DriveType.Public,
            samba_path = "/data/shares/public",
            drive_letter = "Z",
            allowed_permissions = new List<DrivePermission>
            {
                new() { role_id = allStaffRole.role_id.ToString(), access_mode = AccessMode.ReadWrite }
            },
            quota_mb = 0
        };

        await context.shared_drives.AddAsync(publicDrive);

        var defaultVm = new VmConfig
        {
            vm_id = "vm-win11-001",
            vm_name = "Win11-001",
            hostname = "192.168.1.100",
            rdp_port = 3389,
            rdp_username = "htkis_user",
            rdp_password_encrypted = "",
            max_users = 1,
            is_active = true
        };

        await context.vm_configs.AddAsync(defaultVm);

        var rdpConnection = new ConnectionConfig
        {
            connection_id = Guid.Parse("00000000-0000-0000-0000-000000000030"),
            connection_name = "Win11-001",
            protocol = ConnectionProtocol.RDP,
            hostname = "192.168.1.100",
            port = 3389,
            username = "htkis_user",
            password_encrypted = "",
            connection_params = "{}",
            is_remote_app = false,
            is_active = true,
            sort_order = 1
        };

        var vncConnection = new ConnectionConfig
        {
            connection_id = Guid.Parse("00000000-0000-0000-0000-000000000031"),
            connection_name = "Linux VM",
            protocol = ConnectionProtocol.VNC,
            hostname = "192.168.1.101",
            port = 5900,
            username = "",
            password_encrypted = "",
            connection_params = """{"color_depth":24,"cursor":"default"}""",
            is_remote_app = false,
            is_active = true,
            sort_order = 2
        };

        await context.connection_configs.AddRangeAsync(rdpConnection, vncConnection);

        await context.connection_allowed_roles.AddAsync(new ConnectionAllowedRole
        {
            connection_id = rdpConnection.connection_id,
            role_id = allStaffRole.role_id
        });

        await context.connection_allowed_roles.AddAsync(new ConnectionAllowedRole
        {
            connection_id = vncConnection.connection_id,
            role_id = allStaffRole.role_id
        });

        await context.SaveChangesAsync();
    }

    private static string HashPassword(string password)
    {
        var salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        using var argon2 = new Konscious.Security.Cryptography.Argon2id(System.Text.Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = 4,
            MemorySize = 65536,
            Iterations = 3
        };
        var hash = argon2.GetBytes(32);
        return Convert.ToBase64String(salt.Concat(hash).ToArray());
    }
}