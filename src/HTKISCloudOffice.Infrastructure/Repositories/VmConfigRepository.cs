using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Domain.Entities;
using HTKISCloudOffice.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HTKISCloudOffice.Infrastructure.Repositories;

public class VmConfigRepository : IVmConfigRepository
{
    private readonly AppDbContext _context;

    public VmConfigRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<VmConfig?> GetByIdAsync(string vm_id)
    {
        return await _context.vm_configs.FindAsync(vm_id);
    }

    public async Task<VmConfig?> GetByUserIdAsync(Guid user_id)
    {
        var user = await _context.users.FindAsync(user_id);
        if (user?.bound_vm_id == null) return null;
        return await _context.vm_configs.FindAsync(user.bound_vm_id);
    }

    public async Task<VmConfig> CreateAsync(VmConfig vm)
    {
        _context.vm_configs.Add(vm);
        await _context.SaveChangesAsync();
        return vm;
    }

    public async Task UpdateAsync(VmConfig vm)
    {
        vm.updated_at = DateTime.UtcNow;
        _context.vm_configs.Update(vm);
        await _context.SaveChangesAsync();
    }
}
