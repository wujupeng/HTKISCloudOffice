using HTKISCloudOffice.Domain.Enums;

namespace HTKISCloudOffice.Application.DTOs;

public class AppCenterView
{
    public List<ApplicationDto> favorites { get; set; } = new();
    public List<AppCategoryGroup> categories { get; set; } = new();
}

public class AppCategoryGroup
{
    public AppCategory category { get; set; }
    public string category_name { get; set; } = string.Empty;
    public List<ApplicationDto> applications { get; set; } = new();
}

public class FavoriteResult
{
    public bool success { get; set; }
    public string error_code { get; set; } = string.Empty;
    public string error_message { get; set; } = string.Empty;

    public static FavoriteResult Ok() => new() { success = true };
    public static FavoriteResult Fail(string code, string msg) => new() { success = false, error_code = code, error_message = msg };
}

public class AppIconDto
{
    public string icon_id { get; set; } = string.Empty;
    public string icon_name { get; set; } = string.Empty;
    public string icon_type { get; set; } = string.Empty;
    public string icon_url { get; set; } = string.Empty;
}