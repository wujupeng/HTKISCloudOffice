using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace HTKISCloudOffice.IntegrationTests;

public static class TestAuthHelper
{
    private const string SecretKey = "HTKIS_CLOUD_OFFICE_JWT_SECRET_KEY_2024_SECURE_RANDOM_32CHARS";

    public static string GenerateToken(string user_id, string username, string[] roles, int expiration_hours = 8)
    {
        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(SecretKey));
        var creds = new Microsoft.IdentityModel.Tokens.SigningCredentials(
            key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user_id),
            new(JwtRegisteredClaimNames.UniqueName, username),
            new("roles", string.Join(",", roles)),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var token = new JwtSecurityToken(
            issuer: "htkis-cloud-office",
            audience: "htkis-cloud-office-users",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expiration_hours),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}