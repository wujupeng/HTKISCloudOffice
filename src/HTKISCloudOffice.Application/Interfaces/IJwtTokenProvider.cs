using System.Security.Claims;

namespace HTKISCloudOffice.Application.Interfaces;

public interface IJwtTokenProvider
{
    string GenerateToken(Guid user_id, string username, List<string> roles, int? expiration_hours = null);
    string GenerateAutoLoginToken(Guid user_id, string device_id);
    ClaimsPrincipal ValidateToken(string token);
    DateTime GetTokenExpiration(string token);
}

public interface IAesEncryptionService
{
    string Encrypt(string plain_text);
    string Decrypt(string cipher_text);
}