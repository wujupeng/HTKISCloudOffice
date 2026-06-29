using System.Net.Http.Json;
using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Domain.ValueObjects;
using HTKISCloudOffice.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;

namespace HTKISCloudOffice.Infrastructure.Clients;

public class GuacamoleApiClient : IGuacamoleApiClient
{
    private readonly HttpClient _http_client;
    private readonly GuacamoleConfig _config;
    private readonly ILogger<GuacamoleApiClient> _logger;
    private string? _auth_token;
    private DateTime _auth_token_expires = DateTime.MinValue;

    public GuacamoleApiClient(HttpClient http_client, GuacamoleConfig config, ILogger<GuacamoleApiClient> logger)
    {
        _http_client = http_client;
        _config = config;
        _logger = logger;
    }

    public async Task<string> AuthenticateAsync()
    {
        if (_auth_token != null && _auth_token_expires > DateTime.UtcNow.AddMinutes(5))
            return _auth_token;

        try
        {
            var response = await _http_client.PostAsJsonAsync($"{_config.base_url}/api/tokens", new
            {
                username = _config.admin_username,
                password = _config.admin_password
            });

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<GuacamoleTokenResponse>();
            _auth_token = result?.authToken ?? throw new InvalidOperationException("Failed to get Guacamole auth token");
            _auth_token_expires = DateTime.UtcNow.AddHours(1);
            return _auth_token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to authenticate with Guacamole");
            throw;
        }
    }

