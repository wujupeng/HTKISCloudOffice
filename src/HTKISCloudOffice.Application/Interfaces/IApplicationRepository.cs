using AppEntity = HTKISCloudOffice.Domain.Entities.Application;
using HTKISCloudOffice.Domain.Enums;

namespace HTKISCloudOffice.Application.Interfaces;

public interface IApplicationRepository
{
    Task<List<AppEntity>> GetByRoleIdsAsync(List<Guid> role_ids);
    Task<AppEntity?> GetByIdAsync(Guid app_id);
    Task<List<AppEntity>> GetAllAsync(bool include_inactive = false);
    Task<AppEntity> CreateAsync(AppEntity app);
    Task UpdateAsync(AppEntity app);
    Task<List<AppEntity>> GetByCategoryAsync(AppCategory category, List<Guid> role_ids);
}
