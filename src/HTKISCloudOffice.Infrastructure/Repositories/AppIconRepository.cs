using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Domain.Entities;
using HTKISCloudOffice.Domain.Enums;
using HTKISCloudOffice.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HTKISCloudOffice.Infrastructure.Repositories;

public class AppIconRepository : IAppIconRepository
{
    private readonly AppDbContext _context;

    public AppIconRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<AppIcon>> GetAllAsync()
    {
        return await _context.app_icons.OrderBy(i => i.icon_name).ToListAsync();
    }

    public async Task<AppIcon?> GetByIdAsync(Guid icon_id)
    {
        return await _context.app_icons.FindAsync(icon_id);
    }

    public async Task<List<AppIcon>> GetPresetIconsAsync()
    {
        return await _context.app_icons
            .Where(i => i.icon_type == AppIconType.Preset)
            .OrderBy(i => i.icon_name)
            .ToListAsync();
    }

    public async Task<AppIcon> CreateAsync(AppIcon icon)
    {
        _context.app_icons.Add(icon);
        await _context.SaveChangesAsync();
        return icon;
    }

    public async Task<bool> DeleteAsync(Guid icon_id)
    {
        var icon = await _context.app_icons.FindAsync(icon_id);
        if (icon == null) return false;

        _context.app_icons.Remove(icon);
        await _context.SaveChangesAsync();
        return true;
    }
}