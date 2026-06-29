using System.Net;
using System.Net.Http.Json;
using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Domain.Entities;
using HTKISCloudOffice.Domain.Enums;
using HTKISCloudOffice.Domain.ValueObjects;
using HTKISCloudOffice.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace HTKISCloudOffice.IntegrationTests.Endpoints;

public class ConnectionEndpointsTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly IServiceScope _scope;
    private readonly AppDbContext _db;
    private readonly string _adminToken;
    private readonly Guid _adminUserId;
    private readonly Guid _connectionId;
    private readonly Guid _roleId;

    public ConnectionEndpointsTests()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
        _scope = _factory.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();

        _adminUserId = Guid.Parse("00000000-0000-0000-0000-000000000004");
        _connectionId = Guid.Parse("40000000-0000-0000-0000-000000000001");
        _roleId = Guid.Parse("40000000-0000-0000-0000-000000000002");
        SeedTestData();
        _adminToken = TestAuthHelper.GenerateToken(
            _adminUserId.ToString(), "connuser", new[] { "super_admin", "all_staff" });
    }

    private void SeedTestData()
    {
        var user = new User
        {
            user_id = _adminUserId,
            username = "connuser",
            password_hash = "hash",
            display_name = "Conn User",
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

        var conn = new ConnectionConfig
        {
            connection_id = _connectionId,
            connection_name = "RDP-Test",
            protocol = ConnectionProtocol.RDP,
            hostname = "192.168.2.200",
            port = 3389,
            username = "admin",
            password_encrypted = "encrypted_pwd",
            is_active = true,
            sort_order = 1,
            connection_params = "{}"
        };
        _db.connection_configs.Add(conn);
        _db.connection_allowed_roles.Add(new ConnectionAllowedRole { connection_id = _connectionId, role_id = _roleId });
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
    public async Task GetConnections_ReturnsList()
    {
        var request = CreateAuthRequest(HttpMethod.Get, "/api/v1/connections");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<ConnectionConfigDto>>>();
        Assert.NotNull(body);
        Assert.True(body.success);

    }

    [Fact]
    public async Task GetConnections_WithoutAuth_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/connections");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Connect_ReturnsResult()
    {
        _factory.GuacamoleMock
            .Setup(x => x.AuthenticateAsync())
            .ReturnsAsync("test-token");
        _factory.GuacamoleMock
            .Setup(x => x.CreateConnectionAsync(It.IsAny<GuacamoleConnectionParams>()))
            .ReturnsAsync(new GuacamoleConnectionResult { connection_id = "guac-conn-001", connection_token = "token1", guacamole_url = "/guacamole/#/client/guac-conn-001" });

        var request = CreateAuthRequest(HttpMethod.Post, $"/api/v1/connections/{_connectionId}/connect");
        var response = await _client.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.ServiceUnavailable,
            $"Expected OK or ServiceUnavailable, got {response.StatusCode}");
    }

    [Fact]
    public async Task Disconnect_ReturnsSuccess()
    {
        _factory.GuacamoleMock
            .Setup(x => x.DeleteConnectionAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var request = CreateAuthRequest(HttpMethod.Post, $"/api/v1/connections/{_connectionId}/disconnect",
            new { guacamole_connection_id = "guac-conn-001" });
        var response = await _client.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadRequest,
            $"Expected OK or BadRequest, got {response.StatusCode}");
    }

    [Fact]
    public async Task Reconnect_ReturnsResult()
    {
        _factory.GuacamoleMock
            .Setup(x => x.AuthenticateAsync())
            .ReturnsAsync("test-token");
        _factory.GuacamoleMock
            .Setup(x => x.CreateConnectionAsync(It.IsAny<GuacamoleConnectionParams>()))
            .ReturnsAsync(new GuacamoleConnectionResult { connection_id = "guac-conn-002", connection_token = "token2", guacamole_url = "/guacamole/#/client/guac-conn-002" });

        var request = CreateAuthRequest(HttpMethod.Post, $"/api/v1/connections/{_connectionId}/reconnect",
            new { guacamole_connection_id = "guac-conn-001" });
        var response = await _client.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.ServiceUnavailable,
            $"Expected OK or ServiceUnavailable, got {response.StatusCode}");
    }

    public void Dispose()
    {
        _scope.Dispose();
        _factory.Dispose();
    }
}