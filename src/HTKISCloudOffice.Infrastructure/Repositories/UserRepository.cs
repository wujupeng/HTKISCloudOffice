using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Domain.Entities;
using HTKISCloudOffice.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HTKISCloudOffice.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        return await _context.users
            .Include(u => u.user_roles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.username == username);
    }

    public async Task<User?> GetByIdAsync(Guid user_id)
    {
        return await _context.users
            .Include(u => u.user_roles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.user_id == user_id);
    }

    public async Task UpdateAutoLoginTokenAsync(Guid user_id, string token, DateTime expires_at)
    {
        var user = await _context.users.FindAsync(user_id);
        if (user != null)
        {
            user.auto_login_token = token;
            user.auto_login_token_expires_at = expires_at;
            user.updated_at = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task UpdateLastLoginAsync(Guid user_id, DateTime login_time)
    {
        var user = await _context.users.FindAsync(user_id);
        if (user != null)
        {
            user.updated_at = login_time;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<User> CreateAsync(User user)
    {
        _context.users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task UpdateAsync(User user)
    {
        user.updated_at = DateTime.UtcNow;
        _context.users.Update(user);
        await _context.SaveChangesAsync();
    }

    public async Task<(List<User> users, int total)> ListAsync(int page, int page_size, string? department = null, bool? is_active = null)
    {
        var query = _context.users.Include(u => u.user_roles).ThenInclude(ur => ur.Role).AsQueryable();

        if (!string.IsNullOrEmpty(department))
            query = query.Where(u => u.department == department);

        if (is_active.HasValue)
            query = query.Where(u => u.is_active == is_active.Value);

        var total = await query.CountAsync();
        var users = await query
            .OrderBy(u => u.username)
            .Skip((page - 1) * page_size)
            .Take(page_size)
            .ToListAsync();

        return (users, total);
    }
}