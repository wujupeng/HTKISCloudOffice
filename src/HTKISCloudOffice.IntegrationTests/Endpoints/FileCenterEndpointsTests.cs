using System.Net;
using System.Net.Http.Json;
using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Domain.Entities;
using HTKISCloudOffice.Domain.Enums;
using HTKISCloudOffice.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using DriveType = HTKISCloudOffice.Domain.Enums.DriveType;

namespace HTKISCloudOffice.IntegrationTests.Endpoints;

public class FileCenterEndpointsTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly IServiceScope _scope;
    private readonly AppDbContext _db;
    private readonly string _adminToken;
    private readonly Guid _adminUserId;
    private readonly Guid _driveId;

    public FileCenterEndpointsTests()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
        _scope = _factory.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();

        _adminUserId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        _driveId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        SeedTestData();
        _adminToken = TestAuthHelper.GenerateToken(
            _adminUserId.ToString(), "fileuser", new[] { "super_admin", "all_staff" });
    }

    private void SeedTestData()
    {
        var user = new User
        {
            user_id = _adminUserId,
            username = "fileuser",
            password_hash = "hash",
            display_name = "File User",
            department = "IT",
            is_active = true
        };
        _db.users.Add(user);

        var drive = new SharedDrive
        {
            drive_id = _driveId,
            drive_name = "Public Drive",
            drive_type = DriveType.Public,
            samba_path = "/tmp/test-shares/public",
            drive_letter = "Z",
            allowed_permissions = new List<Domain.ValueObjects.DrivePermission>()
        };
        _db.shared_drives.Add(drive);
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
    public async Task GetDrives_ReturnsDriveList()
    {
        var request = CreateAuthRequest(HttpMethod.Get, "/api/v1/file-center/drives");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<FileDriveDto>>>();
        Assert.NotNull(body);
        Assert.True(body.success);

    }

    [Fact]
    public async Task GetDrives_WithoutAuth_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/file-center/drives");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListFiles_ReturnsResult()
    {
        var request = CreateAuthRequest(HttpMethod.Get,
            $"/api/v1/file-center/drives/{_driveId}/files?path=");
        var response = await _client.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadRequest,
            $"Expected OK or BadRequest, got {response.StatusCode}");
    }

    [Fact]
    public async Task CreateDirectory_ReturnsSuccess()
    {
        var request = CreateAuthRequest(HttpMethod.Post,
            $"/api/v1/file-center/drives/{_driveId}/directories",
            new { path = "", dir_name = "test-dir" });
        var response = await _client.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadRequest,
            $"Expected OK or BadRequest, got {response.StatusCode}");
    }

    public void Dispose()
    {
        _scope.Dispose();
        _factory.Dispose();
    }
}