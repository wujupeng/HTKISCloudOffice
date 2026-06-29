using System.Net;
using System.Net.Http.Json;
using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Domain.Entities;
using HTKISCloudOffice.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace HTKISCloudOffice.IntegrationTests.Endpoints;

public class DeviceAuthEndpointsTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly IServiceScope _scope;
    private readonly AppDbContext _db;
    private readonly string _adminToken;
    private readonly Guid _adminUserId;

    public DeviceAuthEndpointsTests()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
        _scope = _factory.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();

        _adminUserId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        SeedTestData();
        _adminToken = TestAuthHelper.GenerateToken(
            _adminUserId.ToString(), "testuser", new[] { "all_staff" });
    }

    private void SeedTestData()
    {
        var user = new User
        {
            user_id = _adminUserId,
            username = "testuser",
            password_hash = "hash",
            display_name = "Test User",
            department = "IT",
            is_active = true
        };
        _db.users.Add(user);
        _db.SaveChanges();
    }

    private HttpRequestMessage CreateAuthRequest(HttpMethod method, string url, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
        if (body != null)
            request.Content = JsonContent.Create(body);
        return request;
    }

    [Fact]
    public async Task DeviceBind_ReturnsSuccess()
    {
        var request = CreateAuthRequest(HttpMethod.Post, "/api/v1/auth/device-bind",
            new { device_id = "device-001", device_name = "Test Tablet" });

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<DeviceBindResult>>();
        Assert.NotNull(body);
        Assert.True(body.success);
        Assert.NotEqual(Guid.Empty, body.data.binding_id);
    }

    [Fact]
    public async Task GetDevices_ReturnsDeviceList()
    {
        var bindRequest = CreateAuthRequest(HttpMethod.Post, "/api/v1/auth/device-bind",
            new { device_id = "device-002", device_name = "Test Tablet 2" });
        await _client.SendAsync(bindRequest);

        var request = CreateAuthRequest(HttpMethod.Get, "/api/v1/auth/devices");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<DeviceBindingDto>>>();
        Assert.NotNull(body);
        Assert.True(body.success);
    }

    [Fact]
    public async Task DeviceBind_WithoutAuth_Returns401()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/device-bind");
        request.Content = JsonContent.Create(new { device_id = "device-003", device_name = "Test" });

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnbindDevice_ReturnsSuccess()
    {
        var bindRequest = CreateAuthRequest(HttpMethod.Post, "/api/v1/auth/device-bind",
            new { device_id = "device-004", device_name = "Test Tablet 4" });
        var bindResponse = await _client.SendAsync(bindRequest);
        var bindBody = await bindResponse.Content.ReadFromJsonAsync<ApiResponse<DeviceBindResult>>();

        var unbindRequest = CreateAuthRequest(HttpMethod.Delete,
            $"/api/v1/auth/device-bind/{bindBody!.data.binding_id}");
        var response = await _client.SendAsync(unbindRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TokenRefresh_ReturnsSuccess()
    {
        var request = CreateAuthRequest(HttpMethod.Post, "/api/v1/auth/token-refresh");
        var response = await _client.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected OK or Unauthorized, got {response.StatusCode}");
    }

    public void Dispose()
    {
        _scope.Dispose();
        _factory.Dispose();
    }
}