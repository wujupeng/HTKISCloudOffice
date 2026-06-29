using HTKISCloudOffice.Application.DTOs;

namespace HTKISCloudOffice.Application.Interfaces;

public interface IDeviceAuthService
{
    Task<AuthResult> AutoLoginWithDeviceAsync(string device_token, string device_id, string ip_address);
    Task<DeviceBindResult> BindDeviceAsync(Guid user_id, string device_id, string device_name, string ip_address);
    Task UnbindDeviceAsync(Guid binding_id, Guid user_id, string ip_address);
    Task<List<DeviceBindingDto>> GetUserDevicesAsync(Guid user_id);
    Task<TokenRefreshResult> RefreshTokenAsync(string token);
    Task<PagedResult<DeviceBindingDto>> GetAllDeviceBindingsAsync(DeviceBindingFilter filter);
    Task<DeviceTokenValidationResult> ValidateDeviceTokenAsync(string device_token, string device_id);
}