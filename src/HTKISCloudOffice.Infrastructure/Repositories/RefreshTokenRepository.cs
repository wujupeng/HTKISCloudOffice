using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Domain.Entities;
using HTKISCloudOffice.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HTKISCloudOffice.Infrastructure.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _context;

    public RefreshTokenRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<RefreshToken?> GetByTokenHashAsync(string token_hash)
    {
        return await _context.refresh_tokens.FirstOrDefaultAsync(t => t.token_hash == token_hash);
    }

    public async Task CreateAsync(RefreshToken token)
    {
        _context.refresh_tokens.Add(token);
        await _context.SaveChangesAsync();
    }

    public async Task RevokeAsync(Guid token_id)
    {
        var token = await _context.refresh_tokens.FindAsync(token_id);
        if (token != null)
        {
            token.is_revoked = true;
            await _context.SaveChangesAsync();
        }
    }

    public async Task RevokeAllForUserAsync(Guid user_id)
    {
        var tokens = await _context.refresh_tokens
            .Where(t => t.user_id == user_id && !t.is_revoked)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.is_revoked = true;
        }

        await _context.SaveChangesAsync();
    }

    public async Task CleanupExpiredAsync()
    {
        await _context.refresh_tokens
            .Where(t => t.expires_at < DateTime.UtcNow)
            .ExecuteDeleteAsync();
    }
}