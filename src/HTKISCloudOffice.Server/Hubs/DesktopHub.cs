using Microsoft.AspNetCore.SignalR;

namespace HTKISCloudOffice.Server.Hubs;

public class DesktopHub : Hub
{
    public async Task Heartbeat()
    {
        await Clients.Caller.SendAsync("HeartbeatAck", DateTime.UtcNow);
    }

    public async Task RequestReconnect(string connection_id)
    {
        await Clients.Caller.SendAsync("ReconnectApproved", connection_id);
    }

    public async Task RequestTokenRefresh()
    {
        await Clients.Caller.SendAsync("TokenRefreshResult", true);
    }

    public static async Task NotifyConnectionStatusChanged(IHubContext<DesktopHub> hub, string connection_id, string status, string protocol = "")
    {
        await hub.Clients.All.SendAsync("ConnectionStatusChanged", connection_id, status, protocol);
    }

    public static async Task NotifyPermissionUpdated(IHubContext<DesktopHub> hub, string user_id)
    {
        await hub.Clients.All.SendAsync("PermissionUpdated", user_id);
    }

    public static async Task NotifyDesktopAlert(IHubContext<DesktopHub> hub, string message)
    {
        await hub.Clients.All.SendAsync("DesktopAlert", message);
    }

    public static async Task NotifyTokenExpiring(IHubContext<DesktopHub> hub, int minutes_remaining)
    {
        await hub.Clients.All.SendAsync("TokenExpiring", minutes_remaining);
    }

    public static async Task NotifyFileOperationProgress(IHubContext<DesktopHub> hub, string operation_id, int progress)
    {
        await hub.Clients.All.SendAsync("FileOperationProgress", operation_id, progress);
    }
}