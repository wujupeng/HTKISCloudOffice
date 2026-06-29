namespace HTKISCloudOffice.Application.Interfaces;

public interface IAppFavoriteRepository
{
    Task<List<Domain.Entities.AppFavorite>> GetByUserIdAsync(Guid user_id);
    Task<Domain.Entities.AppFavorite?> AddAsync(Domain.Entities.AppFavorite favorite);
    Task<bool> RemoveAsync(Guid user_id, Guid app_id);
    Task<bool> IsFavoritedAsync(Guid user_id, Guid app_id);
    Task<List<Guid>> GetFavoriteAppIdsAsync(Guid user_id);
}