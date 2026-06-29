using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HTKISCloudOffice.Server.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", async (AppDbContext db, IGuacamoleApiClient guac_client) =>
        {
            var checks = new Dictionary<string, string>();
            var overall = "healthy";

            try
            {
                await db.Database.CanConnectAsync();
                checks["postgresql"] = "healthy";
            }
            catch
            {
                checks["postgresql"] = "unhealthy";
                overall = "degraded";
            }

            checks["libreoffice"] = await CheckLibreOfficeAsync();
            if (checks["libreoffice"] != "healthy" && overall == "healthy")
                overall = "degraded";

            var guac_token = await TryGuacamoleAuthAsync(guac_client);

            if (guac_token == null)
            {
                checks["guacamole_rdp"] = "unhealthy";
                checks["guacamole_vnc"] = "unhealthy";
                checks["guacamole_ssh"] = "unhealthy";
                if (overall == "healthy") overall = "degraded";
            }
            else
            {
                checks["guacamole_rdp"] = await CheckGuacamoleProtocolAsync(guac_client, guac_token, "rdp");
                checks["guacamole_vnc"] = await CheckGuacamoleProtocolAsync(guac_client, guac_token, "vnc");
                checks["guacamole_ssh"] = await CheckGuacamoleProtocolAsync(guac_client, guac_token, "ssh");

                if ((checks["guacamole_rdp"] != "healthy" || checks["guacamole_vnc"] != "healthy" || checks["guacamole_ssh"] != "healthy") && overall == "healthy")
                    overall = "degraded";
            }

            checks["samba_mount"] = CheckSambaMount();

            if (checks["samba_mount"] != "healthy" && overall == "healthy")
                overall = "degraded";

            return Results.Ok(new
            {
                status = overall,
                checks,
                timestamp = DateTime.UtcNow
            });
        });
    }

    private static async Task<string> CheckLibreOfficeAsync()
    {
        try
        {
            var start_info = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "which",
                Arguments = "libreoffice",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(start_info);
            if (process == null) return "unavailable";

            await process.WaitForExitAsync();
            return process.ExitCode == 0 ? "healthy" : "unavailable";
        }
        catch
        {
            return "unavailable";
        }
    }

    private static async Task<string?> TryGuacamoleAuthAsync(IGuacamoleApiClient guac_client)
    {
        try
        {
            return await guac_client.AuthenticateAsync();
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string> CheckGuacamoleProtocolAsync(IGuacamoleApiClient guac_client, string token, string protocol)
    {
        try
        {
            var connections = await guac_client.GetActiveConnectionsAsync();
            var has_protocol = connections.Any(c =>
                c.name.Contains(protocol, StringComparison.OrdinalIgnoreCase) ||
                c.connection_id.Contains(protocol, StringComparison.OrdinalIgnoreCase));

            return has_protocol ? "healthy" : "no_connections";
        }
        catch
        {
            return "unhealthy";
        }
    }

    private static string CheckSambaMount()
    {
        try
        {
            var mount_root = "/data/shares";
            if (!Directory.Exists(mount_root)) return "unhealthy";

            var sub_dirs = Directory.GetDirectories(mount_root);
            return sub_dirs.Length > 0 ? "healthy" : "no_shares";
        }
        catch
        {
            return "unhealthy";
        }
    }
}
