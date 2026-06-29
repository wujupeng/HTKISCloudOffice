using HTKISCloudOffice.Domain.Entities;

namespace HTKISCloudOffice.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByIdAsync(Guid user_id);
    Task UpdateAutoLoginTokenAsync(Guid user_id, string token, DateTime expires_at);
    Task UpdateLastLoginAsync(Guid user_id, DateTime login_time);
    Task<User> CreateAsync(User user);
    Task UpdateAsync(User user);
    Task<(List<User> users, int total)> ListAsync(int page, int page_size, string? department = null, bool? is_active = null);
}