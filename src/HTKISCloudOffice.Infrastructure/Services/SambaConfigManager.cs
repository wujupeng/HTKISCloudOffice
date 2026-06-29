using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;

namespace HTKISCloudOffice.Infrastructure.Services;

public class SambaConfigManager : ISambaConfigManager
{
    private readonly SambaConfig _config;
    private readonly ILogger<SambaConfigManager> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SambaConfigManager(SambaConfig config, ILogger<SambaConfigManager> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task CreateShareAsync(string share_name, string path, List<string> allowed_users)
    {
        await _lock.WaitAsync();
        try
        {
            var share_section = $"""

                [{share_name}]
                    path = {path}
                    browseable = yes
                    read only = no
                    valid users = {string.Join(",", allowed_users)}
                    create mask = 0660
                    directory mask = 0770

                """;

            await File.AppendAllTextAsync(_config.config_path, share_section);
            await ReloadConfigAsync();
            _logger.LogInformation("Created Samba share: {ShareName}", share_name);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateSharePermissionsAsync(string share_name, List<string> allowed_users)
    {
        await _lock.WaitAsync();
        try
        {
            var content = await File.ReadAllTextAsync(_config.config_path);
            var share_start = content.IndexOf($"[{share_name}]", StringComparison.Ordinal);
            if (share_start < 0) return;

            var next_share = content.IndexOf("\n[", share_start + 1, StringComparison.Ordinal);
            var section_end = next_share > 0 ? next_share : content.Length;
            var section = content[share_start..section_end];

            var updated = System.Text.RegularExpressions.Regex.Replace(
                section,
                @"valid users = .+",
                $"valid users = {string.Join(",", allowed_users)}");

            content = content[..share_start] + updated + content[section_end..];
            await File.WriteAllTextAsync(_config.config_path, content);
            await ReloadConfigAsync();
            _logger.LogInformation("Updated Samba share permissions: {ShareName}", share_name);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RemoveShareAsync(string share_name)
    {
        await _lock.WaitAsync();
        try
        {
            var content = await File.ReadAllTextAsync(_config.config_path);
            var share_start = content.IndexOf($"[{share_name}]", StringComparison.Ordinal);
            if (share_start < 0) return;

            var next_share = content.IndexOf("\n[", share_start + 1, StringComparison.Ordinal);
            var section_end = next_share > 0 ? next_share : content.Length;

            content = content[..share_start] + content[section_end..];
            await File.WriteAllTextAsync(_config.config_path, content);
            await ReloadConfigAsync();
            _logger.LogInformation("Removed Samba share: {ShareName}", share_name);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ReloadConfigAsync()
    {
        try
        {
            var parts = _config.reload_command.Split(' ');
            var process = System.Diagnostics.Process.Start(parts[0], string.Join(" ", parts[1..]));
            if (process != null) await process.WaitForExitAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload Samba configuration");
        }
    }

    public async Task<bool> ValidateShareAsync(string share_name)
    {
        var content = await File.ReadAllTextAsync(_config.config_path);
        return content.Contains($"[{share_name}]");
    }
}