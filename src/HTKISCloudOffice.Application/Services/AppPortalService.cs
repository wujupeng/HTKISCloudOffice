using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Domain.Enums;
using AppEntity = HTKISCloudOffice.Domain.Entities.Application;

namespace HTKISCloudOffice.Application.Services;

public class AppPortalService : IAppPortalService
{
    private readonly IApplicationRepository _app_repo;
    private readonly IPermissionService _perm_svc;
    private readonly IDesktopService _desktop_svc;
    private readonly IAuditService _audit_svc;

    public AppPortalService(
        IApplicationRepository app_repo,
        IPermissionService perm_svc,
        IDesktopService desktop_svc,
        IAuditService audit_svc)
    {
        _app_repo = app_repo;
        _perm_svc = perm_svc;
        _desktop_svc = desktop_svc;
        _audit_svc = audit_svc;
    }

    public async Task<List<ApplicationDto>> GetApplicationsForUserAsync(string user_id)
    {
        var roles = await _perm_svc.GetUserRolesAsync(user_id);
        var role_ids = roles.Select(r => Guid.Parse(r.role_id)).ToList();
        var apps = await _app_repo.GetByRoleIdsAsync(role_ids);

        return apps.Select(a => new ApplicationDto
        {
            app_id = a.app_id.ToString(),
            app_name = a.app_name,
            app_type = a.app_type,
            icon_url = a.icon_url,
            category = a.category,
            description = a.launch_params
        }).ToList();
    }

    public async Task<ApplicationDto?> GetApplicationByIdAsync(string app_id)
    {
        var app = await _app_repo.GetByIdAsync(Guid.Parse(app_id));
        if (app == null) return null;

        return new ApplicationDto
        {
            app_id = app.app_id.ToString(),
            app_name = app.app_name,
            app_type = app.app_type,
            icon_url = app.icon_url,
            category = app.category,
            description = app.launch_params
        };
    }

    public async Task<LaunchResult> LaunchApplicationAsync(string user_id, string app_id)
    {
        var has_access = await _perm_svc.CheckAppAccessAsync(user_id, app_id);
        if (!has_access)
        {
            return LaunchResult.Fail("ACCESS_DENIED", "您没有该应用的访问权限");
        }

        var app = await _app_repo.GetByIdAsync(Guid.Parse(app_id));
        if (app == null)
        {
            return LaunchResult.Fail("APP_NOT_FOUND", "应用不存在");
        }

        await _audit_svc.LogAppLaunchAsync(Guid.Parse(user_id), Guid.Parse(app_id), "");

        return app.app_type switch
        {
            AppType.CloudDesktop => await LaunchDesktopApp(user_id, app),
            AppType.WebLink => LaunchWebLink(app),
            AppType.FileManager => await LaunchDesktopApp(user_id, app),
            _ => LaunchResult.Fail("UNSUPPORTED_APP_TYPE", "不支持的应用类型")
        };
    }

    public async Task<List<ApplicationDto>> GetApplicationsByCategoryAsync(string user_id, AppCategory category)
    {
        var roles = await _perm_svc.GetUserRolesAsync(user_id);
        var role_ids = roles.Select(r => Guid.Parse(r.role_id)).ToList();
        var apps = await _app_repo.GetByCategoryAsync(category, role_ids);

        return apps.Select(a => new ApplicationDto
        {
            app_id = a.app_id.ToString(),
            app_name = a.app_name,
            app_type = a.app_type,
            icon_url = a.icon_url,
            category = a.category,
            description = a.launch_params
        }).ToList();
    }

    private async Task<LaunchResult> LaunchDesktopApp(string user_id, AppEntity app)
    {
        var result = await _desktop_svc.ConnectAsync(user_id, app.app_id.ToString());
        return new LaunchResult
        {
            success = result.success,
            connection_id = result.connection_id,
            guacamole_url = result.guacamole_url,
            error_message = result.error_message
        };
    }

    private static LaunchResult LaunchWebLink(AppEntity app)
    {
        return new LaunchResult
        {
            success = true,
            guacamole_url = app.launch_params
        };
    }
}