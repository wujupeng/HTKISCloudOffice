using HTKISCloudOffice.Domain.Entities;
using HTKISCloudOffice.Domain.Enums;

namespace HTKISCloudOffice.Application.Interfaces;

public interface IConnectionConfigRepository
{
    Task<List<ConnectionConfig>> GetAllAsync();
    Task<ConnectionConfig?> GetByIdAsync(Guid connection_id);
    Task<List<ConnectionConfig>> GetByUserRolesAsync(List<Guid> role_ids);
    Task<ConnectionConfig> CreateAsync(ConnectionConfig config, List<Guid> allowed_role_ids);
    Task<ConnectionConfig> UpdateAsync(ConnectionConfig config, List<Guid>? allowed_role_ids);
    Task DeleteAsync(Guid connection_id);
    Task<List<ConnectionConfig>> GetActiveByProtocolAsync(ConnectionProtocol protocol);
}