using HTKISCloudOffice.Application.DTOs;

namespace HTKISCloudOffice.Application.Interfaces;

public interface IAppCenterService
{
    Task<AppCenterView> GetAppCenterViewAsync(string user_id);
    Task<List<ApplicationDto>> SearchApplicationsAsync(string user_id, string keyword);
    Task<FavoriteResult> AddFavoriteAsync(string user_id, string app_id);
    Task<FavoriteResult> RemoveFavoriteAsync(string user_id, string app_id);
    Task<List<ApplicationDto>> GetUserFavoritesAsync(string user_id);
    Task<AppIconDto> UploadAppIconAsync(string icon_name, byte[] icon_data, string content_type, string? uploaded_by);
    Task<List<AppIconDto>> GetAppIconsAsync();
}