using HTKISCloudOffice.Domain.Enums;

namespace HTKISCloudOffice.Domain.Entities;

public class AppIcon
{
    public Guid icon_id { get; set; } = Guid.NewGuid();
    public string icon_name { get; set; } = string.Empty;
    public AppIconType icon_type { get; set; } = AppIconType.Preset;
    public string icon_url { get; set; } = string.Empty;
    public Guid? uploaded_by { get; set; }
    public DateTime created_at { get; set; } = DateTime.UtcNow;
}