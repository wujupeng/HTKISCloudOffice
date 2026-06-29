using HTKISCloudOffice.Domain.Entities;

namespace HTKISCloudOffice.Application.Interfaces;

public interface IDeviceBindingRepository
{
    Task<List<DeviceBinding>> GetByUserIdAsync(Guid user_id);
    Task<DeviceBinding?> GetByDeviceIdAsync(Guid user_id, string device_id);
    Task<DeviceBinding?> GetByBindingIdAsync(Guid binding_id);
    Task<DeviceBinding> CreateAsync(DeviceBinding binding);
    Task UpdateLastLoginAsync(Guid binding_id, DateTime login_time, string ip_address);
    Task DeactivateAsync(Guid binding_id);
    Task<int> CountActiveBindingsByUserIdAsync(Guid user_id);
    Task<(List<DeviceBinding> items, int total)> GetAllAsync(int page, int page_size, Guid? user_id = null, bool? is_active = null);
}