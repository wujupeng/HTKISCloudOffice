using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Domain.Entities;
using HTKISCloudOffice.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HTKISCloudOffice.Infrastructure.Repositories;

public class SharedDriveRepository : ISharedDriveRepository
{
    private readonly AppDbContext _context;

    public SharedDriveRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<SharedDrive>> GetByRoleIdsAsync(List<Guid> role_ids)
    {
        var all_drives = await _context.shared_drives.Where(d => d.is_active).ToListAsync();
        return all_drives.Where(d => d.allowed_permissions.Any(p => role_ids.Contains(Guid.Parse(p.role_id)))).ToList();
    }

    public async Task<SharedDrive?> GetByIdAsync(Guid drive_id)
    {
        return await _context.shared_drives.FindAsync(drive_id);
    }

    public async Task<List<SharedDrive>> GetAllAsync()
    {
        return await _context.shared_drives.ToListAsync();
    }

    public async Task<SharedDrive> CreateAsync(SharedDrive drive)
    {
        _context.shared_drives.Add(drive);
        await _context.SaveChangesAsync();
        return drive;
    }

    public async Task UpdateAsync(SharedDrive drive)
    {
        drive.updated_at = DateTime.UtcNow;
        _context.shared_drives.Update(drive);
        await _context.SaveChangesAsync();
    }
}
