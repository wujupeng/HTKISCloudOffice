using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Domain.Entities;
using HTKISCloudOffice.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HTKISCloudOffice.Infrastructure.Repositories;

public class RoleRepository : IRoleRepository
{
    private readonly AppDbContext _context;

    public RoleRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Role>> GetByIdsAsync(List<Guid> role_ids)
    {
        return await _context.roles.Where(r => role_ids.Contains(r.role_id)).ToListAsync();
    }

    public async Task<Role?> GetByIdAsync(Guid role_id)
    {
        return await _context.roles.FindAsync(role_id);
    }

    public async Task<List<Role>> GetAllAsync()
    {
        return await _context.roles.ToListAsync();
    }

    public async Task<Role> CreateAsync(Role role)
    {
        _context.roles.Add(role);
        await _context.SaveChangesAsync();
        return role;
    }

    public async Task<List<Role>> GetByUserIdAsync(Guid user_id)
    {
        return await _context.user_roles
            .Where(ur => ur.user_id == user_id)
            .Include(ur => ur.Role)
            .Select(ur => ur.Role)
            .ToListAsync();
    }
}