using System.Security.Claims;
using HTKISCloudOffice.Application.DTOs;

namespace HTKISCloudOffice.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string username, string password, string ip_address);
    Task<AuthResult> AutoLoginAsync(string auto_login_token, string ip_address);
    Task<ClaimsPrincipal> ValidateTokenAsync(string token);
    Task RevokeSessionAsync(string user_id);
    Task<string> RefreshAutoLoginTokenAsync(string user_id);
}