using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Infrastructure.Data;
using HTKISCloudOffice.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HTKISCloudOffice.Infrastructure.Repositories;

public class AppFavoriteRepository : IAppFavoriteRepository
{
    private readonly AppDbContext _context;

    public AppFavoriteRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<AppFavorite>> GetByUserIdAsync(Guid user_id)
    {
        return await _context.app_favorites
            .Include(f => f.Application)
            .Where(f => f.user_id == user_id)
            .OrderByDescending(f => f.created_at)
            .ToListAsync();
    }

    public async Task<AppFavorite?> AddAsync(AppFavorite favorite)
    {
        var exists = await _context.app_favorites
            .AnyAsync(f => f.user_id == favorite.user_id && f.app_id == favorite.app_id);
        if (exists) return null;

        _context.app_favorites.Add(favorite);
        await _context.SaveChangesAsync();
        return favorite;
    }

    public async Task<bool> RemoveAsync(Guid user_id, Guid app_id)
    {
        var favorite = await _context.app_favorites
            .FirstOrDefaultAsync(f => f.user_id == user_id && f.app_id == app_id);
        if (favorite == null) return false;

        _context.app_favorites.Remove(favorite);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> IsFavoritedAsync(Guid user_id, Guid app_id)
    {
        return await _context.app_favorites
            .AnyAsync(f => f.user_id == user_id && f.app_id == app_id);
    }

    public async Task<List<Guid>> GetFavoriteAppIdsAsync(Guid user_id)
    {
        return await _context.app_favorites
            .Where(f => f.user_id == user_id)
            .Select(f => f.app_id)
            .ToListAsync();
    }
}