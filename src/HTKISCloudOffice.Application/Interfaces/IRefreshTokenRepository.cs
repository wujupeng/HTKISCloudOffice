namespace HTKISCloudOffice.Application.Interfaces;

public interface IRefreshTokenRepository
{
    Task<Domain.Entities.RefreshToken?> GetByTokenHashAsync(string token_hash);
    Task CreateAsync(Domain.Entities.RefreshToken token);
    Task RevokeAsync(Guid token_id);
    Task RevokeAllForUserAsync(Guid user_id);
    Task CleanupExpiredAsync();
}