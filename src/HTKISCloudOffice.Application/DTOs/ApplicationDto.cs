using HTKISCloudOffice.Domain.Enums;

namespace HTKISCloudOffice.Application.DTOs;

public class ApplicationDto
{
    public string app_id { get; set; } = string.Empty;
    public string app_name { get; set; } = string.Empty;
    public AppType app_type { get; set; }
    public string icon_url { get; set; } = string.Empty;
    public AppCategory category { get; set; }
    public string description { get; set; } = string.Empty;
}