using HTKISCloudOffice.Domain.Entities;
using HTKISCloudOffice.Domain.Enums;

namespace HTKISCloudOffice.Application.Interfaces;

public interface ISharedDriveRepository
{
    Task<List<SharedDrive>> GetByRoleIdsAsync(List<Guid> role_ids);
    Task<SharedDrive?> GetByIdAsync(Guid drive_id);
    Task<List<SharedDrive>> GetAllAsync();
    Task<SharedDrive> CreateAsync(SharedDrive drive);
    Task UpdateAsync(SharedDrive drive);
}

public interface IVmConfigRepository
{
    Task<VmConfig?> GetByIdAsync(string vm_id);
    Task<VmConfig?> GetByUserIdAsync(Guid user_id);
    Task<VmConfig> CreateAsync(VmConfig vm);
    Task UpdateAsync(VmConfig vm);
}

public interface IAuditLogRepository
{
    Task LogAsync(AuditLog entry);
    Task<(List<AuditLog> items, int total)> QueryAsync(
        Guid? user_id = null, AuditAction? action = null,
        DateTime? start_time = null, DateTime? end_time = null,
        int page = 1, int page_size = 50);
}