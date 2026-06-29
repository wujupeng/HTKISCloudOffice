using HTKISCloudOffice.Domain.Enums;

namespace HTKISCloudOffice.Application.Interfaces;

public interface IAppIconRepository
{
    Task<List<Domain.Entities.AppIcon>> GetAllAsync();
    Task<Domain.Entities.AppIcon?> GetByIdAsync(Guid icon_id);
    Task<List<Domain.Entities.AppIcon>> GetPresetIconsAsync();
    Task<Domain.Entities.AppIcon> CreateAsync(Domain.Entities.AppIcon icon);
    Task<bool> DeleteAsync(Guid icon_id);
}