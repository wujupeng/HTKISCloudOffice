using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Domain.Entities;
using HTKISCloudOffice.Domain.Enums;
using HTKISCloudOffice.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HTKISCloudOffice.Infrastructure.Repositories;

public class ConnectionConfigRepository : IConnectionConfigRepository
{
    private readonly AppDbContext _context;

    public ConnectionConfigRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<ConnectionConfig>> GetAllAsync()
    {
        return await _context.connection_configs
            .Include(c => c.connection_allowed_roles)
            .OrderBy(c => c.sort_order)
            .ToListAsync();
    }

    public async Task<ConnectionConfig?> GetByIdAsync(Guid connection_id)
    {
        return await _context.connection_configs
            .Include(c => c.connection_allowed_roles)
            .FirstOrDefaultAsync(c => c.connection_id == connection_id);
    }

    public async Task<List<ConnectionConfig>> GetByUserRolesAsync(List<Guid> role_ids)
    {
        return await _context.connection_configs
            .Include(c => c.connection_allowed_roles)
            .Where(c => c.is_active && c.connection_allowed_roles.Any(r => role_ids.Contains(r.role_id)))
            .OrderBy(c => c.sort_order)
            .ToListAsync();
    }

    public async Task<ConnectionConfig> CreateAsync(ConnectionConfig config, List<Guid> allowed_role_ids)
    {
        _context.connection_configs.Add(config);

        foreach (var role_id in allowed_role_ids)
        {
            _context.connection_allowed_roles.Add(new ConnectionAllowedRole
            {
                connection_id = config.connection_id,
                role_id = role_id
            });
        }

        await _context.SaveChangesAsync();
        return config;
    }

    public async Task<ConnectionConfig> UpdateAsync(ConnectionConfig config, List<Guid>? allowed_role_ids)
    {
        config.updated_at = DateTime.UtcNow;

        if (allowed_role_ids != null)
        {
            var existing = await _context.connection_allowed_roles
                .Where(r => r.connection_id == config.connection_id)
                .ToListAsync();
            _context.connection_allowed_roles.RemoveRange(existing);

            foreach (var role_id in allowed_role_ids)
            {
                _context.connection_allowed_roles.Add(new ConnectionAllowedRole
                {
                    connection_id = config.connection_id,
                    role_id = role_id
                });
            }
        }

        _context.connection_configs.Update(config);
        await _context.SaveChangesAsync();
        return config;
    }

    public async Task DeleteAsync(Guid connection_id)
    {
        var config = await _context.connection_configs.FindAsync(connection_id);
        if (config != null)
        {
            config.is_active = false;
            config.updated_at = DateTime.UtcNow;
            _context.connection_configs.Update(config);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<ConnectionConfig>> GetActiveByProtocolAsync(ConnectionProtocol protocol)
    {
        return await _context.connection_configs
            .Include(c => c.connection_allowed_roles)
            .Where(c => c.is_active && c.protocol == protocol)
            .OrderBy(c => c.sort_order)
            .ToListAsync();
    }
}