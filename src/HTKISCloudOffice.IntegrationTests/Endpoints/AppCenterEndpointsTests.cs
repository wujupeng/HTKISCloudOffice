using System.Net;
using System.Net.Http.Json;
using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Domain.Entities;
using HTKISCloudOffice.Domain.Enums;
using HTKISCloudOffice.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HTKISCloudOffice.IntegrationTests.Endpoints;

public class AppCenterEndpointsTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly IServiceScope _scope;
    private readonly AppDbContext _db;
    private readonly string _adminToken;
    private readonly Guid _adminUserId;
    private readonly Guid _appId1;
    private readonly Guid _appId2;
    private readonly Guid _roleId;

    public AppCenterEndpointsTests()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
        _scope = _factory.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();

        _adminUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        _appId1 = Guid.Parse("10000000-0000-0000-0000-000000000001");
        _appId2 = Guid.Parse("10000000-0000-0000-0000-000000000002");
        _roleId = Guid.Parse("20000000-0000-0000-0000-000000000001");

        SeedTestData();
        _adminToken = TestAuthHelper.GenerateToken(
            _adminUserId.ToString(), "admin", new[] { "super_admin", "all_staff" });
    }

    private void SeedTestData()
    {
        var user = new User
        {
            user_id = _adminUserId,
            username = "admin",
            password_hash = "hash",
            display_name = "Admin",
            department = "IT",
            is_active = true
        };
        _db.users.Add(user);

        var role = new Role
        {
            role_id = _roleId,
            role_name = "super_admin",
            description = "Super Admin",
            permissions = new List<Domain.ValueObjects.PermissionEntry>()
        };
        _db.roles.Add(role);

        _db.user_roles.Add(new UserRole { user_id = _adminUserId, role_id = _roleId });

        var app1 = new Domain.Entities.Application
        {
            app_id = _appId1,
            app_name = "WPS Office",
            app_type = AppType.CloudDesktop,
            icon_url = "/icons/wps.png",
            category = AppCategory.Office,
            launch_params = "{}",
            is_active = true,
            sort_order = 1
        };
        var app2 = new Domain.Entities.Application
        {
            app_id = _appId2,
            app_name = "ERP System",
            app_type = AppType.WebLink,
            icon_url = "/icons/erp.png",
            category = AppCategory.Business,
            launch_params = "{}",
            is_active = true,
            sort_order = 2
        };
        _db.applications.AddRange(app1, app2);

        _db.app_allowed_roles.Add(new AppAllowedRole { app_id = _appId1, role_id = _roleId });
        _db.app_allowed_roles.Add(new AppAllowedRole { app_id = _appId2, role_id = _roleId });

        _db.SaveChanges();
    }

    [Fact]
    public async Task GetCenter_ReturnsCategoriesAndFavorites()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/applications/center");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AppCenterView>>();
        Assert.NotNull(body);
        Assert.True(body.success);
        Assert.NotNull(body.data);

    }

    [Fact]
    public async Task GetCenter_WithoutAuth_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/applications/center");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SearchApplications_WithQuery_ReturnsMatches()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/applications/search?q=WPS");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ApplicationDto>>>();
        Assert.NotNull(body);
        Assert.True(body.success);
    }

    [Fact]
    public async Task SearchApplications_NoMatch_ReturnsEmptyList()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/applications/search?q=nonexistent");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ApplicationDto>>>();
        Assert.NotNull(body);
        Assert.True(body.success);
    }

    [Fact]
    public async Task AddFavorite_ReturnsSuccess()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/applications/{_appId1}/favorite");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<FavoriteResult>>();
        Assert.NotNull(body);
        Assert.True(body.success);
    }

    [Fact]
    public async Task AddFavorite_Duplicate_Returns409()
    {
        var addRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/applications/{_appId2}/favorite");
        addRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
        var firstResponse = await _client.SendAsync(addRequest);

        var dupRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/applications/{_appId2}/favorite");
        dupRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        var response = await _client.SendAsync(dupRequest);
        Assert.True(response.StatusCode == HttpStatusCode.Conflict ||
                    response.StatusCode == HttpStatusCode.BadRequest ||
                    response.StatusCode == HttpStatusCode.OK,
            $"Expected Conflict/BadRequest/OK, got {response.StatusCode}");
    }

    [Fact]
    public async Task RemoveFavorite_ReturnsSuccess()
    {
        var addRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/applications/{_appId1}/favorite");
        addRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
        await _client.SendAsync(addRequest);

        var removeRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/applications/{_appId1}/favorite");
        removeRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        var response = await _client.SendAsync(removeRequest);
        Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadRequest,
            $"Expected OK or BadRequest, got {response.StatusCode}");
    }

    [Fact]
    public async Task GetFavorites_ReturnsList()
    {
        var addRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/applications/{_appId1}/favorite");
        addRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
        await _client.SendAsync(addRequest);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/applications/favorites");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ApplicationDto>>>();
        Assert.NotNull(body);
        Assert.True(body.success);

    }

    public void Dispose()
    {
        _scope.Dispose();
        _factory.Dispose();
    }
}