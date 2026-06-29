using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Application.Services;
using HTKISCloudOffice.Domain.Entities;
using HTKISCloudOffice.Domain.Enums;
using Moq;

namespace HTKISCloudOffice.UnitTests;

public class AppPortalServiceTests
{
    private readonly Mock<IApplicationRepository> _app_repo;
    private readonly Mock<IPermissionService> _perm_svc;
    private readonly Mock<IDesktopService> _desktop_svc;
    private readonly Mock<IAuditService> _audit_svc;
    private readonly AppPortalService _service;

    public AppPortalServiceTests()
    {
        _app_repo = new Mock<IApplicationRepository>();
        _perm_svc = new Mock<IPermissionService>();
        _desktop_svc = new Mock<IDesktopService>();
        _audit_svc = new Mock<IAuditService>();
        _service = new AppPortalService(_app_repo.Object, _perm_svc.Object,
            _desktop_svc.Object, _audit_svc.Object);
    }

    private static Domain.Entities.Application CreateTestApp(AppType type = AppType.CloudDesktop)
    {
        return new Domain.Entities.Application
        {
            app_id = Guid.NewGuid(),
            app_name = "WPS Office",
            app_type = type,
            icon_url = "/icons/wps.png",
            category = AppCategory.Office,
            launch_params = "https://wps.example.com"
        };
    }

    [Fact]
    public async Task GetApplicationsForUserAsync_ReturnsMappedDtos()
    {
        var user_id = Guid.NewGuid().ToString();
        var role_id = Guid.NewGuid();
        var app = CreateTestApp();

        _perm_svc.Setup(p => p.GetUserRolesAsync(user_id))
            .ReturnsAsync(new List<RoleDto> { new() { role_id = role_id.ToString(), role_name = "all_staff" } });
        _app_repo.Setup(r => r.GetByRoleIdsAsync(It.IsAny<List<Guid>>()))
            .ReturnsAsync(new List<Domain.Entities.Application> { app });

        var result = await _service.GetApplicationsForUserAsync(user_id);

        Assert.Single(result);
        Assert.Equal(app.app_id.ToString(), result[0].app_id);
        Assert.Equal("WPS Office", result[0].app_name);
    }

    [Fact]
    public async Task GetApplicationByIdAsync_WithExistingApp_ReturnsDto()
    {
        var app = CreateTestApp();
        _app_repo.Setup(r => r.GetByIdAsync(app.app_id)).ReturnsAsync(app);

        var result = await _service.GetApplicationByIdAsync(app.app_id.ToString());

        Assert.NotNull(result);
        Assert.Equal(app.app_name, result.app_name);
    }

    [Fact]
    public async Task GetApplicationByIdAsync_WithNonExistentApp_ReturnsNull()
    {
        var app_id = Guid.NewGuid();
        _app_repo.Setup(r => r.GetByIdAsync(app_id)).ReturnsAsync((Domain.Entities.Application?)null);

        var result = await _service.GetApplicationByIdAsync(app_id.ToString());

        Assert.Null(result);
    }

    [Fact]
    public async Task LaunchApplicationAsync_WithoutAccess_ReturnsAccessDenied()
    {
        var user_id = Guid.NewGuid().ToString();
        var app_id = Guid.NewGuid().ToString();
        _perm_svc.Setup(p => p.CheckAppAccessAsync(user_id, app_id)).ReturnsAsync(false);

        var result = await _service.LaunchApplicationAsync(user_id, app_id);

        Assert.False(result.success);
        Assert.Equal("ACCESS_DENIED", result.error_code);
    }

    [Fact]
    public async Task LaunchApplicationAsync_WithWebLink_ReturnsUrl()
    {
        var user_id = Guid.NewGuid().ToString();
        var app = CreateTestApp(AppType.WebLink);
        app.launch_params = "https://wps.example.com";

        _perm_svc.Setup(p => p.CheckAppAccessAsync(user_id, app.app_id.ToString())).ReturnsAsync(true);
        _app_repo.Setup(r => r.GetByIdAsync(app.app_id)).ReturnsAsync(app);

        var result = await _service.LaunchApplicationAsync(user_id, app.app_id.ToString());

        Assert.True(result.success);
        Assert.Equal("https://wps.example.com", result.guacamole_url);
    }

    [Fact]
    public async Task LaunchApplicationAsync_WithCloudDesktop_CallsDesktopService()
    {
        var user_id = Guid.NewGuid().ToString();
        var app = CreateTestApp(AppType.CloudDesktop);

        _perm_svc.Setup(p => p.CheckAppAccessAsync(user_id, app.app_id.ToString())).ReturnsAsync(true);
        _app_repo.Setup(r => r.GetByIdAsync(app.app_id)).ReturnsAsync(app);
        _desktop_svc.Setup(d => d.ConnectAsync(user_id, app.app_id.ToString()))
            .ReturnsAsync(new DesktopConnectionResult
            {
                success = true,
                connection_id = "conn_123",
                guacamole_url = "/guacamole/#/client/conn_123"
            });

        var result = await _service.LaunchApplicationAsync(user_id, app.app_id.ToString());

        Assert.True(result.success);
        Assert.Equal("conn_123", result.connection_id);
    }

    [Fact]
    public async Task LaunchApplicationAsync_WithNonExistentApp_ReturnsNotFound()
    {
        var user_id = Guid.NewGuid().ToString();
        var app_id = Guid.NewGuid();

        _perm_svc.Setup(p => p.CheckAppAccessAsync(user_id, app_id.ToString())).ReturnsAsync(true);
        _app_repo.Setup(r => r.GetByIdAsync(app_id)).ReturnsAsync((Domain.Entities.Application?)null);

        var result = await _service.LaunchApplicationAsync(user_id, app_id.ToString());

        Assert.False(result.success);
        Assert.Equal("APP_NOT_FOUND", result.error_code);
    }

    [Fact]
    public async Task GetApplicationsByCategoryAsync_ReturnsFilteredApps()
    {
        var user_id = Guid.NewGuid().ToString();
        var role_id = Guid.NewGuid();
        var app = CreateTestApp();

        _perm_svc.Setup(p => p.GetUserRolesAsync(user_id))
            .ReturnsAsync(new List<RoleDto> { new() { role_id = role_id.ToString() } });
        _app_repo.Setup(r => r.GetByCategoryAsync(AppCategory.Office, It.IsAny<List<Guid>>()))
            .ReturnsAsync(new List<Domain.Entities.Application> { app });

        var result = await _service.GetApplicationsByCategoryAsync(user_id, AppCategory.Office);

        Assert.Single(result);
        Assert.Equal(AppCategory.Office, result[0].category);
    }
}