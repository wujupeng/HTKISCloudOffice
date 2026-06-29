using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Domain.Entities;
using HTKISCloudOffice.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HTKISCloudOffice.Infrastructure.Repositories;

public class DeviceBindingRepository : IDeviceBindingRepository
{
    private readonly AppDbContext _context;

    public DeviceBindingRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<DeviceBinding>> GetByUserIdAsync(Guid user_id)
    {
        return await _context.device_bindings
            .Where(b => b.user_id == user_id && b.is_active)
            .OrderByDescending(b => b.created_at)
            .ToListAsync();
    }

    public async Task<DeviceBinding?> GetByDeviceIdAsync(Guid user_id, string device_id)
    {
        return await _context.device_bindings
            .FirstOrDefaultAsync(b => b.user_id == user_id && b.device_id == device_id && b.is_active);
    }

    public async Task<DeviceBinding?> GetByBindingIdAsync(Guid binding_id)
    {
        return await _context.device_bindings
            .FirstOrDefaultAsync(b => b.binding_id == binding_id);
    }

    public async Task<DeviceBinding> CreateAsync(DeviceBinding binding)
    {
        _context.device_bindings.Add(binding);
        await _context.SaveChangesAsync();
        return binding;
    }

    public async Task UpdateLastLoginAsync(Guid binding_id, DateTime login_time, string ip_address)
    {
        var binding = await _context.device_bindings.FindAsync(binding_id);
        if (binding != null)
        {
            binding.last_login_at = login_time;
            binding.last_login_ip = ip_address;
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeactivateAsync(Guid binding_id)
    {
        var binding = await _context.device_bindings.FindAsync(binding_id);
        if (binding != null)
        {
            binding.is_active = false;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> CountActiveBindingsByUserIdAsync(Guid user_id)
    {
        return await _context.device_bindings
            .CountAsync(b => b.user_id == user_id && b.is_active);
    }

    public async Task<(List<DeviceBinding> items, int total)> GetAllAsync(int page, int page_size, Guid? user_id = null, bool? is_active = null)
    {
        var query = _context.device_bindings.AsQueryable();

        if (user_id.HasValue)
            query = query.Where(b => b.user_id == user_id.Value);

        if (is_active.HasValue)
            query = query.Where(b => b.is_active == is_active.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(b => b.created_at)
            .Skip((page - 1) * page_size)
            .Take(page_size)
            .ToListAsync();

        return (items, total);
    }
}