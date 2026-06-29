using AppEntity = HTKISCloudOffice.Domain.Entities.Application;
using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Domain.Enums;
using HTKISCloudOffice.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HTKISCloudOffice.Infrastructure.Repositories;

public class ApplicationRepository : IApplicationRepository
{
    private readonly AppDbContext _context;

    public ApplicationRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<AppEntity>> GetByRoleIdsAsync(List<Guid> role_ids)
    {
        return await _context.applications
            .Include(a => a.app_allowed_roles)
            .Where(a => a.is_active && a.app_allowed_roles.Any(ar => role_ids.Contains(ar.role_id)))
            .OrderBy(a => a.sort_order)
            .ToListAsync();
    }

    public async Task<AppEntity?> GetByIdAsync(Guid app_id)
    {
        return await _context.applications
            .Include(a => a.app_allowed_roles)
            .FirstOrDefaultAsync(a => a.app_id == app_id);
    }

    public async Task<List<AppEntity>> GetAllAsync(bool include_inactive = false)
    {
        var query = _context.applications.Include(a => a.app_allowed_roles).AsQueryable();
        if (!include_inactive) query = query.Where(a => a.is_active);
        return await query.OrderBy(a => a.sort_order).ToListAsync();
    }

    public async Task<AppEntity> CreateAsync(AppEntity app)
    {
        _context.applications.Add(app);
        await _context.SaveChangesAsync();
        return app;
    }

    public async Task UpdateAsync(AppEntity app)
    {
        app.updated_at = DateTime.UtcNow;
        _context.applications.Update(app);
        await _context.SaveChangesAsync();
    }

    public async Task<List<AppEntity>> GetByCategoryAsync(AppCategory category, List<Guid> role_ids)
    {
        return await _context.applications
            .Include(a => a.app_allowed_roles)
            .Where(a => a.is_active && a.category == category
                        && a.app_allowed_roles.Any(ar => role_ids.Contains(ar.role_id)))
            .OrderBy(a => a.sort_order)
            .ToListAsync();
    }
}
