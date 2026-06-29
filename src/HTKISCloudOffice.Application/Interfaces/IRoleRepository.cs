using HTKISCloudOffice.Domain.Entities;

namespace HTKISCloudOffice.Application.Interfaces;

public interface IRoleRepository
{
    Task<List<Role>> GetByIdsAsync(List<Guid> role_ids);
    Task<Role?> GetByIdAsync(Guid role_id);
    Task<List<Role>> GetAllAsync();
    Task<Role> CreateAsync(Role role);
    Task<List<Role>> GetByUserIdAsync(Guid user_id);
}