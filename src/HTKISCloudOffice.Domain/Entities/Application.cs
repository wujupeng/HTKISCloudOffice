using HTKISCloudOffice.Domain.Enums;

namespace HTKISCloudOffice.Domain.Entities;

public class Application
{
    public Guid app_id { get; set; } = Guid.NewGuid();
    public string app_name { get; set; } = string.Empty;
    public AppType app_type { get; set; }
    public string icon_url { get; set; } = string.Empty;
    public AppCategory category { get; set; }
    public string launch_params { get; set; } = "{}";
    public string? description { get; set; }
    public Guid? icon_id { get; set; }
    public bool is_active { get; set; } = true;
    public int sort_order { get; set; }
    public DateTime created_at { get; set; } = DateTime.UtcNow;
    public DateTime updated_at { get; set; } = DateTime.UtcNow;

    public ICollection<AppAllowedRole> app_allowed_roles { get; set; } = new List<AppAllowedRole>();
}