    public async Task<GuacamoleConnectionResult> CreateConnectionAsync(GuacamoleConnectionParams param)
    {
        var token = await AuthenticateAsync();

        var payload = new Dictionary<string, object>
        {
            ["name"] = $"htkis-{Guid.NewGuid():N}",
            ["protocol"] = "rdp",
            ["parameters"] = new Dictionary<string, string>
            {
                ["hostname"] = param.vm_hostname,
                ["port"] = param.rdp_port.ToString(),
                ["username"] = param.rdp_username,
                ["password"] = param.rdp_password_encrypted,
                ["resolution"] = param.resolution,
                ["color_depth"] = param.color_depth.ToString(),
                ["remote-app"] = param.app_direct_launch ? param.app_program_path : "",
                ["security_mode"] = "any",
                ["ignore_cert"] = "true"
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_config.base_url}/api/session/data/postgresql/connections")
        {
            Headers = { { "Guacamole-Token", token } },
            Content = JsonContent.Create(payload)
        };

        var response = await _http_client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var conn = await response.Content.ReadFromJsonAsync<GuacamoleConnectionCreateResponse>();

        return new GuacamoleConnectionResult
        {
            connection_id = conn?.identifier ?? "",
            guacamole_url = $"/guacamole/#/client/{conn?.identifier}"
        };
    }

    public async Task<GuacamoleConnectionResult> CreateVncConnectionAsync(VncConnectionParams param)
    {
        var token = await AuthenticateAsync();

        var payload = new Dictionary<string, object>
        {
            ["name"] = $"htkis-vnc-{Guid.NewGuid():N}",
            ["protocol"] = "vnc",
            ["parameters"] = new Dictionary<string, string>
            {
                ["hostname"] = param.hostname,
                ["port"] = param.vnc_port.ToString(),
                ["password"] = param.vnc_password_encrypted,
                ["color-depth"] = param.color_depth.ToString(),
                ["swap-red-blue"] = param.swap_red_blue.ToString().ToLower(),
                ["cursor"] = param.cursor
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_config.base_url}/api/session/data/postgresql/connections")
        {
            Headers = { { "Guacamole-Token", token } },
            Content = JsonContent.Create(payload)
        };

        var response = await _http_client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var conn = await response.Content.ReadFromJsonAsync<GuacamoleConnectionCreateResponse>();

        return new GuacamoleConnectionResult
        {
            connection_id = conn?.identifier ?? "",
            guacamole_url = $"/guacamole/#/client/{conn?.identifier}"
        };
    }

    public async Task<GuacamoleConnectionResult> CreateSshConnectionAsync(SshConnectionParams param)
    {
        var token = await AuthenticateAsync();

        var parameters = new Dictionary<string, string>
        {
            ["hostname"] = param.hostname,
            ["port"] = param.ssh_port.ToString(),
            ["username"] = param.ssh_username,
            ["password"] = param.ssh_password_encrypted,
            ["font-name"] = param.font_name,
            ["font-size"] = param.font_size.ToString()
        };

        if (!string.IsNullOrEmpty(param.private_key))
            parameters["private-key"] = param.private_key;

        var payload = new Dictionary<string, object>
        {
            ["name"] = $"htkis-ssh-{Guid.NewGuid():N}",
            ["protocol"] = "ssh",
            ["parameters"] = parameters
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_config.base_url}/api/session/data/postgresql/connections")
        {
            Headers = { { "Guacamole-Token", token } },
            Content = JsonContent.Create(payload)
        };

        var response = await _http_client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var conn = await response.Content.ReadFromJsonAsync<GuacamoleConnectionCreateResponse>();

        return new GuacamoleConnectionResult
        {
            connection_id = conn?.identifier ?? "",
            guacamole_url = $"/guacamole/#/client/{conn?.identifier}"
        };
    }

    public async Task<GuacamoleConnectionResult> CreateRemoteAppConnectionAsync(RemoteAppConnectionParams param)
    {
        var token = await AuthenticateAsync();

        var payload = new Dictionary<string, object>
        {
            ["name"] = $"htkis-rapp-{Guid.NewGuid():N}",
            ["protocol"] = "rdp",
            ["parameters"] = new Dictionary<string, string>
            {
                ["hostname"] = param.vm_hostname,
                ["port"] = param.rdp_port.ToString(),
                ["username"] = param.rdp_username,
                ["password"] = param.rdp_password_encrypted,
                ["resolution"] = param.resolution,
                ["color_depth"] = param.color_depth.ToString(),
                ["remote-app"] = param.remote_app_program,
                ["security_mode"] = "any",
                ["ignore_cert"] = "true",
                ["disable-wallpaper"] = param.disable_wallpaper.ToString().ToLower(),
                ["disable-full-window-drag"] = param.disable_full_window_drag.ToString().ToLower(),
                ["disable-menu-animations"] = param.disable_menu_animations.ToString().ToLower(),
                ["disable-theming"] = param.disable_theming.ToString().ToLower()
            }
        };

        if (!string.IsNullOrEmpty(param.remote_app_dir))
            ((Dictionary<string, string>)payload["parameters"])["remote-app-dir"] = param.remote_app_dir;
        if (!string.IsNullOrEmpty(param.remote_app_args))
            ((Dictionary<string, string>)payload["parameters"])["remote-app-args"] = param.remote_app_args;

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_config.base_url}/api/session/data/postgresql/connections")
        {
            Headers = { { "Guacamole-Token", token } },
            Content = JsonContent.Create(payload)
        };

        var response = await _http_client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var conn = await response.Content.ReadFromJsonAsync<GuacamoleConnectionCreateResponse>();

        return new GuacamoleConnectionResult
        {
            connection_id = conn?.identifier ?? "",
            guacamole_url = $"/guacamole/#/client/{conn?.identifier}"
        };
    }

    public async Task<GuacamoleConnectionDetail?> GetConnectionAsync(string connection_id)
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{_config.base_url}/api/session/data/postgresql/connections/{connection_id}")
        {
            Headers = { { "Guacamole-Token", token } }
        };

        var response = await _http_client.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        return await response.Content.ReadFromJsonAsync<GuacamoleConnectionDetail>();
    }

    public async Task DeleteConnectionAsync(string connection_id)
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Delete,
            $"{_config.base_url}/api/session/data/postgresql/connections/{connection_id}")
        {
            Headers = { { "Guacamole-Token", token } }
        };

        await _http_client.SendAsync(request);
    }

    public async Task<List<GuacamoleConnectionDetail>> GetActiveConnectionsAsync()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{_config.base_url}/api/session/data/postgresql/activeConnections")
        {
            Headers = { { "Guacamole-Token", token } }
        };

        var response = await _http_client.SendAsync(request);
        if (!response.IsSuccessStatusCode) return new();

        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, GuacamoleConnectionDetail>>();
        return result?.Values.ToList() ?? new();
    }
}

public class GuacamoleTokenResponse
{
    public string authToken { get; set; } = string.Empty;
}

public class GuacamoleConnectionCreateResponse
{
    public string identifier { get; set; } = string.Empty;
}