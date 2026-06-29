using HTKISCloudOffice.Application.DTOs;
using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Domain.Entities;
using HTKISCloudOffice.Domain.Enums;

namespace HTKISCloudOffice.Application.Services;

public class AppCenterService : IAppCenterService
{
    private readonly IAppFavoriteRepository _favorite_repo;
    private readonly IAppIconRepository _icon_repo;
    private readonly IAppPortalService _portal_svc;
    private readonly IPermissionService _perm_svc;

    public AppCenterService(
        IAppFavoriteRepository favorite_repo,
        IAppIconRepository icon_repo,
        IAppPortalService portal_svc,
        IPermissionService perm_svc)
    {
        _favorite_repo = favorite_repo;
        _icon_repo = icon_repo;
        _portal_svc = portal_svc;
        _perm_svc = perm_svc;
    }

    public async Task<AppCenterView> GetAppCenterViewAsync(string user_id)
    {
        if (!Guid.TryParse(user_id, out var uid)) return new AppCenterView();

        var all_apps = await _portal_svc.GetApplicationsForUserAsync(user_id);
        var favorite_app_ids = await _favorite_repo.GetFavoriteAppIdsAsync(uid);
        var favorite_set = favorite_app_ids.ToHashSet();

        var favorites = all_apps.Where(a => favorite_set.Contains(Guid.Parse(a.app_id))).ToList();
        var remaining = all_apps.Where(a => !favorite_set.Contains(Guid.Parse(a.app_id))).ToList();

        var categories = remaining
            .GroupBy(a => a.category)
            .Select(g => new AppCategoryGroup
            {
                category = g.Key,
                category_name = GetCategoryName(g.Key),
                applications = g.OrderBy(a => a.app_name).ToList()
            })
            .OrderBy(c => c.category)
            .ToList();

        return new AppCenterView
        {
            favorites = favorites,
            categories = categories
        };
    }

    public async Task<List<ApplicationDto>> SearchApplicationsAsync(string user_id, string keyword)
    {
        var all_apps = await _portal_svc.GetApplicationsForUserAsync(user_id);
        if (string.IsNullOrWhiteSpace(keyword)) return all_apps;

        var lower_keyword = keyword.ToLowerInvariant();
        return all_apps.Where(a => a.app_name.ToLowerInvariant().Contains(lower_keyword)).ToList();
    }

    public async Task<FavoriteResult> AddFavoriteAsync(string user_id, string app_id)
    {
        if (!Guid.TryParse(user_id, out var uid) || !Guid.TryParse(app_id, out var aid))
            return FavoriteResult.Fail("INVALID_ID", "无效的ID格式");

        var exists = await _favorite_repo.IsFavoritedAsync(uid, aid);
        if (exists) return FavoriteResult.Fail("ALREADY_FAVORITED", "已收藏该应用");

        var favorite = new AppFavorite
        {
            user_id = uid,
            app_id = aid
        };

        var result = await _favorite_repo.AddAsync(favorite);
        if (result == null) return FavoriteResult.Fail("ADD_FAILED", "收藏失败");

        return FavoriteResult.Ok();
    }

    public async Task<FavoriteResult> RemoveFavoriteAsync(string user_id, string app_id)
    {
        if (!Guid.TryParse(user_id, out var uid) || !Guid.TryParse(app_id, out var aid))
            return FavoriteResult.Fail("INVALID_ID", "无效的ID格式");

        var removed = await _favorite_repo.RemoveAsync(uid, aid);
        if (!removed) return FavoriteResult.Fail("NOT_FAVORITED", "未收藏该应用");

        return FavoriteResult.Ok();
    }

    public async Task<List<ApplicationDto>> GetUserFavoritesAsync(string user_id)
    {
        if (!Guid.TryParse(user_id, out var uid)) return new List<ApplicationDto>();

        var favorites = await _favorite_repo.GetByUserIdAsync(uid);
        return favorites.Select(f => new ApplicationDto
        {
            app_id = f.Application.app_id.ToString(),
            app_name = f.Application.app_name,
            app_type = f.Application.app_type,
            icon_url = f.Application.icon_url,
            category = f.Application.category,
            description = f.Application.description ?? f.Application.launch_params
        }).ToList();
    }

    public async Task<AppIconDto> UploadAppIconAsync(string icon_name, byte[] icon_data, string content_type, string? uploaded_by)
    {
        var safe_name = SanitizeFileName(icon_name);
        var file_name = $"{Guid.NewGuid()}_{safe_name}";
        var relative_path = $"/icons/custom/{file_name}";
        var full_path = Path.Combine("wwwroot", "icons", "custom", file_name);

        Directory.CreateDirectory(Path.GetDirectoryName(full_path)!);
        await File.WriteAllBytesAsync(full_path, icon_data);

        var icon = new AppIcon
        {
            icon_name = safe_name,
            icon_type = AppIconType.Custom,
            icon_url = relative_path,
            uploaded_by = Guid.TryParse(uploaded_by, out var uid) ? uid : null
        };

        await _icon_repo.CreateAsync(icon);

        return new AppIconDto
        {
            icon_id = icon.icon_id.ToString(),
            icon_name = icon.icon_name,
            icon_type = icon.icon_type.ToString().ToLowerInvariant(),
            icon_url = icon.icon_url
        };
    }

    public async Task<List<AppIconDto>> GetAppIconsAsync()
    {
        var icons = await _icon_repo.GetAllAsync();
        return icons.Select(i => new AppIconDto
        {
            icon_id = i.icon_id.ToString(),
            icon_name = i.icon_name,
            icon_type = i.icon_type.ToString().ToLowerInvariant(),
            icon_url = i.icon_url
        }).ToList();
    }

    private static string GetCategoryName(AppCategory category) => category switch
    {
        AppCategory.Office => "办公应用",
        AppCategory.Business => "业务系统",
        AppCategory.File => "文件管理",
        AppCategory.Tool => "工具",
        _ => category.ToString()
    };

    private static string SanitizeFileName(string name)
    {
        var invalid = new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };
        foreach (var c in invalid) name = name.Replace(c, '_');
        return name;
    }
}